using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// The skydiver: free-fall / canopy physics plus the procedural body and parachute.
    public class Jumper : MonoBehaviour
    {
        public const float Terminal = 42f;       // free-fall speed (m/s)
        public const float CanopySink = 7f;      // sink speed with an open canopy near the ground
        public const float SafeLanding = 12f;    // touch down faster than this and you crash
        public const float Radius = 0.7f;

        public Vector3 hVel;        // horizontal velocity
        public float vFall;         // downward speed (positive)
        public bool deploying, deployed;
        public float deployT, deployDuration;
        float deployV0;
        public float tumbleT;
        public int shields;

        Transform tilt, pose, armL, armR, legL, legR, canopy, bubble;
        readonly List<Transform> lines = new List<Transform>();
        readonly List<Transform> parts = new List<Transform>();
        TrailRenderer trailL, trailR;
        Material trailMat;
        Vector3 spin;
        float poseBlend = 0f; // 0 = standing, 1 = free-fall
        float deployVisual;

        public bool Tumbling { get { return tumbleT > 0f; } }
        public float DeployProgress { get { return deployDuration > 0f ? Mathf.Clamp01(deployT / deployDuration) : 0f; } }

        /// Altitude needed to fully open the canopy at the current fall speed.
        public float HMin { get { return Economy.DeployTime * (vFall + CanopySink) * 0.5f; } }

        public static Jumper Create(Transform parent, Vector3 pos, SkinDef skin)
        {
            var go = new GameObject("Jumper");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var j = go.AddComponent<Jumper>();
            j.Build(skin);
            return j;
        }

        void Build(SkinDef skin)
        {
            tilt = Shapes.Group("Tilt", transform, Vector3.zero).transform;
            pose = Shapes.Group("Pose", tilt, Vector3.zero).transform;
            var suit = Mat.Lit(skin.suit);
            var suit2 = Mat.Lit(skin.suit2);
            var helmet = Mat.Lit(skin.helmet);
            var skinTone = Mat.Lit(new Color(0.96f, 0.78f, 0.62f));
            var dark = Mat.Lit(new Color(0.12f, 0.12f, 0.16f), 0.2f);

            Part("Torso", MeshGen.Box(), suit, pose, new Vector3(0f, 0.25f, 0f), new Vector3(0.72f, 0.9f, 0.42f));
            Part("Stripe", MeshGen.Box(), suit2, pose, new Vector3(0f, 0.42f, 0f), new Vector3(0.74f, 0.16f, 0.44f));
            Part("Pack", MeshGen.Box(), suit2, pose, new Vector3(0f, 0.3f, -0.3f), new Vector3(0.58f, 0.7f, 0.26f));
            Part("Face", MeshGen.Sphere(1), skinTone, pose, new Vector3(0f, 0.95f, 0.04f), Vector3.one * 0.5f);
            Part("Helmet", MeshGen.Sphere(1), helmet, pose, new Vector3(0f, 1.0f, -0.03f), new Vector3(0.58f, 0.56f, 0.58f));
            Part("Visor", MeshGen.Box(), dark, pose, new Vector3(0f, 0.97f, 0.24f), new Vector3(0.4f, 0.16f, 0.12f));

            armL = Limb("ArmL", pose, new Vector3(-0.46f, 0.6f, 0f), 0.75f, 0.2f, suit, skinTone);
            armR = Limb("ArmR", pose, new Vector3(0.46f, 0.6f, 0f), 0.75f, 0.2f, suit, skinTone);
            legL = Limb("LegL", pose, new Vector3(-0.2f, -0.2f, 0f), 0.9f, 0.26f, suit2, dark);
            legR = Limb("LegR", pose, new Vector3(0.2f, -0.2f, 0f), 0.9f, 0.26f, suit2, dark);

            // Parachute: 7 cells in an arc + lines to the shoulders.
            canopy = Shapes.Group("Canopy", transform, new Vector3(0f, 5.2f, 0f)).transform;
            var ca = Mat.Lit(skin.canopyA);
            var cb = Mat.Lit(skin.canopyB);
            for (int i = 0; i < 7; i++)
            {
                float th = (i - 3) * 9f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(th) * 6.2f, Mathf.Cos(th) * 6.2f - 6.2f, 0f);
                Shapes.Make("Cell", MeshGen.Box(), i % 2 == 0 ? ca : cb, canopy, pos, new Vector3(1.02f, 0.5f, 2.6f),
                    Quaternion.Euler(0f, 0f, -(i - 3) * 9f));
            }
            var lineMat = Mat.Lit(new Color(0.9f, 0.9f, 0.9f));
            Vector3[] tops = { new Vector3(-3f, 4.4f, 1f), new Vector3(3f, 4.4f, 1f), new Vector3(-3f, 4.4f, -1f), new Vector3(3f, 4.4f, -1f) };
            for (int i = 0; i < tops.Length; i++)
            {
                Vector3 from = new Vector3(tops[i].x > 0 ? 0.35f : -0.35f, 0.6f, 0f);
                Vector3 to = tops[i];
                var l = Shapes.Make("Line", MeshGen.Box(), lineMat, transform, (from + to) * 0.5f, new Vector3(0.04f, (to - from).magnitude, 0.04f));
                l.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
                lines.Add(l.transform);
            }
            SetCanopyVisible(0f);

            bubble = Shapes.Make("Shield", MeshGen.Sphere(1), Mat.Clear(new Color(0.4f, 1f, 0.85f, 0.18f), 0f), transform, new Vector3(0f, 0.3f, 0f), Vector3.one * 2.8f).transform;

            // Wingtip trails from the hands for a sense of speed.
            trailMat = Mat.UniqueClear(new Color(1f, 1f, 1f, 0.45f), 0f);
            trailL = Trail(armL);
            trailR = Trail(armR);
        }

        TrailRenderer Trail(Transform arm)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(arm, false);
            go.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = trailMat;
            t.time = 0.35f;
            t.startWidth = 0.18f;
            t.endWidth = 0f;
            t.minVertexDistance = 0.3f;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.emitting = false;
            return t;
        }

        void Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale)
        {
            parts.Add(Shapes.Make(name, mesh, mat, parent, pos, scale).transform);
        }

        Transform Limb(string name, Transform parent, Vector3 pivot, float length, float width, Material mat, Material endMat)
        {
            var p = Shapes.Group(name, parent, pivot).transform;
            Part(name + "Seg", MeshGen.Box(), mat, p, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, width));
            Part(name + "End", MeshGen.Sphere(0), endMat, p, new Vector3(0f, -length - 0.05f, 0f), Vector3.one * (width * 1.15f));
            return p;
        }

        void SetCanopyVisible(float amount)
        {
            deployVisual = amount;
            bool on = amount > 0.01f;
            canopy.gameObject.SetActive(on);
            if (on)
            {
                float overshoot = 1f + Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI) * 0.18f;
                float s = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(amount)) * overshoot;
                canopy.localScale = new Vector3(s, Mathf.Lerp(0.2f, 1f, amount), s);
                canopy.localPosition = new Vector3(0f, Mathf.Lerp(1.5f, 5.2f, amount), 0f);
            }
            for (int i = 0; i < lines.Count; i++) lines[i].gameObject.SetActive(amount > 0.6f);
        }

        // ------------------------------------------------------------------------ physics

        public void BeginFall()
        {
            vFall = 6f;
            hVel = new Vector3(0f, 0f, 5f);
            shields = Economy.Shields;
        }

        public void TickFreefall(Vector2 input, float dt)
        {
            if (tumbleT > 0f)
            {
                tumbleT -= dt;
                input = Vector2.zero;
            }
            Vector3 want = new Vector3(input.x, 0f, input.y) * Economy.FreefallSteer;
            float accel = input.sqrMagnitude > 0.01f ? 42f : 16f;
            hVel = Vector3.MoveTowards(hVel, want, accel * dt);
            float rate = vFall > Terminal ? 6f : 22f;
            vFall = Mathf.MoveTowards(vFall, Terminal, rate * dt);
            transform.position += (hVel + Vector3.down * vFall) * dt;
        }

        public void Boost(float amount)
        {
            vFall = Mathf.Min(vFall + amount, Terminal + 20f);
        }

        public void StartDeploy()
        {
            deploying = true;
            deployT = 0f;
            deployV0 = vFall;
            deployDuration = Economy.DeployTime;
        }

        /// Sink speed under canopy: faster when high so a safe early open doesn't drag on forever.
        public static float SinkAt(float altitude)
        {
            return Mathf.Lerp(CanopySink, 13f, Mathf.InverseLerp(30f, 120f, altitude));
        }

        public void TickCanopy(Vector2 input, Vector3 wind, float altitude, float dt)
        {
            if (tumbleT > 0f)
            {
                tumbleT -= dt;
                input *= 0.2f;
            }
            float sink = SinkAt(altitude);
            if (deploying && !deployed)
            {
                deployT += dt;
                float s = Mathf.Clamp01(deployT / deployDuration);
                s = s * s * (3f - 2f * s);
                vFall = Mathf.Lerp(deployV0, sink, s);
                if (deployT >= deployDuration) deployed = true;
            }
            else vFall = Mathf.MoveTowards(vFall, sink, 10f * dt);

            float control = deployed ? 1f : 0.35f;
            Vector3 want = new Vector3(input.x, 0f, input.y) * Economy.CanopySteer * control + wind * Economy.WindFactor;
            hVel = Vector3.MoveTowards(hVel, want, (deployed ? 12f : 20f) * dt);
            transform.position += (hVel + Vector3.down * vFall) * dt;
        }

        public void Tumble(Vector3 away, float strength)
        {
            tumbleT = deployed ? 0.4f : 0.9f;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            hVel = away.normalized * strength;
            if (!deploying) vFall *= 0.8f;
            spin = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * 900f;
        }

        // ------------------------------------------------------------------------ visuals

        public void SetShieldVisible(bool on) { if (bubble != null) bubble.gameObject.SetActive(on); }

        public void SetTrail(bool on, Color c)
        {
            trailL.emitting = on;
            trailR.emitting = on;
            trailMat.SetColor("_Color", c);
        }

        /// mode: 0 idle (standing), 1 free fall, 2 canopy, 3 landed
        public void Animate(int mode, Vector2 steer, float time, float dt)
        {
            float targetBlend = mode == 1 ? 1f : 0f;
            poseBlend = Mathf.MoveTowards(poseBlend, targetBlend, dt * 3f);
            float e = poseBlend * poseBlend * (3f - 2f * poseBlend);

            if (tumbleT > 0f)
            {
                tilt.Rotate(spin * dt, Space.World);
            }
            else
            {
                Quaternion bank;
                if (mode == 1) bank = Quaternion.Euler(steer.y * 18f, 0f, -steer.x * 28f);
                else if (mode == 2) bank = Quaternion.Euler(steer.y * 8f, 0f, -steer.x * 14f);
                else bank = Quaternion.identity;
                tilt.localRotation = Quaternion.Slerp(tilt.localRotation, bank, dt * 8f);
            }
            pose.localRotation = Quaternion.Euler(90f * e, 0f, 0f);

            float flutter = Mathf.Sin(time * 30f) * 4f * e;
            float armFree = -100f + flutter, armCanopy = -155f, armIdle = -12f;
            float legFree = 22f + flutter * 0.5f;
            float a, l;
            if (mode == 2 || deployVisual > 0.5f)
            {
                a = Mathf.Lerp(armCanopy, armCanopy + steer.x * 25f, 0.5f);
                l = 6f + Mathf.Sin(time * 3f) * 5f;
            }
            else if (mode == 1) { a = Mathf.Lerp(armIdle, armFree, e); l = Mathf.Lerp(3f, legFree, e); }
            else if (mode == 3) { a = -40f + Mathf.Sin(time * 10f) * 60f; l = 4f; }
            else { a = armIdle + Mathf.Sin(time * 2f) * 3f; l = 3f; }
            armL.localRotation = Quaternion.Euler(0f, 0f, a);
            armR.localRotation = Quaternion.Euler(0f, 0f, -a);
            legL.localRotation = Quaternion.Euler(Mathf.Sin(time * 2f) * 4f * e, 0f, -l);
            legR.localRotation = Quaternion.Euler(-Mathf.Sin(time * 2f) * 4f * e, 0f, l);

            if (mode == 3)
            {
                // landed: the canopy collapses behind the jumper
                deploying = false;
                if (deployVisual > 0f) SetCanopyVisible(Mathf.MoveTowards(deployVisual, 0f, dt * 1.2f));
            }
            else if (deploying) SetCanopyVisible(Mathf.Clamp01(deployT / Mathf.Max(0.01f, deployDuration) * 1.1f));
            if (bubble != null && bubble.gameObject.activeSelf)
                bubble.localScale = Vector3.one * (2.8f + Mathf.Sin(time * 6f) * 0.1f);

            // Canopy sways and faces travel direction.
            if (deployed || deploying)
            {
                Vector3 flat = hVel;
                flat.y = 0f;
                if (flat.sqrMagnitude > 1f)
                {
                    var look = Quaternion.LookRotation(flat.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 2f);
                }
                canopy.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.3f) * 4f, 0f, -steer.x * 10f + Mathf.Sin(time * 1.7f) * 3f);
            }
        }

        /// Turn the body into flying debris (crash).
        public void Explode(Transform debrisParent, float groundY, Vector3 baseVel)
        {
            trailL.emitting = false;
            trailR.emitting = false;
            SetCanopyVisible(0f);
            if (bubble != null) bubble.gameObject.SetActive(false);
            foreach (var p in parts)
            {
                if (p == null) continue;
                Vector3 wp = p.position;
                Quaternion wr = p.rotation;
                Vector3 ws = p.lossyScale;
                p.SetParent(debrisParent, true);
                p.position = wp;
                p.rotation = wr;
                p.localScale = ws;
                var d = p.gameObject.AddComponent<Debris>();
                d.vel = baseVel * 0.3f + new Vector3(Random.Range(-8f, 8f), Random.Range(6f, 16f), Random.Range(-8f, 8f));
                d.angVel = Random.insideUnitSphere * 720f;
                d.ground = groundY + ws.y * 0.5f;
            }
        }
    }

    /// Simple ballistic debris with ground bounce (used for the crash "ragdoll").
    public class Debris : MonoBehaviour
    {
        public Vector3 vel, angVel;
        public float ground;

        void Update()
        {
            float dt = Time.deltaTime;
            vel.y -= 28f * dt;
            transform.position += vel * dt;
            transform.Rotate(angVel * dt, Space.World);
            if (transform.position.y < ground)
            {
                var p = transform.position;
                p.y = ground;
                transform.position = p;
                vel.y = -vel.y * 0.35f;
                vel.x *= 0.6f;
                vel.z *= 0.6f;
                angVel *= 0.6f;
                if (Mathf.Abs(vel.y) < 1f) { vel = Vector3.zero; angVel = Vector3.zero; enabled = false; }
            }
        }
    }
}
