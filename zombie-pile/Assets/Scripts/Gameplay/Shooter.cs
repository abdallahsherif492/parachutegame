using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombiePile
{
    /// Who drives a shooter: the local player (the camera is on it), a remote player (network host only),
    /// or the AI safety net.
    public enum Control { AI, Local, Remote }

    /// What a remote player brought with them (their own upgrades), applied to their shooter on the host.
    public class PlayerStats
    {
        public int weapon, gunDmg, gunRate, gunBullets, snDmg, snRate, snPierce, barrelPow, barrelReload, airLevel;
    }

    /// The three shooters, left to right on screen: Lis on the left tower, Shaun on the wall, Sam on the right
    /// tower (slots 0, 1, 2 = keys 1, 2, 3 = camera views 0, 1, 2). Shaun has the SMG / rifle / shotgun and
    /// the barrels and airstrikes; the tower shooters are snipers who see the pile on the wall face. The one
    /// the player is on follows their aim; the others keep firing on their own at a low rate as a safety net
    /// (or are driven by other players in a co-op room).
    public class Shooter : MonoBehaviour
    {
        public static readonly Shooter[] Slots = new Shooter[3];
        public static Shooter Wall { get { return Slots[1]; } }
        public static Shooter Active { get { int v = CameraRig.I != null ? CameraRig.I.View : 1; return Slots[Mathf.Clamp(v, 0, 2)]; } }
        public static readonly string[] Names = { "LIS", "SHAUN", "SAM" };

        public int Slot { get; private set; }
        public Control Ctl = Control.AI;
        public PlayerStats Remote;                       // set on the host for a remote player's shooter
        Vector3 remoteOrigin, remoteDir = Vector3.forward, remoteAim;
        bool remoteFire;
        public bool IsTower { get { return Slot != 1; } }
        public bool Firing { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Ray LastRay { get; private set; }         // the pointer ray of the last frame (sent to the host)
        float netBarrel = 1f, netAir = 1f;
        float barrelMax = 6f, airMax = 40f;
        public float BarrelReady { get { return Net.IsClient ? netBarrel : 1f - Mathf.Clamp01(barrelT / barrelMax); } }
        public float AirReady { get { return Net.IsClient ? netAir : 1f - Mathf.Clamp01(airT / airMax); } }
        /// Cooldowns as the host reports them (clients only show them).
        public void SetNetCooldowns(float barrel, float air) { netBarrel = barrel; netAir = air; }
        /// Where another player is aiming (clients see the other shooters turn and fire).
        public void SetNetAim(Vector3 aim, bool firing) { AimPoint = aim; Firing = firing; }

        Transform yaw, muzzle;
        GameObject model;
        Renderer gunR;
        Kit.Anim kit;
        Camera cam;
        float fireT, barrelT, airT, aiT, kick;
        Zombie aiTarget;
        int shownWeapon = -1;
        readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly HashSet<Zombie> seen = new HashSet<Zombie>();
        const int ShotMask = (1 << 0) | (1 << Arena.Props) | (1 << Zombie.Layer);

        public static void BuildAll()
        {
            var right = Arena.TowerSpot; right.x = -right.x;
            Slots[0] = Make(0, "Lis", "Characters_Lis", Arena.TowerSpot);
            Slots[1] = Make(1, "Shaun", "Characters_Shaun", new Vector3(0f, Arena.WallHeight, Arena.WallFront - 1.3f));
            Slots[2] = Make(2, "Sam", "Characters_Sam", right);
            SetLocal(1);
        }

        /// The player moves to a slot: it is theirs now, the others go back to the AI (or stay remote).
        public static void SetLocal(int slot)
        {
            for (int i = 0; i < 3; i++)
            {
                var s = Slots[i];
                if (s == null) continue;
                if (i == slot) s.Ctl = Control.Local;
                else if (s.Ctl == Control.Local) s.Ctl = Control.AI;
            }
        }

        static Shooter Make(int slot, string name, string modelName, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var s = go.AddComponent<Shooter>();
            s.Slot = slot;
            s.cam = Camera.main;
            // the soldier turns as a whole ('yaw'); the model inside keeps any facing correction from Kit
            s.yaw = new GameObject("Yaw").transform;
            s.yaw.SetParent(go.transform, false);
            var holder = Kit.Place(modelName, pos, 0f, 1f, s.yaw, true) ?? Kit.Place("Characters_Shaun", pos, 0f, 1f, s.yaw, true);
            if (holder == null) holder = new GameObject("NoModel");     // models missing: the game still starts (see the Console)
            holder.transform.SetParent(s.yaw, true);
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

        int WeaponId { get { return Remote != null ? Remote.weapon : Save.Data.weapon; } }
        WeaponDef W { get { return IsTower ? Econ.Sniper : Econ.Weapons[Mathf.Clamp(WeaponId, 0, Econ.Weapons.Length - 1)]; } }
        int L(Up u, int remote) { return Remote != null ? remote : Econ.Lvl(u); }

        public void RefreshWeapon()
        {
            var w = W;
            if (shownWeapon == w.id) return;
            shownWeapon = w.id;
            gunR = Kit.ShowWeapon(model, w.model, w.tint);
        }

        // ------------------------------------------------------------------ stats (weapon x upgrades x power-ups)
        float Damage
        {
            get
            {
                float m = IsTower ? Econ.SniperDamageMult(L(Up.SniperDamage, Remote != null ? Remote.snDmg : 0)) : Econ.GunDamageMult(L(Up.GunDamage, Remote != null ? Remote.gunDmg : 0));
                return W.damage * m * Game.DamageBoost;
            }
        }
        float Rate
        {
            get
            {
                float m = IsTower ? Econ.SniperRateMult(L(Up.SniperRate, Remote != null ? Remote.snRate : 0)) : Econ.GunRateMult(L(Up.GunRate, Remote != null ? Remote.gunRate : 0));
                return W.rate * m * Game.RateBoost;
            }
        }
        int Bullets { get { return W.bullets + (IsTower ? 0 : L(Up.GunBullets, Remote != null ? Remote.gunBullets : 0)); } }
        int Pierce { get { return W.pierce + (IsTower ? L(Up.SniperPierce, Remote != null ? Remote.snPierce : 0) : 0); } }

        /// Remote player's pointer, from the network (host side).
        public void SetRemoteInput(Vector3 origin, Vector3 dir, Vector3 aim, bool fire)
        {
            remoteOrigin = origin; remoteDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward; remoteAim = aim; remoteFire = fire;
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            barrelT = Mathf.Max(0f, barrelT - dt);
            airT = Mathf.Max(0f, airT - dt);
            kick = Mathf.Max(0f, kick - dt * 8f);
            bool playing = Game.I != null && Game.I.Playing;
            if (!playing) { Firing = false; Face(dt); return; }

            bool active = Ctl == Control.Local && !CameraRig.I.Switching;
            Ray ray;
            if (Ctl == Control.Remote) { ray = new Ray(remoteOrigin, remoteDir); AimPoint = remoteAim; Firing = remoteFire; active = true; }
            else if (active) { Firing = ManualAim(out ray); LastRay = ray; }
            else if (Net.IsClient) ray = new Ray(muzzle.position, yaw.forward);      // aim and fire come from the host
            else Firing = AiAim(dt, out ray);
            Face(dt);
            if (gunR != null) muzzle.position = gunR.bounds.center + yaw.forward * gunR.bounds.extents.magnitude * 0.85f;

            fireT -= dt;
            // the idle shooter is only a safety net: slow, and it only fires at real threats
            float rate = Rate * (active ? 1f : (IsTower ? 0.18f : 0.12f));
            if (Net.IsClient) { if (Ctl != Control.Local) return; }   // clients only show their own shots; the host fires for everybody
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
            at += Random.insideUnitSphere * (IsTower ? 0.3f : 0.6f);
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
            if (top != null && top.transform.position.y > Arena.WallHeight * 0.45f) return top;
            return null;
        }

        static Zombie PickForWall()
        {
            Zombie best = null;
            float bz = Arena.WallFront + 9f;   // only what is already close to the wall
            foreach (var z in Zombie.Alive)
            {
                if (z == null || z.State != ZState.Run) continue;
                float d = z.transform.position.z;
                if (d < bz) { bz = d; best = z; }
            }
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
                    if (drum != null) { end = hit.point; if (!Net.IsClient) drum.Detonate(); break; }
                    var crate = hit.collider.GetComponentInParent<Crate>();
                    if (crate != null) { end = hit.point; if (!Net.IsClient) crate.Hit(); break; }
                    end = hit.point;
                    Fx.I.Dust(hit.point, 0.25f);
                    break;
                }
                if (seen.Contains(z) || !z.IsAlive) continue;
                seen.Add(z);
                if (Net.IsClient)
                {
                    // the host does the damage: here only the tracer and a puff of blood
                    end = hit.point;
                    Fx.I.Blood(hit.point, -ray.direction, 3);
                    if (left-- <= 0) break;
                    continue;
                }
                bool head = z.HeadCollider != null && hit.collider == z.HeadCollider && z.Armor <= 0f;
                float dmg = Damage * (head ? w.headMult : 1f) * Random.Range(0.9f, 1.1f) * (manual ? 1f : 0.5f);
                bool killed = z.Hit(dmg, ray.direction, hit.point, head, w.kick);
                if (Hud.I != null)
                {
                    Game.Dmg(hit.point, Mathf.RoundToInt(dmg), head);
                    if (Net.IsHost) Net.Host.Hit(z.Id);
                    if (manual) Hud.I.HitMark(killed);
                }
                SoundBank.I.Play(head ? SoundBank.I.headshot : SoundBank.I.hit, head ? 0.45f : 0.28f, Random.Range(0.9f, 1.15f));
                end = hit.point;
                if (left-- <= 0) break;
            }
            Fx.I.Tracer(muzzle.position, end, IsTower);
            if (Net.IsHost) Net.Host.ShotFx(Slot, muzzle.position, end, IsTower);
        }

        // ------------------------------------------------------------------ abilities (the wall gunner's)
        public void ThrowBarrel() { ThrowBarrelAt(Active.AimPoint, null); }

        /// Whoever presses the button throws a barrel from the wall; a remote player's own upgrades apply.
        public void ThrowBarrelAt(Vector3 aim, PlayerStats st)
        {
            if (Game.I == null || !Game.I.Playing) return;
            if (Net.IsClient) { Net.PressBarrel(); return; }
            if (barrelT > 0f) return;
            int l = st != null ? st.barrelPow : Econ.Lvl(Up.BarrelPower);
            barrelMax = barrelT = Econ.BarrelCooldown(st != null ? st.barrelReload : Econ.Lvl(Up.BarrelReload));
            SoundBank.I.Play(SoundBank.I.throwS, 0.6f);
            aim.z = Mathf.Max(aim.z, Arena.WallFront + 0.8f);
            var from = muzzle.position + Vector3.up * 0.4f;
            ThrownBarrel.Throw(from, aim, Econ.BarrelRadius(l), Econ.BarrelDamage(l), true);
            if (Net.IsHost) Net.Host.BarrelThrown(from, aim);
        }

        public void CallAirstrike() { CallAirstrikeAt(Active.AimPoint, null); }

        public void CallAirstrikeAt(Vector3 aim, PlayerStats st)
        {
            if (Game.I == null || !Game.I.Playing) return;
            if (Net.IsClient) { Net.PressAirstrike(); return; }
            int lv = st != null ? st.airLevel : Econ.Lvl(Up.Airstrike);
            if (lv <= 0 || airT > 0f) return;
            airMax = airT = Econ.AirstrikeCooldown(lv);
            Airstrike.Call(aim);
            Game.Say("AIRSTRIKE!", "", 1.1f);
            if (Net.IsHost) Net.Host.Airstrike(aim);
        }

        public void ResetCooldowns() { barrelT = 0f; airT = 0f; }
        public void SetCooldownMax(float barrel, float air) { barrelMax = barrel; airMax = air; }

        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }
    }
}
