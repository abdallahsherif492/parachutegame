using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// A physics zombie. Alive: an upright box that shuffles toward the wall and climbs over
    /// whatever is in front of it (other zombies, dead bodies), so a pile builds up against the wall.
    /// Dead: the rotation is freed and it tumbles like a sack; the body stays in the pile for a while.
    public class Zombie : MonoBehaviour
    {
        public static readonly List<Zombie> Alive = new List<Zombie>();
        static readonly Color[] Skins = { new Color(0.56f, 0.74f, 0.42f), new Color(0.47f, 0.66f, 0.47f), new Color(0.62f, 0.7f, 0.38f), new Color(0.5f, 0.6f, 0.55f) };
        static readonly Color[] Shirts = { new Color(0.75f, 0.3f, 0.28f), new Color(0.3f, 0.45f, 0.7f), new Color(0.85f, 0.75f, 0.5f), new Color(0.45f, 0.55f, 0.35f), new Color(0.6f, 0.4f, 0.65f), new Color(0.9f, 0.9f, 0.88f) };
        static readonly Color[] Pants = { new Color(0.2f, 0.25f, 0.4f), new Color(0.35f, 0.28f, 0.22f), new Color(0.25f, 0.25f, 0.27f) };

        public bool IsAlive { get; private set; }
        public bool IsBrute { get; private set; }
        public float Hp { get; private set; }
        public Collider HeadCollider { get; private set; }

        ZombieModels.Rig model;
        Rigidbody rb;
        BoxCollider body;
        Transform vis, head, legL, legR, armL, armR;
        float baseScale = 1f;
        float speed, climb, animT, deadT, punch, groanT, steerX, stuckT;
        bool touching, breaching;
        Vector3 breachFrom;
        float breachT;

        public static Zombie Spawn(Vector3 pos, float hp, float speed, bool brute)
        {
            var go = new GameObject(brute ? "Brute" : "Zombie");
            go.transform.position = pos;
            var z = go.AddComponent<Zombie>();
            z.Init(hp, speed, brute);
            return z;
        }

        void Init(float hp, float spd, bool brute)
        {
            IsBrute = brute;
            float s = brute ? 1.9f : Random.Range(1.05f, 1.2f);
            Hp = hp * (brute ? 12f : 1f);
            speed = spd * (brute ? 0.55f : Random.Range(0.8f, 1.25f));
            climb = brute ? 4f : Random.Range(4.6f, 5.6f);
            steerX = Random.Range(-1f, 1f);
            groanT = Random.Range(1f, 6f);
            animT = Random.value * 10f;

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = brute ? 6f : 1f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            body = gameObject.AddComponent<BoxCollider>();
            body.size = new Vector3(0.72f, 1.62f, 0.56f) * s;
            body.center = Vector3.zero;

            // ---- looks (built from smooth low-poly shapes)
            var skin = Skins[Random.Range(0, Skins.Length)] * (brute ? 0.85f : 1f);
            var shirt = Shirts[Random.Range(0, Shirts.Length)];
            var pants = Pants[Random.Range(0, Pants.Length)];
            vis = Shapes.Group("Vis", transform, Vector3.zero).transform;
            vis.localScale = Vector3.one * s;
            baseScale = s;
            Shapes.Make("Torso", MeshGen.Capsule(0.42f), Mat.Lit(shirt), vis, new Vector3(0, 0.12f, 0), new Vector3(0.74f, 1f, 0.56f));
            Shapes.Make("Rip", MeshGen.Capsule(0.3f), Mat.Lit(skin), vis, new Vector3(0.15f, 0.25f, -0.24f), new Vector3(0.22f, 0.3f, 0.12f));
            Shapes.Box(vis, new Vector3(0, -0.36f, 0), new Vector3(0.66f, 0.34f, 0.5f), pants);
            legL = Limb(vis, new Vector3(-0.17f, -0.45f, 0), new Vector3(0.26f, 0.42f, 0.26f), pants, skin);
            legR = Limb(vis, new Vector3(0.17f, -0.45f, 0), new Vector3(0.26f, 0.42f, 0.26f), pants, skin);
            armL = Limb(vis, new Vector3(-0.43f, 0.48f, 0), new Vector3(0.2f, 0.62f, 0.2f), skin, skin);
            armR = Limb(vis, new Vector3(0.43f, 0.48f, 0), new Vector3(0.2f, 0.62f, 0.2f), skin, skin);

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(transform, false);
            headGo.transform.localPosition = new Vector3(0, 1.02f * s, 0);
            head = headGo.transform;
            var hc = headGo.AddComponent<SphereCollider>();
            hc.radius = 0.27f * s;
            HeadCollider = hc;
            var hv = Shapes.Group("HeadVis", head, Vector3.zero).transform;
            hv.localScale = Vector3.one * s;
            Shapes.Make("Skull", MeshGen.Capsule(0.46f), Mat.Lit(skin), hv, Vector3.zero, new Vector3(0.52f, 0.58f, 0.5f));
            var eye = brute ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.92f, 0.35f);
            Shapes.Sphere(hv, new Vector3(-0.11f, 0.06f, -0.22f), new Vector3(0.12f, 0.1f, 0.06f), eye, 1f);
            Shapes.Sphere(hv, new Vector3(0.11f, 0.04f, -0.22f), new Vector3(0.1f, 0.12f, 0.06f), eye, 1f);
            Shapes.Box(hv, new Vector3(0, -0.14f, -0.23f), new Vector3(0.2f, 0.07f, 0.04f), new Color(0.2f, 0.08f, 0.08f));
            Shapes.Make("Hair", MeshGen.Capsule(0.46f), Mat.Lit(new Color(0.2f, 0.17f, 0.15f)), hv, new Vector3(0, 0.2f, 0.05f), new Vector3(0.5f, 0.25f, 0.46f));
            if (brute)
                Shapes.Make("Jaw", MeshGen.Capsule(0.4f), Mat.Lit(skin * 0.9f), hv, new Vector3(0, -0.2f, -0.05f), new Vector3(0.5f, 0.25f, 0.45f));

            model = ZombieModels.Spawn(transform, body.size.y + 0.3f * s);
            if (model != null)
            {
                // the imported model replaces the low-poly body and head
                vis.gameObject.SetActive(false);
                hv.gameObject.SetActive(false);
                if (brute) model.root.localScale *= 1.05f;
            }

            IsAlive = true;
            Alive.Add(this);
        }

        static Transform Limb(Transform parent, Vector3 pivot, Vector3 size, Color top, Color bottom)
        {
            var p = Shapes.Group("Pivot", parent, pivot).transform;
            Shapes.Make("Upper", MeshGen.Capsule(0.45f), Mat.Lit(top), p, new Vector3(0, -size.y * 0.3f, 0), new Vector3(size.x, size.y * 0.6f, size.z));
            Shapes.Make("Lower", MeshGen.Capsule(0.45f), Mat.Lit(bottom), p, new Vector3(0, -size.y * 0.75f, 0), new Vector3(size.x * 0.9f, size.y * 0.5f, size.z * 0.9f));
            return p;
        }

        Vector3 Velocity { get { return rb.GetPointVelocity(rb.worldCenterOfMass); } }

        void FixedUpdate()
        {
            if (!IsAlive || breaching) { touching = false; return; }
            var v = Velocity;
            var pos = transform.position;
            float half = body.size.z * 0.5f;

            // shuffle toward the gate; spread across it a little so the pile has a shape
            float targetX = Mathf.Clamp(steerX * 1.8f, -Arena.HalfWidth + 0.8f, Arena.HalfWidth - 0.8f);
            var want = new Vector3((targetX - pos.x) * 0.6f, 0f, -speed);
            var dv = new Vector3(want.x - v.x, 0f, want.z - v.z);
            dv = Vector3.ClampMagnitude(dv, 30f * Time.fixedDeltaTime);
            rb.AddForce(dv, ForceMode.VelocityChange);

            // blocked by a zombie or a body in front: clamber up over it
            bool atWall = pos.z <= Arena.WallFront + half + 0.08f;
            RaycastHit hit;
            var eye = pos + Vector3.up * body.size.y * 0.15f;
            bool blocked = Physics.Raycast(eye, Vector3.back, out hit, half + 0.45f, ~(1 << Arena.IgnoreRaycast), QueryTriggerInteraction.Ignore)
                           && hit.collider.GetComponentInParent<Zombie>() != null;
            if (!blocked) blocked = Physics.Raycast(pos - Vector3.up * body.size.y * 0.35f, Vector3.back, out hit, half + 0.35f, ~(1 << Arena.IgnoreRaycast), QueryTriggerInteraction.Ignore)
                                    && hit.collider.GetComponentInParent<Zombie>() != null;
            if (blocked && touching && v.y < climb)
                rb.AddForce(Vector3.up * Mathf.Min(climb - v.y, 60f * Time.fixedDeltaTime), ForceMode.VelocityChange);

            // unstick: pinned in place for a while -> a small hop
            if (v.sqrMagnitude < 0.05f && !atWall) { stuckT += Time.fixedDeltaTime; if (stuckT > 1.2f) { rb.AddForce(new Vector3(Random.Range(-1f, 1f), 4f, -1f), ForceMode.VelocityChange); stuckT = 0f; } }
            else stuckT = 0f;

            // the top of the pile reached the parapet: over the wall it goes
            float feet = pos.y - body.size.y * 0.5f;
            if (pos.z < Arena.WallFront + 1.3f && feet > Arena.WallHeight - 1.15f) StartBreach();
            touching = false;
        }

        void OnCollisionStay(Collision c) { touching = true; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsAlive && !breaching)
            {
                var v = Velocity;
                if (model != null) model.Move(new Vector2(v.x, v.z).magnitude + Mathf.Max(0f, v.y));
                animT += dt * (3f + new Vector2(v.x, v.z).magnitude * 2.6f);
                float swing = Mathf.Sin(animT) * 45f;
                vis.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(animT)) * 0.12f, 0f);
                vis.localRotation = Quaternion.Euler(-14f, 0f, Mathf.Sin(animT) * 6f);   // lunging run
                legL.localRotation = Quaternion.Euler(swing, 0, 0);
                legR.localRotation = Quaternion.Euler(-swing, 0, 0);
                // arms reach forward (toward the wall); up when climbing
                float reach = Mathf.Lerp(80f, 150f, Mathf.Clamp01(v.y / 3f));
                armL.localRotation = Quaternion.Euler(reach + Mathf.Sin(animT * 0.9f) * 10f, 0, -6f);
                armR.localRotation = Quaternion.Euler(reach + Mathf.Sin(animT * 0.9f + 2f) * 10f, 0, 6f);
                head.localRotation = Quaternion.Euler(Mathf.Sin(animT * 0.5f) * 8f, 0, Mathf.Sin(animT * 0.7f) * 12f);
                groanT -= dt;
                if (groanT < 0f) { groanT = Random.Range(4f, 12f); if (Random.value < 0.35f) SoundBank.I.Groan(); }
            }
            else if (breaching)
            {
                breachT += dt * 2.2f;
                var top = new Vector3(breachFrom.x, Arena.WallHeight + 1.2f, -0.2f);
                transform.position = Vector3.Lerp(breachFrom, top, Mathf.SmoothStep(0, 1, breachT)) + Vector3.up * Mathf.Sin(breachT * Mathf.PI) * 0.8f;
                armL.localRotation = armR.localRotation = Quaternion.Euler(160f, 0, 0);
                if (breachT >= 1f)
                {
                    Game.I.OnBreach(this);
                    Fx.I.Burst(transform.position, 14, new Color(0.45f, 0.7f, 0.3f), 5f);
                    Destroy(gameObject);
                }
            }
            else
            {
                deadT += dt;
                if (deadT > 5.5f)
                {
                    float k = 1f - (deadT - 5.5f) / 0.6f;
                    if (k <= 0f) { Destroy(gameObject); return; }
                    transform.localScale = Vector3.one * k;
                }
            }
            if (punch > 0f)
            {
                punch = Mathf.Max(0f, punch - dt * 6f);
                vis.localScale = Vector3.one * baseScale * (1f + punch * 0.25f);
            }
        }

        void StartBreach()
        {
            breaching = true;
            IsAlive = false;
            Alive.Remove(this);
            rb.isKinematic = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            breachFrom = transform.position;
        }

        /// Returns true if this hit killed the zombie.
        public bool Hit(float dmg, Vector3 dir, Vector3 point, bool headshot)
        {
            if (!IsAlive) { rb.AddForceAtPosition(dir.normalized * 1.5f, point, ForceMode.Impulse); return false; }
            Hp -= dmg;
            punch = 1f;
            Fx.I.Burst(point, headshot ? 10 : 5, new Color(0.35f, 0.75f, 0.25f), headshot ? 5f : 3f);
            rb.AddForce(dir.normalized * (IsBrute ? 0.4f : 0.8f), ForceMode.VelocityChange);
            if (Hp > 0f) return false;
            Die(dir * (IsBrute ? 4f : 7f) + Vector3.up * 3f, point, headshot);
            return true;
        }

        public void Die(Vector3 impulse, Vector3 point, bool popHead)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Alive.Remove(this);
            rb.constraints = RigidbodyConstraints.None;
            if (model != null)
            {
                model.Die();
                if (popHead && model.headBone != null) { model.headBone.localScale = Vector3.one * 0.01f; Fx.I.Burst(head.position, 16, new Color(0.35f, 0.75f, 0.25f), 6f); }
            }
            rb.AddForceAtPosition(impulse * rb.mass, point, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 6f * rb.mass, ForceMode.Impulse);
            armL.localRotation = Quaternion.Euler(170f, 0, -30f);
            armR.localRotation = Quaternion.Euler(170f, 0, 30f);
            if (popHead && !IsBrute && model == null)
            {
                // off it goes
                head.SetParent(null, true);
                var hb = head.gameObject.AddComponent<Rigidbody>();
                hb.mass = 0.2f;
                hb.AddForce(impulse * 0.12f + Vector3.up * 2.2f, ForceMode.Impulse);
                hb.AddTorque(Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
                Destroy(head.gameObject, 6f);
            }
        }

        /// Explosions: kill or fling everything in range.
        public void Blast(Vector3 center, float force, float radius, float dmg)
        {
            float d = Vector3.Distance(center, transform.position);
            float k = 1f - Mathf.Clamp01(d / radius);
            if (IsAlive)
            {
                Hp -= dmg * (0.4f + 0.6f * k);
                punch = 1f;
                if (Hp <= 0f)
                {
                    var dir = (transform.position - center).normalized;
                    Die((dir + Vector3.up * 0.8f) * force * (0.5f + k) / (IsBrute ? 3f : 1f), transform.position, false);
                    return;
                }
            }
            rb.AddExplosionForce(force * rb.mass * (IsAlive ? 0.4f : 1f), center, radius, 1.2f, ForceMode.Impulse);
        }

        void OnDestroy() { Alive.Remove(this); }
    }
}
