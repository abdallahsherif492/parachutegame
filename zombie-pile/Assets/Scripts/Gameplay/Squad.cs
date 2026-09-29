using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Things that get thrown or fired at the horde. Rocket, Grenade, Cluster and Dynamite are explosions
    /// (Boom); Molotov leaves a fire, Frost slows, Tire rolls through the crowd, Hydrant washes it back.
    public enum Toy { Rocket, Grenade, Molotov, Frost, Cluster, Dynamite, Tire, Hydrant }

    public static class Toys
    {
        public static readonly string[] Names = { "ROCKET!", "GRENADE!", "MOLOTOV!", "FROST BOMB!", "CLUSTER BOMB!", "DYNAMITE!", "TIRE!", "HYDRANT!" };
        static readonly List<Zombie> near = new List<Zombie>();

        /// sim: the simulation (offline or the host). A co-op guest only shows what the host throws.
        public static void Launch(Toy kind, Vector3 from, Vector3 to, bool sim, float power = 1f)
        {
            Projectile.Throw(kind, from, to, sim, power);
            SoundBank.I.Play(kind == Toy.Rocket ? SoundBank.I.boom : SoundBank.I.throwS, kind == Toy.Rocket ? 0.4f : 0.5f, Random.Range(0.9f, 1.1f));
            if (kind == Toy.Rocket) SoundBank.I.Play(SoundBank.I.whoosh, 0.6f);
            if (sim && Net.IsHost) Net.Host.Toy((int)kind, from, to);
        }

        /// The living zombies within r of a point (horizontally), valid until the next call.
        public static List<Zombie> Near(Vector3 c, float r)
        {
            near.Clear();
            foreach (var z in Zombie.Alive)
            {
                if (z == null || z.Puppet || !z.IsAlive) continue;
                var p = z.transform.position;
                float dx = p.x - c.x, dz = p.z - c.z;
                if (dx * dx + dz * dz <= r * r) near.Add(z);
            }
            return near;
        }
    }

    /// One flying toy: an arc (or a straight rocket), then whatever it does on landing.
    public class Projectile : MonoBehaviour
    {
        Toy kind;
        Vector3 from, to, dir;
        float t, flight, arc, power = 1f, trailT, fuse, rollT, hitT;
        bool sim, landed, rolling;
        Transform body;
        readonly Dictionary<Zombie, float> touched = new Dictionary<Zombie, float>();

        public static Projectile Throw(Toy kind, Vector3 from, Vector3 to, bool sim, float power)
        {
            var go = new GameObject("Toy " + kind);
            var p = go.AddComponent<Projectile>();
            p.kind = kind; p.from = from; p.to = to; p.sim = sim; p.power = power;
            float d = Vector3.Distance(from, to);
            bool rocket = kind == Toy.Rocket;
            p.flight = rocket ? Mathf.Clamp(d / 34f, 0.3f, 1.3f) : Mathf.Clamp(d / 16f, 0.55f, 1.5f);
            p.arc = rocket ? 0.6f : Mathf.Clamp(d * 0.16f, 1.8f, 5f);
            p.dir = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
            p.Build();
            go.transform.position = from;
            return p;
        }

        void Build()
        {
            var b = new GameObject("Body").transform;
            b.SetParent(transform, false);
            body = b;
            switch (kind)
            {
                case Toy.Rocket:
                    Shapes.Make("Tube", MeshGen.Cylinder(), Mat.Lit(new Color(0.42f, 0.45f, 0.3f)), b, Vector3.zero, new Vector3(0.17f, 0.9f, 0.17f), Quaternion.Euler(90f, 0f, 0f));
                    Shapes.Make("Tip", MeshGen.Cone(), Mat.Lit(new Color(0.9f, 0.25f, 0.15f)), b, new Vector3(0f, 0f, 0.55f), new Vector3(0.17f, 0.3f, 0.17f), Quaternion.Euler(90f, 0f, 0f));
                    break;
                case Toy.Grenade: Shapes.Sphere(b, Vector3.zero, Vector3.one * 0.26f, new Color(0.25f, 0.34f, 0.2f)); break;
                case Toy.Molotov:
                    Shapes.Cylinder(b, Vector3.zero, new Vector3(0.15f, 0.34f, 0.15f), new Color(0.75f, 0.5f, 0.15f), 0.2f);
                    Shapes.Cone(b, Vector3.up * 0.28f, new Vector3(0.12f, 0.3f, 0.12f), new Color(1f, 0.55f, 0.1f));
                    break;
                case Toy.Frost: Shapes.Sphere(b, Vector3.zero, Vector3.one * 0.34f, new Color(0.6f, 0.9f, 1f), 0.6f); break;
                case Toy.Cluster:
                    Shapes.Sphere(b, Vector3.zero, Vector3.one * 0.4f, new Color(0.55f, 0.1f, 0.1f));
                    Shapes.Sphere(b, new Vector3(0.14f, 0.1f, 0f), Vector3.one * 0.18f, new Color(0.9f, 0.7f, 0.2f));
                    break;
                case Toy.Dynamite:
                    for (int i = -1; i <= 1; i++) Shapes.Cylinder(b, new Vector3(i * 0.13f, 0f, 0f), new Vector3(0.11f, 0.5f, 0.11f), new Color(0.8f, 0.15f, 0.12f));
                    Shapes.Box(b, Vector3.zero, new Vector3(0.5f, 0.06f, 0.06f), new Color(0.15f, 0.12f, 0.1f));
                    break;
                case Toy.Tire:
                    if (Kit.Place("Wheel", Vector3.zero, 0f, 1f, b, true) == null) Shapes.Cylinder(b, Vector3.zero, new Vector3(0.9f, 0.3f, 0.9f), new Color(0.08f, 0.08f, 0.09f));
                    break;
                case Toy.Hydrant:
                    if (Kit.Place("FireHydrant", Vector3.zero, 0f, 0.8f, b, true) == null) Shapes.Cylinder(b, Vector3.zero, new Vector3(0.35f, 0.8f, 0.35f), new Color(0.8f, 0.15f, 0.12f));
                    break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (rolling) { Roll(dt); return; }
            if (landed)
            {
                // dynamite sits there with its fuse burning
                fuse -= dt;
                trailT -= dt;
                if (trailT <= 0f) { trailT = 0.06f; Fx.I.Sparks(transform.position + Vector3.up * 0.4f); }
                if (fuse <= 0f)
                {
                    if (sim) Boom.Explode(transform.position + Vector3.up * 0.3f, 4.2f, 150f * power, 13f, true);
                    Destroy(gameObject);
                }
                return;
            }
            t += dt / flight;
            var pos = Vector3.Lerp(from, to, Mathf.Min(t, 1f)) + Vector3.up * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * arc;
            var v = pos - transform.position;
            transform.position = pos;
            if (kind == Toy.Rocket)
            {
                if (v.sqrMagnitude > 0.0001f) body.rotation = Quaternion.LookRotation(v);
                trailT -= dt;
                if (trailT <= 0f) { trailT = 0.035f; Fx.I.Smoke(pos - v.normalized * 0.5f, 0.5f); Fx.I.Burst(pos - v.normalized * 0.4f, 1, new Color(1f, 0.7f, 0.2f), 1f, 0.12f); }
            }
            else body.Rotate(new Vector3(380f, 120f, 60f) * dt);
            if (t >= 1f) Land();
        }

        void Land()
        {
            var at = new Vector3(to.x, Mathf.Max(0f, to.y), to.z);
            transform.position = at;
            switch (kind)
            {
                case Toy.Rocket:
                    if (sim) Boom.Explode(at + Vector3.up * 0.3f, 3.6f * Mathf.Min(power, 1.6f), 120f * power, 12f, true);
                    Destroy(gameObject);
                    break;
                case Toy.Grenade:
                    if (sim) Boom.Explode(at + Vector3.up * 0.3f, 3.1f, 85f * power, 10f, false);
                    Destroy(gameObject);
                    break;
                case Toy.Cluster:
                    if (sim)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            var q = at + new Vector3(Random.Range(-2.2f, 2.2f), 0.3f, Random.Range(-2.2f, 2.2f));
                            Game.I.Delay(0.1f + i * 0.13f, () => Boom.Explode(q, 2.1f, 55f * power, 8f, false));
                        }
                    }
                    Destroy(gameObject);
                    break;
                case Toy.Molotov:
                    Fx.I.Burst(at + Vector3.up * 0.3f, 26, new Color(1f, 0.55f, 0.15f), 5f, 0.14f);
                    SoundBank.I.Play(SoundBank.I.smash, 0.5f, 1.5f);
                    FirePatch.Make(at, sim, power);
                    Destroy(gameObject);
                    break;
                case Toy.Frost:
                    Fx.I.Burst(at + Vector3.up * 0.5f, 40, new Color(0.6f, 0.92f, 1f), 5f, 0.13f);
                    Fx.I.Smoke(at + Vector3.up * 0.6f, 2.6f);
                    SoundBank.I.Play(SoundBank.I.clang, 0.5f, 1.6f);
                    if (sim)
                        foreach (var z in Toys.Near(at, 4.2f).ToArray()) { z.Slow(0.35f, 5f); z.Hit(8f * power, Vector3.forward, z.Center, false, 0f); }
                    Destroy(gameObject);
                    break;
                case Toy.Dynamite:
                    landed = true; fuse = 1.3f;
                    break;
                case Toy.Tire:
                    rolling = true; rollT = 2.8f;
                    body.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f);   // upright, rolling along the street
                    transform.position = at + Vector3.up * 0.45f;
                    break;
                case Toy.Hydrant:
                    Fx.I.Burst(at + Vector3.up * 0.4f, 44, new Color(0.7f, 0.9f, 1f), 7f, 0.12f);
                    Fx.I.Smoke(at + Vector3.up * 0.8f, 2f);
                    SoundBank.I.Play(SoundBank.I.smash, 0.7f, 0.8f);
                    CameraRig.I.Shake(0.25f);
                    if (sim)
                        foreach (var z in Toys.Near(at, 3.6f).ToArray()) { z.Blast(at, 3.6f, 30f * power, 7f); z.Slow(0.6f, 2.5f); }
                    Destroy(gameObject);
                    break;
            }
        }

        /// The tyre rolls up the street into the horde, knocking zombies about.
        void Roll(float dt)
        {
            rollT -= dt;
            var p = transform.position + dir * 10f * dt;
            p.x = Mathf.Clamp(p.x, -Arena.HalfWidth + 0.4f, Arena.HalfWidth - 0.4f);
            transform.position = p;
            body.Rotate(0f, 0f, -600f * dt, Space.Self);
            trailT -= dt;
            if (trailT <= 0f) { trailT = 0.08f; Fx.I.Dust(new Vector3(p.x, 0.1f, p.z), 0.5f); }
            hitT -= dt;
            if (sim && hitT <= 0f)
            {
                hitT = 0.1f;
                foreach (var z in Toys.Near(p, 1.25f).ToArray())
                {
                    float last;
                    if (touched.TryGetValue(z, out last) && Time.time - last < 0.7f) continue;
                    touched[z] = Time.time;
                    z.Blast(p, 1.6f, 45f * power, 6f);
                    SoundBank.I.Play(SoundBank.I.smash, 0.35f, Random.Range(1f, 1.4f));
                }
            }
            if (rollT <= 0f || p.z > Arena.SpawnZ - 4f) Destroy(gameObject);
        }
    }

    /// A pool of burning fuel: zombies standing in it catch fire.
    public class FirePatch : MonoBehaviour
    {
        const float Radius = 2.5f, Life = 5.5f;
        float t, tick;
        bool sim;
        float power;
        Vector3 c;

        public static void Make(Vector3 at, bool sim, float power)
        {
            var go = new GameObject("Fire patch");
            go.transform.position = at;
            var f = go.AddComponent<FirePatch>();
            f.sim = sim; f.c = at; f.power = power;
            for (int i = 0; i < 6; i++)
            {
                var a = i * 1.047f;
                var pos = at + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Radius * 0.6f);
                Flame.Make(go.transform, pos, i == 0 ? 1.4f : 0.9f, false, i % 3 == 0);
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (sim)
            {
                tick -= Time.deltaTime;
                if (tick <= 0f)
                {
                    tick = 0.25f;
                    foreach (var z in Toys.Near(c, Radius).ToArray()) z.Burn(26f * power, 1.8f);
                }
            }
            if (t > Life) Destroy(gameObject);
        }
    }

    /// The buddies help a little: whoever the player is not controlling throws or fires something at the
    /// densest bunch of zombies every so often (Lis: rocket launcher, Sam: a different thing each time,
    /// Shaun, when the player is up on a tower: grenades and fire). Only the simulation decides.
    public class Squad : MonoBehaviour
    {
        Shooter sh;
        float t;
        Transform launcher;

        static readonly Toy[] SamPool = { Toy.Grenade, Toy.Molotov, Toy.Frost, Toy.Tire, Toy.Cluster, Toy.Dynamite, Toy.Hydrant };
        static readonly int[] SamFrom = { 1, 2, 3, 4, 5, 6, 7 };
        static readonly Toy[] ShaunPool = { Toy.Grenade, Toy.Molotov, Toy.Dynamite };
        static readonly int[] ShaunFrom = { 1, 2, 6 };
        static readonly Color[] Tint =
        {
            new Color(1f, 0.55f, 0.3f), new Color(0.6f, 0.95f, 0.4f), new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.9f, 1f),
            new Color(1f, 0.4f, 0.4f), new Color(1f, 0.35f, 0.3f), new Color(0.8f, 0.8f, 0.85f), new Color(0.5f, 0.8f, 1f)
        };

        public static void Attach(Shooter s)
        {
            var q = s.gameObject.AddComponent<Squad>();
            q.sh = s;
            q.t = 5f + s.Slot * 2.5f;
            if (s.Slot == 0)
            {
                // the bazooka over Lis's shoulder
                var yaw = s.YawTransform;
                q.launcher = new GameObject("Bazooka").transform;
                q.launcher.SetParent(yaw, false);
                q.launcher.localPosition = new Vector3(0.28f, 1.42f, 0.25f);
                Shapes.Make("Tube", MeshGen.Cylinder(), Mat.Lit(new Color(0.36f, 0.4f, 0.26f)), q.launcher, Vector3.zero, new Vector3(0.2f, 0.62f, 0.2f), Quaternion.Euler(90f, 0f, 0f));
                Shapes.Make("Rim", MeshGen.Cylinder(), Mat.Lit(new Color(0.15f, 0.15f, 0.16f)), q.launcher, new Vector3(0f, 0f, 0.62f), new Vector3(0.25f, 0.06f, 0.25f), Quaternion.Euler(90f, 0f, 0f));
                Shapes.Make("Rim", MeshGen.Cylinder(), Mat.Lit(new Color(0.15f, 0.15f, 0.16f)), q.launcher, new Vector3(0f, 0f, -0.62f), new Vector3(0.25f, 0.06f, 0.25f), Quaternion.Euler(90f, 0f, 0f));
            }
        }

        /// A new level: everybody starts with a short wait.
        public static void ResetAll()
        {
            foreach (var s in Shooter.Slots) if (s != null) { var q = s.GetComponent<Squad>(); if (q != null) q.t = 6f + s.Slot * 2.5f; }
        }

        float Interval()
        {
            int lv = Game.I != null ? Game.I.LevelNumber : 1;
            float baseT = sh.Slot == 0 ? 15f : sh.Slot == 2 ? 11f : 20f;
            float k = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01((lv - 1) / 20f));
            return baseT * k * Random.Range(0.8f, 1.2f);
        }

        void Update()
        {
            bool helping = sh.Ctl == Control.AI && Game.I != null && Game.I.Playing && !Net.IsClient;
            if (launcher != null) launcher.gameObject.SetActive(sh.Ctl == Control.AI);
            if (!helping) return;
            t -= Time.deltaTime;
            if (t > 0f) return;

            Vector3 target;
            if (!PickTarget(out target)) { t = 1.5f; return; }
            int lv = Game.I.LevelNumber;
            Toy kind = Pick(lv);
            var from = sh.MuzzlePos + Vector3.up * 0.4f;
            if (kind == Toy.Tire) target = new Vector3(Mathf.Clamp(target.x, -Arena.HalfWidth + 1f, Arena.HalfWidth - 1f), 0f, Mathf.Max(Arena.WallFront + 4f, target.z - 4f));
            if (kind == Toy.Hydrant) target.z = Mathf.Min(target.z, 10f);
            target.y = 0.2f;
            float power = 1f + 0.05f * Mathf.Min(lv - 1, 20);
            Toys.Launch(kind, from, target, true, power);
            Stats.Add(Ev.Toy);
            Coach.Tip(Coach.TipSquad, "YOUR BUDDIES HELP!", "The shooters you are not controlling throw things:\nLis fires a bazooka, Sam throws bombs, fire and more.", 8f);
            Game.Pop(from + Vector3.up * 1.1f, Toys.Names[(int)kind], Tint[(int)kind], 0.9f);
            t = Interval();
        }

        Toy Pick(int level)
        {
            if (sh.Slot == 0) return Toy.Rocket;
            var pool = sh.Slot == 2 ? SamPool : ShaunPool;
            var from = sh.Slot == 2 ? SamFrom : ShaunFrom;
            int n = 0;
            for (int i = 0; i < pool.Length; i++) if (from[i] <= level) n++;
            int pick = Random.Range(0, Mathf.Max(1, n));
            for (int i = 0; i < pool.Length; i++) if (from[i] <= level && pick-- == 0) return pool[i];
            return pool[0];
        }

        /// The middle of the densest bunch in reach (heavy zombies and the top of the pile count extra).
        bool PickTarget(out Vector3 pos)
        {
            pos = Vector3.zero;
            float best = 0f;
            var list = Zombie.Alive;
            float minZ = Arena.WallFront + 2f, maxZ = 42f;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || a.Puppet || !a.IsAlive) continue;
                var ap = a.transform.position;
                if (ap.z < minZ - 1.5f || ap.z > maxZ) continue;
                float score = a.IsBrute ? 3f : 0.5f;
                if (a.State == ZState.Pile || a.State == ZState.Climb) score += 1.5f;
                var sum = ap; int n = 1;
                for (int j = 0; j < list.Count; j++)
                {
                    var b = list[j];
                    if (j == i || b == null || b.Puppet || !b.IsAlive) continue;
                    var bp = b.transform.position;
                    float dx = bp.x - ap.x, dz = bp.z - ap.z;
                    if (dx * dx + dz * dz > 9f) continue;
                    score += b.IsBrute ? 2f : 1f; sum += bp; n++;
                }
                if (score > best) { best = score; pos = sum / n; }
            }
            if (best < 2.5f) return false;        // not worth it yet: a lone walker is the gun's job
            pos.z = Mathf.Max(pos.z, minZ);
            return true;
        }
    }
}
