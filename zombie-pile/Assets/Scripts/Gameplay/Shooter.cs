using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombiePile
{
    /// One of the two shooters: Shaun on the wall (SMG / rifle / shotgun, throws barrels) and Lis on the
    /// tower (sniper rifle, sees the pile on the wall face). The one the camera is on follows the player's
    /// aim; the other keeps firing on its own at a lower rate (Lis goes for the zombies about to climb over).
    public class Shooter : MonoBehaviour
    {
        public static Shooter Wall, Tower;
        public static Shooter Active { get { return CameraRig.I != null && CameraRig.I.View == 1 ? Tower : Wall; } }

        public bool IsTower { get; private set; }
        public bool Firing { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public float BarrelReady { get { return 1f - Mathf.Clamp01(barrelT / Econ.BarrelCooldown(Econ.Lvl(Up.BarrelReload))); } }
        public float AirReady { get { return 1f - Mathf.Clamp01(airT / Econ.AirstrikeCooldown(Econ.Lvl(Up.Airstrike))); } }

        Transform yaw, muzzle;
        GameObject model;
        Renderer gunR;
        Kit.Anim kit;
        Camera cam;
        float fireT, barrelT, airT, aiT, kick;
        Zombie aiTarget;
        string shownWeapon;
        readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly HashSet<Zombie> seen = new HashSet<Zombie>();
        const int ShotMask = (1 << 0) | (1 << Arena.Props) | (1 << Zombie.Layer);

        public static void BuildBoth()
        {
            Wall = Make("Shaun", "Characters_Shaun", new Vector3(0f, Arena.WallHeight, Arena.WallFront - 1.3f), false);
            Tower = Make("Lis", "Characters_Lis", Arena.TowerSpot, true);
        }

        static Shooter Make(string name, string modelName, Vector3 pos, bool tower)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var s = go.AddComponent<Shooter>();
            s.IsTower = tower;
            s.cam = Camera.main;
            // the soldier turns as a whole ('yaw'); the model inside keeps any facing correction from Kit
            s.yaw = new GameObject("Yaw").transform;
            s.yaw.SetParent(go.transform, false);
            var holder = Kit.Place(modelName, pos, 0f, 1f, s.yaw, true);
            s.model = holder;
            s.kit = Kit.Anim.From(holder);
            s.kit.Start(s.kit.idle);
            s.muzzle = new GameObject("Muzzle").transform;
            s.muzzle.SetParent(s.yaw, false);
            s.muzzle.localPosition = new Vector3(0.2f, 1.1f, 0.9f);
            s.AimPoint = new Vector3(0f, 1.5f, Arena.WallFront + 8f);
            s.yaw.rotation = Quaternion.LookRotation(Flat(s.AimPoint - pos));
            s.RefreshWeapon();
            return s;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 0.0001f ? Vector3.forward : v; }

        WeaponDef W { get { return IsTower ? Econ.Sniper : Econ.Weapon; } }

        public void RefreshWeapon()
        {
            var w = W;
            if (shownWeapon == w.model) return;
            shownWeapon = w.model;
            gunR = Kit.ShowWeapon(model, w.model);
        }

        // ------------------------------------------------------------------ stats (weapon x upgrades x power-ups)
        float Damage
        {
            get
            {
                float m = IsTower ? Econ.SniperDamageMult(Econ.Lvl(Up.SniperDamage)) : Econ.GunDamageMult(Econ.Lvl(Up.GunDamage));
                return W.damage * m * Game.DamageBoost;
            }
        }
        float Rate
        {
            get
            {
                float m = IsTower ? Econ.SniperRateMult(Econ.Lvl(Up.SniperRate)) : Econ.GunRateMult(Econ.Lvl(Up.GunRate));
                return W.rate * m * Game.RateBoost;
            }
        }
        int Bullets { get { return W.bullets + (IsTower ? 0 : Econ.Lvl(Up.GunBullets)); } }
        int Pierce { get { return W.pierce + (IsTower ? Econ.Lvl(Up.SniperPierce) : 0); } }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            barrelT = Mathf.Max(0f, barrelT - dt);
            airT = Mathf.Max(0f, airT - dt);
            kick = Mathf.Max(0f, kick - dt * 8f);
            bool playing = Game.I != null && Game.I.Playing;
            if (!playing) { Firing = false; Face(dt); return; }

            bool active = Active == this && !CameraRig.I.Switching;
            Ray ray;
            if (active) Firing = ManualAim(out ray);
            else Firing = AiAim(dt, out ray);
            Face(dt);
            if (gunR != null) muzzle.position = gunR.bounds.center + yaw.forward * gunR.bounds.extents.magnitude * 0.85f;

            fireT -= dt;
            float rate = Rate * (active ? 1f : (IsTower ? 0.55f : 0.35f));
            if (Firing && fireT <= 0f)
            {
                fireT = 1f / Mathf.Max(0.1f, rate);
                Shoot(ray, active);
            }
        }

        void Face(float dt)
        {
            var d = Flat(AimPoint - transform.position);
            yaw.rotation = Quaternion.Slerp(yaw.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 16f));
            model.transform.localPosition = new Vector3(0f, 0f, -kick * 0.08f);
        }

        static bool PointerOverUI(int finger)
        {
            var es = EventSystem.current;
            return es != null && (finger >= 0 ? es.IsPointerOverGameObject(finger) : es.IsPointerOverGameObject());
        }

        /// The player's pointer (first finger or mouse), with aim assist toward the zombie nearest to it.
        bool ManualAim(out Ray ray)
        {
            Vector2 screen; bool down;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                screen = t.position;
                down = !PointerOverUI(t.fingerId) && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            }
            else
            {
                screen = Input.mousePosition;
                down = Input.GetMouseButton(0) && !PointerOverUI(-1);
            }
            ray = cam.ScreenPointToRay(screen);
            var assisted = Assist(screen);
            if (assisted.HasValue) ray = new Ray(cam.transform.position, (assisted.Value - cam.transform.position).normalized);
            RaycastHit h;
            if (Physics.Raycast(ray, out h, 300f, ShotMask, QueryTriggerInteraction.Ignore)) AimPoint = h.point;
            else AimPoint = ray.GetPoint(40f);
            return down;
        }

        /// Snaps to a zombie's head (or body) if the pointer is close to it on screen.
        Vector3? Assist(Vector2 screen)
        {
            float radius = 46f * Screen.height / 720f;
            float best = radius * radius;
            Vector3? pick = null;
            for (int i = 0; i < Zombie.Alive.Count; i++)
            {
                var z = Zombie.Alive[i];
                if (z == null) continue;
                if (z.HeadCollider != null)
                {
                    var hp = cam.WorldToScreenPoint(z.Head.position);
                    if (hp.z > 0f)
                    {
                        float d = ((Vector2)hp - screen).sqrMagnitude;
                        if (d < best * 0.6f) { best = d / 0.6f; pick = z.Head.position; }
                    }
                }
                var cp = cam.WorldToScreenPoint(z.Center);
                if (cp.z > 0f)
                {
                    float d = ((Vector2)cp - screen).sqrMagnitude;
                    if (d < best) { best = d; pick = z.Center; }
                }
            }
            return pick;
        }

        /// The shooter the camera is not on: picks a target every now and then and fires at it.
        bool AiAim(float dt, out Ray ray)
        {
            aiT -= dt;
            if (aiT <= 0f || aiTarget == null || !aiTarget.IsAlive)
            {
                aiT = 0.35f;
                aiTarget = IsTower ? PickForTower() : PickForWall();
            }
            if (aiTarget == null || !aiTarget.IsAlive) { ray = new Ray(muzzle.position, yaw.forward); return false; }
            var at = aiTarget.HeadCollider != null && IsTower ? aiTarget.Head.position : aiTarget.Center;
            at += Random.insideUnitSphere * (IsTower ? 0.12f : 0.35f);
            AimPoint = at;
            var o = muzzle.position;
            ray = new Ray(o, (at - o).normalized);
            return true;
        }

        static Zombie PickForTower()
        {
            // whatever is about to climb over first: leapers in the air, then the top of the pile
            foreach (var z in Zombie.Alive) if (z != null && z.State == ZState.Leap) return z;
            var top = Pile.Highest();
            if (top != null && top.transform.position.y > 1.5f) return top;
            return Nearest();
        }

        static Zombie PickForWall()
        {
            Zombie best = null;
            float bz = 30f;
            foreach (var z in Zombie.Alive)
            {
                if (z == null || z.State != ZState.Run) continue;
                float d = z.transform.position.z;
                if (d < bz) { bz = d; best = z; }
            }
            return best ?? Nearest();
        }

        static Zombie Nearest()
        {
            Zombie best = null;
            float bz = 40f;
            foreach (var z in Zombie.Alive) { if (z == null) continue; float d = z.transform.position.z; if (d < bz) { bz = d; best = z; } }
            return best;
        }

        // ------------------------------------------------------------------ shooting
        void Shoot(Ray aim, bool manual)
        {
            kick = 1f;
            Fx.I.MuzzleFlash(muzzle.position, IsTower ? 0.7f : 0.45f);
            if (IsTower) SoundBank.I.Play(SoundBank.I.sniper, manual ? 0.55f : 0.3f, Random.Range(0.95f, 1.05f));
            else SoundBank.I.Play(Bullets > 3 ? SoundBank.I.boom : SoundBank.I.shot, manual ? (Bullets > 3 ? 0.25f : 0.33f) : 0.16f, Random.Range(0.9f, 1.1f));
            if (manual) CameraRig.I.Shake(IsTower ? 0.2f : 0.1f);
            Physics.SyncTransforms();
            var w = W;
            int n = Bullets;
            for (int b = 0; b < n; b++)
            {
                float spread = w.spread + (n > 1 && w.bullets == 1 ? 1.2f : 0f);
                float off = n == 1 || w.bullets > 1 ? 0f : (b - (n - 1) * 0.5f) * 1.6f;
                var right = Vector3.Cross(Vector3.up, aim.direction).normalized;
                var dir = Quaternion.AngleAxis(off + Random.Range(-spread, spread), Vector3.up)
                        * Quaternion.AngleAxis(Random.Range(-spread, spread) * 0.6f, right) * aim.direction;
                FireRay(new Ray(aim.origin, dir), manual);
            }
        }

        void FireRay(Ray ray, bool manual)
        {
            int n = Physics.RaycastNonAlloc(ray, hits, 300f, ShotMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, n, HitComparer.Instance);
            int left = Pierce;
            Vector3 end = ray.GetPoint(60f);
            seen.Clear();
            var w = W;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                var z = hit.collider.GetComponentInParent<Zombie>();
                if (z == null)
                {
                    var drum = hit.collider.GetComponentInParent<ExplosiveBarrel>();
                    if (drum != null) { end = hit.point; drum.Detonate(); break; }
                    var crate = hit.collider.GetComponentInParent<Crate>();
                    if (crate != null) { end = hit.point; crate.Hit(); break; }
                    end = hit.point;
                    Fx.I.Dust(hit.point, 0.25f);
                    break;
                }
                if (seen.Contains(z) || !z.IsAlive) continue;
                seen.Add(z);
                bool head = z.HeadCollider != null && hit.collider == z.HeadCollider && z.Armor <= 0f;
                float dmg = Damage * (head ? w.headMult : 1f) * Random.Range(0.9f, 1.1f);
                bool killed = z.Hit(dmg, ray.direction, hit.point, head, w.kick);
                if (Hud.I != null)
                {
                    Hud.I.Damage(hit.point, Mathf.RoundToInt(dmg), head);
                    if (manual) Hud.I.HitMark(killed);
                }
                SoundBank.I.Play(head ? SoundBank.I.headshot : SoundBank.I.hit, head ? 0.45f : 0.28f, Random.Range(0.9f, 1.15f));
                end = hit.point;
                if (left-- <= 0) break;
            }
            Fx.I.Tracer(muzzle.position, end, IsTower);
        }

        // ------------------------------------------------------------------ abilities (the wall gunner's)
        public void ThrowBarrel()
        {
            if (Game.I == null || !Game.I.Playing || barrelT > 0f) return;
            barrelT = Econ.BarrelCooldown(Econ.Lvl(Up.BarrelReload));
            SoundBank.I.Play(SoundBank.I.throwS, 0.6f);
            int l = Econ.Lvl(Up.BarrelPower);
            var target = Active.AimPoint;
            target.z = Mathf.Max(target.z, Arena.WallFront + 0.8f);
            ThrownBarrel.Throw(muzzle.position + Vector3.up * 0.4f, target, Econ.BarrelRadius(l), Econ.BarrelDamage(l));
        }

        public void CallAirstrike()
        {
            if (Game.I == null || !Game.I.Playing || Econ.Lvl(Up.Airstrike) <= 0 || airT > 0f) return;
            airT = Econ.AirstrikeCooldown(Econ.Lvl(Up.Airstrike));
            Airstrike.Call(Active.AimPoint);
            if (Hud.I != null) Hud.I.Banner("AIRSTRIKE!", "", 1.1f);
        }

        public void ResetCooldowns() { barrelT = 0f; airT = 0f; }

        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }
    }
}
