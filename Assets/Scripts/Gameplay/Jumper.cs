using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// The skydiver: free-fall / canopy physics plus visuals.
    /// Visuals are either an imported animated character (see CharacterModel) or a
    /// procedural rounded body with jointed limbs and spring-driven animation.
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

        // rig
        Transform tilt, pose, body, head, canopy, bubble;
        Transform shoulderL, shoulderR, elbowL, elbowR, hipL, hipR, kneeL, kneeR;
        readonly List<Transform> cells = new List<Transform>();
        readonly List<Vector3> cellScale = new List<Vector3>();
        readonly List<Transform> lines = new List<Transform>();
        readonly List<Transform> parts = new List<Transform>();
        CharacterModel model;
        TrailRenderer trailL, trailR;
        Material trailMat;

        // animation state
        Vector3 spin;
        float poseBlend;          // 0 = upright, 1 = belly-down free fall
        float deployVisual;
        Vector2 bank, bankVel;    // spring-driven lean
        float squash, squashVel;  // landing / opening squash & stretch
        float swing, swingVel;    // legs swinging under the canopy
        Vector3 lastHVel;
        Quaternion tiltRot = Quaternion.identity;
        int lastMode = -1;
        float modeTime;

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

        // ======================================================================== building

        void Build(SkinDef skin)
        {
            tilt = Shapes.Group("Tilt", transform, Vector3.zero).transform;
            pose = Shapes.Group("Pose", tilt, Vector3.zero).transform;
            body = Shapes.Group("Body", pose, Vector3.zero).transform;

            model = CharacterModel.TryCreate(body);
            if (model == null) BuildProceduralBody(skin);
            else
            {
                // hand anchors for trails / lines on an imported model
                shoulderL = Shapes.Group("HandL", body, new Vector3(-0.8f, 0.6f, 0f)).transform;
                shoulderR = Shapes.Group("HandR", body, new Vector3(0.8f, 0.6f, 0f)).transform;
            }

            BuildCanopy(skin);
            bubble = Shapes.Make("Shield", MeshGen.Smooth(), Mat.Clear(new Color(0.4f, 1f, 0.85f, 0.16f), 0f), transform,
                new Vector3(0f, 0.3f, 0f), Vector3.one * 2.8f).transform;

            trailMat = Mat.UniqueClear(new Color(1f, 1f, 1f, 0.45f), 0f);
            trailL = Trail(model == null ? elbowL : shoulderL, model == null ? -0.42f : 0f);
            trailR = Trail(model == null ? elbowR : shoulderR, model == null ? -0.42f : 0f);
        }

        void BuildProceduralBody(SkinDef skin)
        {
            var suit = Mat.Lit(skin.suit);
            var suit2 = Mat.Lit(skin.suit2);
            var helmet = Mat.Lit(skin.helmet);
            var skinTone = Mat.Lit(new Color(0.96f, 0.76f, 0.6f));
            var dark = Mat.Lit(new Color(0.13f, 0.13f, 0.17f));
            var lens = Mat.Lit(new Color(0.25f, 0.55f, 0.95f), 0.5f);
            var sphere = MeshGen.Smooth();

            Part("Pelvis", sphere, suit2, body, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.36f, 0.36f));
            Part("Torso", sphere, suit, body, new Vector3(0f, 0.38f, 0f), new Vector3(0.62f, 0.8f, 0.42f));
            Part("Band", sphere, suit2, body, new Vector3(0f, 0.5f, 0f), new Vector3(0.645f, 0.16f, 0.445f));
            Part("StrapL", MeshGen.Box(), dark, body, new Vector3(-0.14f, 0.36f, 0.2f), new Vector3(0.07f, 0.62f, 0.05f));
            Part("StrapR", MeshGen.Box(), dark, body, new Vector3(0.14f, 0.36f, 0.2f), new Vector3(0.07f, 0.62f, 0.05f));
            Part("Pack", sphere, suit2, body, new Vector3(0f, 0.42f, -0.24f), new Vector3(0.52f, 0.6f, 0.26f));
            Part("Neck", MeshGen.Capsule(0.3f), skinTone, body, new Vector3(0f, 0.8f, 0f), new Vector3(0.18f, 0.18f, 0.18f));

            head = Shapes.Group("Head", body, new Vector3(0f, 0.86f, 0f)).transform;
            Part("Face", sphere, skinTone, head, new Vector3(0f, 0.16f, 0.03f), Vector3.one * 0.42f);
            Part("Helmet", sphere, helmet, head, new Vector3(0f, 0.22f, -0.02f), new Vector3(0.49f, 0.44f, 0.5f));
            Part("Strap", sphere, dark, head, new Vector3(0f, 0.16f, 0f), new Vector3(0.47f, 0.09f, 0.46f));
            Part("LensL", sphere, lens, head, new Vector3(-0.09f, 0.16f, 0.2f), new Vector3(0.15f, 0.12f, 0.07f));
            Part("LensR", sphere, lens, head, new Vector3(0.09f, 0.16f, 0.2f), new Vector3(0.15f, 0.12f, 0.07f));

            shoulderL = Joint("ShoulderL", body, new Vector3(-0.35f, 0.64f, 0f));
            shoulderR = Joint("ShoulderR", body, new Vector3(0.35f, 0.64f, 0f));
            elbowL = Segment(shoulderL, "UpperArmL", 0.38f, 0.17f, suit);
            elbowR = Segment(shoulderR, "UpperArmR", 0.38f, 0.17f, suit);
            var handL = Segment(elbowL, "ForearmL", 0.36f, 0.15f, suit);
            var handR = Segment(elbowR, "ForearmR", 0.36f, 0.15f, suit);
            Part("GloveL", sphere, suit2, handL, Vector3.zero, Vector3.one * 0.17f);
            Part("GloveR", sphere, suit2, handR, Vector3.zero, Vector3.one * 0.17f);

            hipL = Joint("HipL", body, new Vector3(-0.14f, -0.1f, 0f));
            hipR = Joint("HipR", body, new Vector3(0.14f, -0.1f, 0f));
            kneeL = Segment(hipL, "ThighL", 0.46f, 0.22f, suit);
            kneeR = Segment(hipR, "ThighR", 0.46f, 0.22f, suit);
            var ankleL = Segment(kneeL, "ShinL", 0.44f, 0.19f, suit);
            var ankleR = Segment(kneeR, "ShinR", 0.44f, 0.19f, suit);
            Part("BootL", sphere, dark, ankleL, new Vector3(0f, -0.02f, 0.07f), new Vector3(0.2f, 0.15f, 0.32f));
            Part("BootR", sphere, dark, ankleR, new Vector3(0f, -0.02f, 0.07f), new Vector3(0.2f, 0.15f, 0.32f));
        }

        Transform Joint(string name, Transform parent, Vector3 pos)
        {
            return Shapes.Group(name, parent, pos).transform;
        }

        /// A rounded limb segment hanging down from 'joint'. Returns the joint at its far end.
        Transform Segment(Transform joint, string name, float length, float thickness, Material mat)
        {
            float r = Mathf.Clamp(thickness / (2f * length), 0.05f, 0.5f);
            float s = thickness / (2f * r);
            Part(name, MeshGen.Capsule(r), mat, joint, new Vector3(0f, -length * 0.5f, 0f), new Vector3(s, length + thickness * 0.3f, s));
            return Joint(name + "End", joint, new Vector3(0f, -length, 0f));
        }

        void Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale)
        {
            parts.Add(Shapes.Make(name, mesh, mat, parent, pos, scale).transform);
        }

        void BuildCanopy(SkinDef skin)
        {
            // Ram-air canopy: 9 pillowy cells on an arc, alternating colors, plus suspension lines.
            canopy = Shapes.Group("Canopy", transform, new Vector3(0f, 5.4f, 0f)).transform;
            var ca = Mat.Lit(skin.canopyA);
            var cb = Mat.Lit(skin.canopyB);
            var sphere = MeshGen.Smooth();
            const int n = 9;
            const float step = 8f, radius = 6.6f;
            for (int i = 0; i < n; i++)
            {
                float a = (i - (n - 1) * 0.5f) * step;
                float th = a * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(th) * radius, Mathf.Cos(th) * radius - radius, 0f);
                var sc = new Vector3(0.98f, 0.62f, 3.1f);
                var c = Shapes.Make("Cell", sphere, i % 2 == 0 ? ca : cb, canopy, pos, sc, Quaternion.Euler(0f, 0f, -a));
                cells.Add(c.transform);
                cellScale.Add(sc);
            }
            var lineMat = Mat.Lit(new Color(0.92f, 0.92f, 0.95f));
            float edge = Mathf.Sin((n - 1) * 0.5f * step * Mathf.Deg2Rad) * radius;
            float[] xs = { -edge, -edge * 0.45f, edge * 0.45f, edge };
            foreach (float x in xs)
                foreach (float z in new[] { 1.1f, -1.1f })
                {
                    Vector3 from = new Vector3(x < 0 ? -0.3f : 0.3f, 0.66f, 0f);
                    Vector3 to = new Vector3(x, 5.4f - 0.3f - (1f - Mathf.Cos(Mathf.Asin(Mathf.Clamp(x / radius, -1f, 1f)))) * radius, z);
                    var l = Shapes.Make("Line", MeshGen.Box(), lineMat, transform, (from + to) * 0.5f, new Vector3(0.025f, (to - from).magnitude, 0.025f));
                    l.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
                    lines.Add(l.transform);
                }
            SetCanopyVisible(0f, 0f);
        }

        TrailRenderer Trail(Transform anchor, float offset)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, offset, 0f);
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = trailMat;
            t.time = 0.35f;
            t.startWidth = 0.16f;
            t.endWidth = 0f;
            t.minVertexDistance = 0.3f;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.emitting = false;
            return t;
        }

        /// amount 0..1 = inflation. Cells pop open from the center outwards with a springy overshoot.
        void SetCanopyVisible(float amount, float time)
        {
            deployVisual = amount;
            bool on = amount > 0.01f;
            if (canopy.gameObject.activeSelf != on) canopy.gameObject.SetActive(on);
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].gameObject.activeSelf != (amount > 0.35f)) lines[i].gameObject.SetActive(amount > 0.35f);
            if (!on) return;
            canopy.localPosition = new Vector3(0f, Mathf.Lerp(1.2f, 5.4f, Spring01(amount * 1.3f)), 0f);
            for (int i = 0; i < cells.Count; i++)
            {
                float delay = Mathf.Abs(i - (cells.Count - 1) * 0.5f) * 0.07f;
                float k = Spring01(Mathf.Clamp01((amount - delay) / (1f - 0.3f)));
                float billow = 1f + Mathf.Sin(time * 7f + i * 0.9f) * 0.05f * Mathf.Clamp01(amount);
                var s = cellScale[i];
                cells[i].localScale = new Vector3(s.x * Mathf.Lerp(0.2f, 1f, k), s.y * k * billow, s.z * Mathf.Lerp(0.3f, 1f, k));
            }
        }

        /// 0..1 with a springy overshoot at the end.
        static float Spring01(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Exp(-6f * t) * Mathf.Cos(9f * t) * (1f - t);
        }

        // ======================================================================== physics

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
            float accel = input.sqrMagnitude > 0.01f ? 75f : 38f;   // snappy: go where you point, stop when you let go
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
            squash = -0.25f;   // jolt when the canopy grabs the air
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
            hVel = Vector3.MoveTowards(hVel, want, 20f * dt);
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

        /// Called on touchdown for a squash.
        public void Land() { squash = 0.35f; squashVel = 0f; }

        // ======================================================================== animation

        public void SetShieldVisible(bool on) { if (bubble != null) bubble.gameObject.SetActive(on); }

        public void SetTrail(bool on, Color c)
        {
            trailL.emitting = on;
            trailR.emitting = on;
            trailMat.SetColor("_Color", c);
        }

        static void SpringStep(ref float x, ref float v, float target, float k, float damp, float dt)
        {
            v += (target - x) * k * dt;
            v *= Mathf.Exp(-damp * dt);
            x += v * dt;
        }

        /// mode: 0 idle (standing), 1 free fall, 2 canopy, 3 landed
        public void Animate(int mode, Vector2 steer, float time, float dt)
        {
            if (dt <= 0f) return;
            if (mode != lastMode) { lastMode = mode; modeTime = 0f; }
            modeTime += dt;

            float targetBlend = mode == 1 ? 1f : 0f;
            poseBlend = Mathf.MoveTowards(poseBlend, targetBlend, dt * 2.5f);
            float e = poseBlend * poseBlend * (3f - 2f * poseBlend);
            float speed01 = Mathf.Clamp01(vFall / Terminal);

            // Lean into turns with a spring (a little overshoot = feels alive).
            Vector2 bankTarget = mode == 1 ? new Vector2(steer.y * 22f, -steer.x * 32f)
                               : mode == 2 ? new Vector2(steer.y * 8f, -steer.x * 18f) : Vector2.zero;
            SpringStep(ref bank.x, ref bankVel.x, bankTarget.x, 160f, 9f, dt);
            SpringStep(ref bank.y, ref bankVel.y, bankTarget.y, 160f, 9f, dt);
            if (tumbleT > 0f) tiltRot = Quaternion.Euler(spin * dt) * tiltRot;
            else tiltRot = Quaternion.Slerp(tiltRot, Quaternion.Euler(bank.x, 0f, bank.y), 1f - Mathf.Exp(-dt * 12f));

            // pendulum: under the canopy the body swings against sideways acceleration
            float swingTarget = 0f;
            if (deployed || deploying)
            {
                Vector3 acc = (hVel - lastHVel) / dt;
                swingTarget = Mathf.Clamp(-Vector3.Dot(acc, transform.right) * 2.5f, -25f, 25f);
            }
            SpringStep(ref swing, ref swingVel, swingTarget, 30f, 3f, dt);
            tilt.localRotation = tiltRot * Quaternion.Euler(0f, 0f, swing);
            pose.localRotation = Quaternion.Euler(90f * e, 0f, 0f);

            // squash & stretch
            SpringStep(ref squash, ref squashVel, 0f, 220f, 10f, dt);
            float bounce = mode == 3 && modeTime > 0.4f ? Mathf.Abs(Mathf.Sin(modeTime * 7f)) * 0.25f : 0f;
            body.localScale = new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f);
            body.localPosition = new Vector3(0f, bounce, 0f);

            if (model != null) model.Play(tumbleT > 0f ? 4 : mode, speed01, dt);
            else AnimateLimbs(mode, steer, time, dt, e, speed01);

            // canopy
            if (mode == 3)
            {
                deploying = false;
                if (deployVisual > 0f) SetCanopyVisible(Mathf.MoveTowards(deployVisual, 0f, dt * 1.2f), time);
            }
            else if (deploying) SetCanopyVisible(Mathf.Clamp01(deployT / Mathf.Max(0.01f, deployDuration) * 1.15f), time);
            if (bubble != null && bubble.gameObject.activeSelf)
                bubble.localScale = Vector3.one * (2.8f + Mathf.Sin(time * 6f) * 0.1f);

            if (deployed || deploying)
            {
                Vector3 flat = hVel;
                flat.y = 0f;
                if (flat.sqrMagnitude > 1f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized), dt * 2f);
                canopy.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.3f) * 3f, 0f, -steer.x * 12f + Mathf.Sin(time * 1.7f) * 2f);
            }
            lastHVel = hVel;
        }

        void AnimateLimbs(int mode, Vector2 steer, float time, float dt, float e, float speed01)
        {
            // Target joint angles (degrees). Shoulders/hips rotate on Z (sideways) and X (fore/back).
            float shZ, elZ, shX, hipZ, hipX, knX, headY = 0f;
            float n1 = Mathf.PerlinNoise(time * 6f, 0.3f) - 0.5f, n2 = Mathf.PerlinNoise(0.7f, time * 6f) - 0.5f;
            float flutter = 10f * speed01 * e;

            if (tumbleT > 0f)
            {
                // flailing
                shZ = 90f + Mathf.Sin(time * 20f) * 60f; elZ = 40f + Mathf.Sin(time * 17f) * 40f; shX = Mathf.Sin(time * 13f) * 50f;
                hipZ = 20f + Mathf.Sin(time * 15f) * 20f; hipX = Mathf.Sin(time * 11f) * 40f; knX = 50f + Mathf.Sin(time * 19f) * 40f;
            }
            else if (mode == 1 || (mode == 0 && e > 0.01f))
            {
                // Classic "box" position: arms out and bent up, knees bent, legs spread.
                // Steering: reach with the arms toward the turn.
                shZ = Mathf.Lerp(10f, 92f - steer.y * 18f, e) + n1 * flutter;
                elZ = Mathf.Lerp(8f, 88f + steer.y * 20f, e) + n2 * flutter;
                shX = -steer.x * 12f * e;
                hipZ = Mathf.Lerp(2f, 20f, e) + n2 * flutter * 0.6f;
                hipX = Mathf.Lerp(0f, 12f, e) - steer.y * 10f * e;
                knX = Mathf.Lerp(4f, 72f, e) + n1 * flutter * 0.6f;
                headY = -steer.x * 20f * e;
            }
            else if (mode == 2 || deployVisual > 0.5f)
            {
                // Hanging in the harness, hands up on the toggles; pull the side you turn to.
                float pullL = Mathf.Clamp01(-steer.x), pullR = Mathf.Clamp01(steer.x);
                shZ = 158f; elZ = 18f;
                shX = 0f;
                hipZ = 8f; hipX = -55f + Mathf.Sin(time * 2.2f) * 6f; knX = 65f + Mathf.Sin(time * 2.6f) * 8f;
                ApplyArms(shZ - pullL * 50f, shZ - pullR * 50f, elZ + pullL * 40f, elZ + pullR * 40f, 0f, dt);
                ApplyLegs(hipZ, hipX, knX, dt);
                SetHead(-steer.x * 25f, dt);
                return;
            }
            else if (mode == 3)
            {
                // Celebrate: arms up and waving.
                shZ = 150f + Mathf.Sin(modeTime * 9f) * 22f; elZ = 30f + Mathf.Sin(modeTime * 9f + 1f) * 20f; shX = 0f;
                hipZ = 6f; hipX = 0f; knX = 6f + Mathf.Abs(Mathf.Sin(modeTime * 7f)) * 25f;
                headY = Mathf.Sin(modeTime * 3f) * 20f;
            }
            else
            {
                // Idle at the door: breathing, looking down at the drop.
                shZ = 10f + Mathf.Sin(time * 2f) * 3f; elZ = 12f; shX = Mathf.Sin(time * 1.1f) * 4f;
                hipZ = 3f; hipX = 0f; knX = 4f;
                headY = Mathf.Sin(time * 0.7f) * 35f;
            }
            ApplyArms(shZ, shZ, elZ, elZ, shX, dt);
            ApplyLegs(hipZ, hipX, knX, dt);
            SetHead(headY, dt);
        }

        void ApplyArms(float shL, float shR, float elL, float elR, float shX, float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 14f);
            shoulderL.localRotation = Quaternion.Slerp(shoulderL.localRotation, Quaternion.Euler(shX, 0f, -shL), k);
            shoulderR.localRotation = Quaternion.Slerp(shoulderR.localRotation, Quaternion.Euler(-shX, 0f, shR), k);
            elbowL.localRotation = Quaternion.Slerp(elbowL.localRotation, Quaternion.Euler(0f, 0f, -elL), k);
            elbowR.localRotation = Quaternion.Slerp(elbowR.localRotation, Quaternion.Euler(0f, 0f, elR), k);
        }

        void ApplyLegs(float hipZ, float hipX, float knX, float dt)
        {
            float k = 1f - Mathf.Exp(-dt * 12f);
            hipL.localRotation = Quaternion.Slerp(hipL.localRotation, Quaternion.Euler(hipX, 0f, -hipZ), k);
            hipR.localRotation = Quaternion.Slerp(hipR.localRotation, Quaternion.Euler(hipX, 0f, hipZ), k);
            kneeL.localRotation = Quaternion.Slerp(kneeL.localRotation, Quaternion.Euler(knX, 0f, 0f), k);
            kneeR.localRotation = Quaternion.Slerp(kneeR.localRotation, Quaternion.Euler(knX, 0f, 0f), k);
        }

        void SetHead(float yaw, float dt)
        {
            if (head == null) return;
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0f, yaw, 0f), 1f - Mathf.Exp(-dt * 6f));
        }

        // ======================================================================== crash

        /// Turn the body into flying debris (crash).
        public void Explode(Transform debrisParent, float groundY, Vector3 baseVel)
        {
            trailL.emitting = false;
            trailR.emitting = false;
            SetCanopyVisible(0f, 0f);
            if (bubble != null) bubble.gameObject.SetActive(false);
            if (model != null)
            {
                // tumble the whole character
                var root = model.Detach(debrisParent);
                var d = root.gameObject.AddComponent<Debris>();
                d.vel = baseVel * 0.3f + new Vector3(Random.Range(-4f, 4f), Random.Range(8f, 14f), Random.Range(-4f, 4f));
                d.angVel = Random.insideUnitSphere * 540f;
                d.ground = groundY;
                return;
            }
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
