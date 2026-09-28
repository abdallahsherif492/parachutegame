using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombiePile
{
    /// The soldier on the wall. Hold to fire at the pointer (mouse or finger); throw explosive barrels.
    public class Gunner : MonoBehaviour
    {
        public static Gunner I;

        // upgradable stats
        public float fireRate = 10f, damage = 1f, headMult = 2.5f, spread = 1.2f;
        public int bullets = 1, pierce = 0;
        public float barrelCooldown = 5f, barrelRadius = 3.6f;
        public bool cluster;

        public float BarrelReady { get { return 1f - Mathf.Clamp01(barrelT / barrelCooldown); } }
        public bool Firing { get; private set; }
        public Vector3 AimPoint { get; private set; }

        Transform yaw, gun, muzzle;
        float fireT, barrelT, kick;
        Camera cam;
        readonly RaycastHit[] hits = new RaycastHit[24];
        const int Mask = ~(1 << Arena.IgnoreRaycast);

        public static Gunner Build()
        {
            var go = new GameObject("Gunner");
            go.transform.position = new Vector3(0f, Arena.WallHeight + 0.2f, -0.35f);
            return go.AddComponent<Gunner>();
        }

        void Awake()
        {
            I = this;
            cam = Camera.main;
            var army = new Color(0.36f, 0.45f, 0.27f);
            var dark = new Color(0.2f, 0.24f, 0.18f);
            var skin = new Color(0.95f, 0.75f, 0.58f);
            yaw = Shapes.Group("Yaw", transform, Vector3.zero).transform;
            Shapes.Make("LegL", MeshGen.Capsule(0.45f), Mat.Lit(dark), yaw, new Vector3(-0.17f, 0.45f, 0), new Vector3(0.26f, 0.9f, 0.3f));
            Shapes.Make("LegR", MeshGen.Capsule(0.45f), Mat.Lit(dark), yaw, new Vector3(0.17f, 0.45f, 0), new Vector3(0.26f, 0.9f, 0.3f));
            Shapes.Make("Torso", MeshGen.Capsule(0.42f), Mat.Lit(army), yaw, new Vector3(0, 1.25f, 0), new Vector3(0.72f, 0.95f, 0.5f));
            Shapes.Box(yaw, new Vector3(0, 1.3f, -0.2f), new Vector3(0.6f, 0.5f, 0.2f), dark);   // backpack
            Shapes.Make("Head", MeshGen.Smooth(), Mat.Lit(skin), yaw, new Vector3(0, 1.95f, 0), Vector3.one * 0.46f);
            Shapes.Make("Helmet", MeshGen.Capsule(0.46f), Mat.Lit(army * 0.9f), yaw, new Vector3(0, 2.07f, 0), new Vector3(0.56f, 0.34f, 0.58f));
            gun = Shapes.Group("Gun", yaw, new Vector3(0.22f, 1.45f, 0.25f)).transform;
            Shapes.Box(gun, new Vector3(0, 0, 0.35f), new Vector3(0.13f, 0.17f, 0.9f), new Color(0.16f, 0.16f, 0.18f));
            Shapes.Box(gun, new Vector3(0, -0.14f, 0.2f), new Vector3(0.1f, 0.22f, 0.12f), new Color(0.16f, 0.16f, 0.18f));
            Shapes.Make("Barrel", MeshGen.Cylinder(), Mat.Lit(new Color(0.1f, 0.1f, 0.11f)), gun, new Vector3(0, 0.02f, 0.95f), new Vector3(0.07f, 0.35f, 0.07f), Quaternion.Euler(90, 0, 0));
            Shapes.Make("ArmL", MeshGen.Capsule(0.45f), Mat.Lit(army), gun, new Vector3(-0.2f, -0.05f, 0.25f), new Vector3(0.18f, 0.6f, 0.18f), Quaternion.Euler(75, 20, 0));
            Shapes.Make("ArmR", MeshGen.Capsule(0.45f), Mat.Lit(army), gun, new Vector3(0.12f, -0.1f, -0.05f), new Vector3(0.18f, 0.5f, 0.18f), Quaternion.Euler(60, 0, 0));
            muzzle = Shapes.Group("Muzzle", gun, new Vector3(0, 0.02f, 1.15f)).transform;
            AimPoint = new Vector3(0, 1, 12);
        }

        bool PointerOverUI(int finger)
        {
            var es = EventSystem.current;
            return es != null && (finger >= 0 ? es.IsPointerOverGameObject(finger) : es.IsPointerOverGameObject());
        }

        void Update()
        {
            float dt = Time.deltaTime;
            barrelT = Mathf.Max(0f, barrelT - dt);
            kick = Mathf.Max(0f, kick - dt * 10f);
            if (Game.I == null || !Game.I.Playing) { Firing = false; return; }

            // pointer: first finger on phones, the mouse on desktop
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
            var ray = cam.ScreenPointToRay(screen);
            RaycastHit h;
            if (Physics.Raycast(ray, out h, 300f, Mask, QueryTriggerInteraction.Ignore)) AimPoint = h.point;
            else AimPoint = ray.GetPoint(30f);

            // turn the soldier toward the aim point
            var flat = AimPoint - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) yaw.rotation = Quaternion.Slerp(yaw.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-dt * 20f));
            var local = yaw.InverseTransformPoint(AimPoint) - gun.localPosition;
            float pitch = -Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            gun.localRotation = Quaternion.Euler(pitch, 0, 0);
            gun.localPosition = new Vector3(0.22f, 1.45f, 0.25f - kick * 0.12f);

            Firing = down;
            fireT -= dt;
            if (down && fireT <= 0f) { fireT = 1f / fireRate; Shoot(ray); }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) ThrowBarrel();
        }

        void Shoot(Ray aim)
        {
            kick = 1f;
            Fx.I.MuzzleFlash(muzzle.position);
            SoundBank.I.Play(SoundBank.I.shot, 0.35f, Random.Range(0.9f, 1.1f));
            CameraRig.I.Shake(0.12f);
            for (int b = 0; b < bullets; b++)
            {
                float off = bullets == 1 ? 0f : (b - (bullets - 1) * 0.5f) * 2.4f;
                var dir = Quaternion.AngleAxis(off + Random.Range(-spread, spread), Vector3.up) * Quaternion.AngleAxis(Random.Range(-spread, spread) * 0.5f, cam.transform.right) * aim.direction;
                FireRay(new Ray(aim.origin, dir));
            }
        }

        void FireRay(Ray ray)
        {
            int n = Physics.RaycastNonAlloc(ray, hits, 300f, Mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, n, HitComparer.Instance);
            int left = pierce;
            Vector3 end = ray.GetPoint(60f);
            var seen = new HashSet<Zombie>();
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                var z = hit.collider.GetComponentInParent<Zombie>();
                if (z == null)
                {
                    end = hit.point;
                    Fx.I.Burst(hit.point, 3, new Color(0.6f, 0.55f, 0.5f), 2f, 0.08f);
                    break;
                }
                if (seen.Contains(z)) continue;
                seen.Add(z);
                if (!z.IsAlive) { z.Hit(0f, ray.direction, hit.point, false); continue; }   // bodies don't stop bullets
                bool head = hit.collider == z.HeadCollider;
                bool killed = z.Hit(damage * (head ? headMult : 1f), ray.direction, hit.point, head);
                SoundBank.I.Play(head ? SoundBank.I.headshot : SoundBank.I.hit, head ? 0.5f : 0.35f, Random.Range(0.9f, 1.15f));
                if (killed) Game.I.OnKill(z, head, hit.point);
                end = hit.point;
                if (left-- <= 0) break;
            }
            Fx.I.Tracer(muzzle.position, end);
        }

        public void ThrowBarrel()
        {
            if (Game.I == null || !Game.I.Playing || barrelT > 0f) return;
            barrelT = barrelCooldown;
            SoundBank.I.Play(SoundBank.I.throwS, 0.6f);
            ThrownBarrel.Throw(muzzle.position + Vector3.up * 0.4f, AimPoint, barrelRadius, cluster);
        }

        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
        }
    }

    /// An explosive barrel lobbed onto the pile.
    public class ThrownBarrel : MonoBehaviour
    {
        Vector3 from, to;
        float t, radius, flight;
        bool cluster;

        public static void Throw(Vector3 from, Vector3 to, float radius, bool cluster)
        {
            var go = new GameObject("ThrownBarrel");
            var b = go.AddComponent<ThrownBarrel>();
            b.from = from; b.to = to; b.radius = radius; b.cluster = cluster;
            b.flight = Mathf.Clamp(Vector3.Distance(from, to) / 18f, 0.45f, 1f);
            Shapes.Cylinder(go.transform, Vector3.zero, new Vector3(0.6f, 0.85f, 0.6f), new Color(0.85f, 0.2f, 0.15f));
            Shapes.Cylinder(go.transform, Vector3.zero, new Vector3(0.62f, 0.15f, 0.62f), new Color(1f, 0.8f, 0.15f), 0.3f);
            go.transform.position = from;
        }

        void Update()
        {
            t += Time.deltaTime / flight;
            var p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 3.5f;
            transform.position = p;
            transform.Rotate(new Vector3(400f, 90f, 0f) * Time.deltaTime);
            if (t < 1f) return;
            Explode(to, radius, 16f, 14f);
            if (cluster)
                for (int i = 0; i < 3; i++)
                {
                    var off = Quaternion.Euler(0, i * 120f + Random.Range(-20f, 20f), 0) * Vector3.forward * radius * 0.9f;
                    Game.I.Delay(0.18f + i * 0.08f, () => Explode(to + off, radius * 0.6f, 10f, 8f));
                }
            Destroy(gameObject);
        }

        public static void Explode(Vector3 at, float radius, float force, float dmg)
        {
            Fx.I.Explosion(at, radius);
            SoundBank.I.Play(SoundBank.I.boom, 0.9f, Random.Range(0.9f, 1.05f));
            var cols = Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore);
            var done = new HashSet<Zombie>();
            foreach (var c in cols)
            {
                var z = c.GetComponentInParent<Zombie>();
                if (z == null || done.Contains(z)) continue;
                done.Add(z);
                bool wasAlive = z.IsAlive;
                z.Blast(at, force, radius, dmg);
                if (wasAlive && !z.IsAlive) Game.I.OnKill(z, false, z.transform.position);
            }
        }
    }
}
