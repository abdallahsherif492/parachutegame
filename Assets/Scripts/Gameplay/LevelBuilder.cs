using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// Builds a Level (rings, coins, hazards, target, scenery) from a LevelConfig.
    public static class LevelBuilder
    {
        static System.Random rng;
        static Transform stat;   // non-moving scenery, static-batched after the build

        static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static int RI(int a, int bExclusive) { return rng.Next(a, bExclusive); }
        static Vector3 RandDir()
        {
            float a = R(0f, Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // Path the ring line follows, top to bottom (x,z plus altitude y).
        static readonly List<Vector3> path = new List<Vector3>();

        public static Vector3 PathAt(float y)
        {
            if (path.Count == 0) return Vector3.zero;
            if (y >= path[0].y) return path[0];
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                if (y <= a.y && y >= b.y)
                {
                    float t = Mathf.InverseLerp(a.y, b.y, y);
                    return Vector3.Lerp(a, b, t);
                }
            }
            return path[path.Count - 1];
        }

        public static Level Build(LevelConfig cfg, Transform parent)
        {
            rng = new System.Random(cfg.seed);
            path.Clear();
            var lv = new Level { cfg = cfg, theme = cfg.Theme };
            var th = lv.theme;
            lv.root = new GameObject("Level " + (cfg.index + 1));
            lv.root.transform.SetParent(parent, false);
            Transform root = lv.root.transform;

            SkyEnv.Apply(th.sky, th.light, th.ambientSky, th.ambientGround);
            if (Camera.main != null) Camera.main.backgroundColor = th.sky;

            if (cfg.movingTarget && !cfg.water) cfg.targetTop = 2.2f;

            float ang = R(-50f, 50f) * Mathf.Deg2Rad;
            Vector3 tXZ = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * cfg.targetDistance;
            lv.spawn = new Vector3(0f, cfg.targetTop + cfg.height, 0f);

            var windDir = RandDir();
            lv.wind = windDir * cfg.wind;

            stat = Shapes.Group("Static", root, Vector3.zero).transform;
            BuildGround(lv, stat, tXZ);
            BuildTarget(lv, root, tXZ);
            BuildRings(lv, root, tXZ);
            BuildHazards(lv, root);
            if (cfg.city) BuildTowers(lv, stat, tXZ);
            BuildCoins(lv, root);
            BuildClouds(lv, stat);
            BuildProps(lv, stat, tXZ);
            BuildPlane(lv, root);
            // Hundreds of scenery pieces -> a handful of draw calls (matters a lot on mobile WebGL).
            StaticBatchingUtility.Combine(stat.gameObject);
            return lv;
        }

        // ------------------------------------------------------------------------------ ground

        static void BuildGround(Level lv, Transform root, Vector3 tXZ)
        {
            var th = lv.theme;
            Shapes.Make("Ground", MeshGen.Plane(), Mat.Lit(th.ground), root, new Vector3(tXZ.x, 0f, tXZ.z), new Vector3(4000f, 1f, 4000f));
            int patches = lv.cfg.water ? 0 : 40;
            for (int i = 0; i < patches; i++)
            {
                Vector3 p = tXZ + RandDir() * R(30f, 600f);
                float s = R(40f, 140f);
                if (lv.cfg.city)
                {
                    // road grid
                    bool alongX = i % 2 == 0;
                    var pos = new Vector3(Mathf.Round(p.x / 60f) * 60f, 0.05f, Mathf.Round(p.z / 60f) * 60f);
                    Shapes.Make("Road", MeshGen.Plane(), Mat.Lit(th.ground2), root, pos, alongX ? new Vector3(900f, 1f, 9f) : new Vector3(9f, 1f, 900f));
                }
                else
                {
                    Shapes.Make("Patch", MeshGen.Disc(10), Mat.Lit(th.ground2), root, new Vector3(p.x, 0.04f, p.z),
                        new Vector3(s, 1f, s * R(0.5f, 1f)), Quaternion.Euler(0f, R(0f, 360f), 0f));
                }
            }
            if (lv.cfg.world == 1)
            {
                // oasis
                Vector3 o = tXZ + RandDir() * R(60f, 120f);
                Shapes.Make("Oasis", MeshGen.Disc(14), Mat.Lit(new Color(0.2f, 0.6f, 0.8f)), root, new Vector3(o.x, 0.08f, o.z), new Vector3(40f, 1f, 30f));
            }
        }

        // ------------------------------------------------------------------------------ target

        static void BuildTarget(Level lv, Transform root, Vector3 tXZ)
        {
            var cfg = lv.cfg;
            var th = lv.theme;
            float r = cfg.targetRadius;
            var t = new Target { radius = r, top = cfg.targetTop };
            var go = Shapes.Group("Target", root, new Vector3(tXZ.x, cfg.targetTop, tXZ.z));
            t.tr = go.transform;
            t.start = go.transform.position;
            lv.target = t;

            Vector3 moveAxis = RandDir();
            if (cfg.movingTarget)
            {
                t.axis = moveAxis;
                t.amp = 10f + cfg.local * 1.5f;
                t.freq = cfg.targetSpeed / t.amp;
                go.transform.rotation = Quaternion.LookRotation(moveAxis);
            }

            // Pad rings: outer, mid, bullseye.
            Shapes.Make("PadOuter", MeshGen.Disc(), Mat.Lit(th.targetA, 0.35f), go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(r * 2f, 1f, r * 2f));
            Shapes.Make("PadMid", MeshGen.Disc(), Mat.Lit(th.targetB, 0.35f), go.transform, new Vector3(0f, 0.06f, 0f), new Vector3(r * 1.2f, 1f, r * 1.2f));
            Shapes.Make("PadBull", MeshGen.Disc(), Mat.Lit(new Color(1f, 0.85f, 0.1f), 0.5f), go.transform, new Vector3(0f, 0.09f, 0f), new Vector3(r * 0.5f, 1f, r * 0.5f));
            Shapes.Make("PadEdge", MeshGen.Torus(0.02f, 32, 4), Mat.Lit(th.targetB, 0.6f), go.transform, new Vector3(0f, 0.1f, 0f), new Vector3(r * 2.08f, 3f, r * 2.08f));

            if (cfg.city)
            {
                float w = r * 2.3f;
                var b = Shapes.Box(stat, new Vector3(tXZ.x, cfg.targetTop * 0.5f, tXZ.z), new Vector3(w, cfg.targetTop, w), th.prop1);
                lv.targetBuilding = b.transform;
                lv.roofHalf = w * 0.5f;
                AddWindows(b.transform, stat, new Vector3(tXZ.x, cfg.targetTop * 0.5f, tXZ.z), new Vector3(w, cfg.targetTop, w), th);
                Shapes.Make("Roof", MeshGen.Box(), Mat.Lit(th.ground2), go.transform, new Vector3(0f, -0.25f, 0f), new Vector3(w, 0.5f, w));
            }
            else if (cfg.water)
            {
                Shapes.Make("Hull", MeshGen.Box(), Mat.Lit(new Color(0.95f, 0.95f, 0.97f)), go.transform, new Vector3(0f, -0.9f, 0f), new Vector3(r * 2.3f, 1.8f, r * 2.8f));
                Shapes.Make("HullStripe", MeshGen.Box(), Mat.Lit(th.targetB), go.transform, new Vector3(0f, -1.3f, 0f), new Vector3(r * 2.35f, 0.5f, r * 2.85f));
                Shapes.Make("Bow", MeshGen.Cone(4), Mat.Lit(new Color(0.95f, 0.95f, 0.97f)), go.transform, new Vector3(0f, -0.9f, r * 1.4f),
                    new Vector3(r * 1.6f, r * 1.3f, 1.8f), Quaternion.Euler(90f, 0f, 0f));
            }
            else if (cfg.movingTarget)
            {
                Color body = cfg.world == 3 ? new Color(1f, 0.5f, 0.1f) : new Color(0.85f, 0.2f, 0.2f);
                Shapes.Make("Bed", MeshGen.Box(), Mat.Lit(new Color(0.3f, 0.3f, 0.33f)), go.transform, new Vector3(0f, -0.4f, 0f), new Vector3(r * 2.2f, 0.8f, r * 2.4f));
                Shapes.Make("Cab", MeshGen.Box(), Mat.Lit(body), go.transform, new Vector3(0f, 0.6f, r * 1.2f + 2.2f), new Vector3(r * 1.3f, 3.4f, 3.6f));
                Shapes.Make("Glass", MeshGen.Box(), Mat.Lit(new Color(0.5f, 0.8f, 0.95f), 0.4f), go.transform, new Vector3(0f, 1.4f, r * 1.2f + 4.02f), new Vector3(r * 1.1f, 1.2f, 0.1f));
                for (int i = 0; i < 6; i++)
                {
                    float z = (i / 2 - 1) * r * 0.9f;
                    float x = (i % 2 == 0 ? -1f : 1f) * r * 1.1f;
                    Shapes.Make("Wheel", MeshGen.Cylinder(8), Mat.Lit(new Color(0.1f, 0.1f, 0.1f)), go.transform, new Vector3(x, -1.4f, z),
                        new Vector3(1.6f, 0.6f, 1.6f), Quaternion.Euler(0f, 0f, 90f));
                }
            }
            else
            {
                Shapes.Make("Base", MeshGen.Cylinder(20), Mat.Lit(new Color(0.55f, 0.55f, 0.6f)), go.transform, new Vector3(0f, -0.15f, 0f), new Vector3(r * 2.15f, 0.3f, r * 2.15f));
            }

            // Guide beam visible from high up.
            Shapes.Make("Beam", MeshGen.Cylinder(12), Mat.Clear(new Color(th.targetB.r, th.targetB.g, th.targetB.b, 0.22f), 0.1f), go.transform,
                new Vector3(0f, 300f, 0f), new Vector3(r * 0.5f, 600f, r * 0.5f));

            // Windsock shows wind direction + strength at the pad.
            if (lv.wind.sqrMagnitude > 0.01f)
            {
                var pole = Shapes.Group("Windsock", go.transform, new Vector3(r * 0.95f, 0f, 0f));
                Shapes.Make("Pole", MeshGen.Cylinder(6), Mat.Lit(new Color(0.8f, 0.8f, 0.8f)), pole.transform, new Vector3(0f, 2f, 0f), new Vector3(0.15f, 4f, 0.15f));
                var sock = Shapes.Group("Sock", pole.transform, new Vector3(0f, 3.8f, 0f));
                sock.transform.rotation = Quaternion.LookRotation(lv.wind.normalized);
                float len = 1f + lv.cfg.wind * 0.4f;
                Shapes.Make("Cone", MeshGen.Cone(8), Mat.Lit(new Color(1f, 0.45f, 0.1f)), sock.transform, Vector3.zero,
                    new Vector3(0.8f, len, 0.8f), Quaternion.Euler(90f, 0f, 0f));
                lv.windsock = sock.transform;
            }

            path.Add(lv.spawn);
        }

        static void AddWindows(Transform building, Transform root, Vector3 center, Vector3 size, WorldTheme th)
        {
            int rows = Mathf.Clamp(Mathf.FloorToInt(size.y / 6f), 1, 30);
            Color[] neon = { th.prop2, th.prop3, new Color(1f, 0.85f, 0.3f) };
            Color c = neon[Mathf.Abs(Mathf.RoundToInt(center.x + center.z)) % neon.Length];
            var mat = Mat.Lit(c, 1f);
            for (int i = 0; i < rows; i++)
            {
                if (i % 2 == 1) continue;
                float y = center.y - size.y * 0.5f + 3f + i * 6f;
                Shapes.Make("Win", MeshGen.Box(), mat, root, new Vector3(center.x, y, center.z), new Vector3(size.x + 0.2f, 0.8f, size.z + 0.2f));
            }
        }

        // ------------------------------------------------------------------------------ rings

        static void BuildRings(Level lv, Transform root, Vector3 tXZ)
        {
            var cfg = lv.cfg;
            int n = cfg.rings;
            float yTop = lv.spawn.y - 60f;
            float yBot = cfg.targetTop + 140f;
            float gapY = (yTop - yBot) / Mathf.Max(1, n - 1);
            float gapTime = gapY / 42f;
            float maxStep = Mathf.Max(5f, 0.6f * 13f * gapTime);
            Vector3 dir = tXZ.normalized;
            var perp = new Vector3(dir.z, 0f, -dir.x);
            float freq = R(1.2f, 2.4f);
            float phase = R(0f, Mathf.PI * 2f);
            Vector3 prev = Vector3.zero;
            var matA = Mat.Lit(new Color(1f, 0.82f, 0.15f), 0.55f);
            var matB = Mat.Lit(new Color(0.2f, 0.9f, 1f), 0.55f);

            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / (n + 1f);
                float y = yTop - gapY * i;
                Vector3 p = tXZ * t;
                float off = Mathf.Sin(t * freq * Mathf.PI + phase) * cfg.lateral * Mathf.Clamp01((1f - t) * 1.3f) + R(-2f, 2f);
                p += perp * off;
                Vector3 step = p - prev;
                if (step.magnitude > maxStep) p = prev + step.normalized * maxStep;
                prev = p;

                var pos = new Vector3(p.x, y, p.z);
                var go = Shapes.Make("Ring", MeshGen.Torus(0.06f), i % 2 == 0 ? matA : matB, root, pos, Vector3.one * cfg.ringRadius * 2f);
                lv.rings.Add(new Ring { pos = pos, radius = cfg.ringRadius, tr = go.transform });
                path.Add(pos);
            }
            path.Add(new Vector3(tXZ.x, cfg.targetTop + 45f, tXZ.z));
            path.Add(new Vector3(tXZ.x, cfg.targetTop, tXZ.z));
        }

        // ------------------------------------------------------------------------------ coins

        static void BuildCoins(Level lv, Transform root)
        {
            var mat = Mat.Lit(new Color(1f, 0.8f, 0.1f), 0.45f);
            for (int s = 0; s < path.Count - 2; s++)
            {
                Vector3 a = path[s], b = path[s + 1];
                bool last = s == path.Count - 3;
                int count = last ? 5 : 3;
                for (int k = 1; k <= count; k++)
                {
                    float t = k / (count + 1f);
                    if (s == 0 && t < 0.4f) continue;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    if (InsideHazard(lv, p, 2f)) continue;
                    AddCoin(lv, root, mat, p);
                }
            }
            // Risky bonus coins hugging balloons and helicopters.
            foreach (var h in lv.hazards)
            {
                if (h.kind == HazardKind.Storm || h.label == "Bird!") continue;
                float rad = h.box ? h.half.x : h.radius;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = h.basePos + RandDir() * (rad + 1.8f) + Vector3.up * R(-2f, 2f);
                    if (!InsideHazard(lv, p, 1.2f)) AddCoin(lv, root, mat, p);
                }
            }
        }

        static void AddCoin(Level lv, Transform root, Material mat, Vector3 p)
        {
            var go = Shapes.Make("Coin", MeshGen.Cylinder(12), mat, root, p, new Vector3(1.4f, 0.18f, 1.4f), Quaternion.Euler(90f, 0f, 0f));
            lv.coins.Add(new Coin { tr = go.transform });
        }

        static bool InsideHazard(Level lv, Vector3 p, float margin)
        {
            foreach (var h in lv.hazards)
                if (h.motion == 0 || h.motion == 3)
                {
                    h.tr.position = h.basePos;
                    if (h.Distance(p) < margin) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------------------ hazards

        static void BuildHazards(Level lv, Transform root)
        {
            var cfg = lv.cfg;
            // Candidate altitude slots: midway between path points.
            var slots = new List<float>();
            for (int i = 0; i < path.Count - 2; i++)
            {
                float y = (path[i].y + path[i + 1].y) * 0.5f;
                if (y > lv.spawn.y - 60f || y < cfg.targetTop + 80f) continue;
                slots.Add(y);
            }
            if (slots.Count == 0) slots.Add(cfg.targetTop + cfg.height * 0.5f);

            var kinds = new List<int>();
            for (int i = 0; i < cfg.birdFlocks; i++) kinds.Add(0);
            for (int i = 0; i < cfg.balloons; i++) kinds.Add(1);
            for (int i = 0; i < cfg.drones; i++) kinds.Add(2);
            for (int i = 0; i < cfg.helis; i++) kinds.Add(3);
            for (int i = 0; i < cfg.storms; i++) kinds.Add(4);
            // shuffle
            for (int i = kinds.Count - 1; i > 0; i--)
            {
                int j = RI(0, i + 1);
                int tmp = kinds[i]; kinds[i] = kinds[j]; kinds[j] = tmp;
            }

            float gap = slots.Count > 1 ? Mathf.Abs(slots[0] - slots[1]) : 60f;
            for (int i = 0; i < kinds.Count; i++)
            {
                float y = slots[i % slots.Count];
                if (i >= slots.Count) y += R(-0.28f, 0.28f) * gap;
                Vector3 c = PathAt(y);
                c.y = y;
                switch (kinds[i])
                {
                    case 0: Birds(lv, root, c); break;
                    case 1: Balloon(lv, root, c); break;
                    case 2: Drone(lv, root, c); break;
                    case 3: Heli(lv, root, c); break;
                    case 4: Storm(lv, root, c); break;
                }
            }
        }

        static Hazard Add(Level lv, GameObject go, HazardKind kind, string label, float radius)
        {
            var h = new Hazard { tr = go.transform, kind = kind, label = label, radius = radius, basePos = go.transform.position };
            lv.hazards.Add(h);
            return h;
        }

        static void Birds(Level lv, Transform root, Vector3 c)
        {
            int count = RI(3, 6);
            Vector3 axis = RandDir();
            float speed = R(5f, 9f);
            float amp = R(10f, 16f);
            float phase = R(0f, Mathf.PI * 2f);
            var body = Mat.Lit(new Color(0.25f, 0.22f, 0.2f));
            var wing = Mat.Lit(new Color(0.35f, 0.3f, 0.28f));
            var beak = Mat.Lit(new Color(1f, 0.6f, 0.1f));
            Vector3 side = new Vector3(axis.z, 0f, -axis.x);
            for (int i = 0; i < count; i++)
            {
                // V formation
                Vector3 off = -axis * (Mathf.Abs(i - count / 2) * 2.2f) + side * ((i - count / 2) * 2.4f) + Vector3.up * R(-1f, 1f);
                var go = Shapes.Group("Bird", root, c + off);
                Shapes.Make("Body", MeshGen.Sphere(0), body, go.transform, Vector3.zero, new Vector3(0.6f, 0.5f, 1.3f));
                Shapes.Make("Beak", MeshGen.Cone(4), beak, go.transform, new Vector3(0f, 0f, 0.7f), new Vector3(0.2f, 0.4f, 0.2f), Quaternion.Euler(90f, 0f, 0f));
                var wl = Shapes.Group("WingL", go.transform, new Vector3(-0.2f, 0.1f, 0f));
                Shapes.Make("W", MeshGen.Box(), wing, wl.transform, new Vector3(-0.9f, 0f, 0f), new Vector3(1.8f, 0.08f, 0.8f));
                var wr = Shapes.Group("WingR", go.transform, new Vector3(0.2f, 0.1f, 0f));
                Shapes.Make("W", MeshGen.Box(), wing, wr.transform, new Vector3(0.9f, 0f, 0f), new Vector3(1.8f, 0.08f, 0.8f));
                var h = Add(lv, go, HazardKind.Tumble, "Bird!", 1.0f);
                h.motion = 1; h.axis = axis; h.amp = amp; h.freq = speed / amp; h.phase = phase;
                h.faceMotion = true;
                h.spinners = new[] { wl.transform, wr.transform };
                h.flap = true; h.spinSpeed = 14f;
            }
        }

        static void Balloon(Level lv, Transform root, Vector3 c)
        {
            Vector3 pos = c + RandDir() * R(1.5f, 4f);
            var go = Shapes.Group("Balloon", root, pos);
            Color[] cols = { new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 0.95f), new Color(0.95f, 0.75f, 0.2f), new Color(0.6f, 0.35f, 0.9f) };
            Color col = cols[RI(0, cols.Length)];
            Shapes.Make("Envelope", MeshGen.Sphere(1), Mat.Lit(col), go.transform, Vector3.zero, new Vector3(7f, 8f, 7f));
            Shapes.Make("Band", MeshGen.Cylinder(12), Mat.Lit(Color.white), go.transform, new Vector3(0f, 0.3f, 0f), new Vector3(7.1f, 1.4f, 7.1f));
            Shapes.Make("Basket", MeshGen.Box(), Mat.Lit(new Color(0.55f, 0.38f, 0.2f)), go.transform, new Vector3(0f, -5.8f, 0f), new Vector3(1.6f, 1.2f, 1.6f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Shapes.Make("Rope", MeshGen.Box(), Mat.Lit(new Color(0.3f, 0.25f, 0.2f)), go.transform, d * 1.2f + Vector3.down * 4.3f,
                    new Vector3(0.08f, 2.8f, 0.08f));
            }
            var h = Add(lv, go, HazardKind.Tumble, "Balloon!", 3.6f);
            h.motion = 3; h.amp = 1.5f; h.freq = R(0.6f, 1f); h.phase = R(0f, 6f);
        }

        static void Drone(Level lv, Transform root, Vector3 c)
        {
            var go = Shapes.Group("Drone", root, c + RandDir() * R(0f, 3f));
            Shapes.Make("Body", MeshGen.Box(), Mat.Lit(new Color(0.2f, 0.2f, 0.24f)), go.transform, Vector3.zero, new Vector3(1.2f, 0.4f, 1.2f));
            Shapes.Make("Light", MeshGen.Sphere(0), Mat.Lit(new Color(1f, 0.1f, 0.1f), 1f), go.transform, new Vector3(0f, -0.3f, 0.5f), Vector3.one * 0.35f);
            var rotors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward;
                Shapes.Make("Arm", MeshGen.Box(), Mat.Lit(new Color(0.3f, 0.3f, 0.35f)), go.transform, d * 0.8f, new Vector3(0.15f, 0.15f, 0.15f) + new Vector3(Mathf.Abs(d.x), 0f, Mathf.Abs(d.z)) * 1.2f);
                var r = Shapes.Group("Rotor", go.transform, d * 1.4f + Vector3.up * 0.3f);
                Shapes.Make("Blade", MeshGen.Box(), Mat.Lit(new Color(0.85f, 0.85f, 0.9f)), r.transform, Vector3.zero, new Vector3(1.4f, 0.05f, 0.15f));
                rotors[i] = r.transform;
            }
            var h = Add(lv, go, HazardKind.Tumble, "Drone!", 1.5f);
            h.motion = 1; h.axis = RandDir(); h.amp = R(6f, 10f); h.freq = R(0.8f, 1.5f); h.phase = R(0f, 6f);
            h.spinners = rotors; h.spinSpeed = 1500f;
        }

        static void Heli(Level lv, Transform root, Vector3 c)
        {
            var go = Shapes.Group("Heli", root, c + RandDir() * R(5f, 9f));
            var hull = Mat.Lit(new Color(0.9f, 0.25f, 0.2f));
            Shapes.Make("Cabin", MeshGen.Sphere(1), hull, go.transform, Vector3.zero, new Vector3(3f, 2.6f, 4.4f));
            Shapes.Make("Glass", MeshGen.Sphere(1), Mat.Lit(new Color(0.55f, 0.85f, 1f), 0.3f), go.transform, new Vector3(0f, 0.3f, 1.3f), new Vector3(2.4f, 1.8f, 2.2f));
            Shapes.Make("Tail", MeshGen.Box(), hull, go.transform, new Vector3(0f, 0.3f, -4f), new Vector3(0.5f, 0.5f, 5f));
            Shapes.Make("Fin", MeshGen.Box(), hull, go.transform, new Vector3(0f, 1f, -6.3f), new Vector3(0.2f, 1.6f, 0.9f));
            Shapes.Make("SkidL", MeshGen.Box(), Mat.Lit(new Color(0.2f, 0.2f, 0.2f)), go.transform, new Vector3(-1.1f, -1.6f, 0f), new Vector3(0.15f, 0.15f, 4f));
            Shapes.Make("SkidR", MeshGen.Box(), Mat.Lit(new Color(0.2f, 0.2f, 0.2f)), go.transform, new Vector3(1.1f, -1.6f, 0f), new Vector3(0.15f, 0.15f, 4f));
            var rotor = Shapes.Group("Rotor", go.transform, new Vector3(0f, 1.6f, 0f));
            var blade = Mat.Lit(new Color(0.15f, 0.15f, 0.15f));
            Shapes.Make("B1", MeshGen.Box(), blade, rotor.transform, Vector3.zero, new Vector3(9f, 0.08f, 0.45f));
            Shapes.Make("B2", MeshGen.Box(), blade, rotor.transform, Vector3.zero, new Vector3(0.45f, 0.08f, 9f));
            var h = Add(lv, go, HazardKind.Lethal, "Helicopter!", 0f);
            h.box = true; h.half = new Vector3(4.3f, 1.8f, 4.3f);
            h.motion = 2; h.amp = R(4f, 7f); h.freq = R(0.25f, 0.4f); h.phase = R(0f, 6f);
            h.faceMotion = true;
            h.spinners = new[] { rotor.transform }; h.spinSpeed = 900f;
        }

        static void Storm(Level lv, Transform root, Vector3 c)
        {
            var go = Shapes.Group("Storm", root, c + RandDir() * R(6f, 11f));
            var dark = Mat.Lit(new Color(0.32f, 0.33f, 0.4f));
            var dark2 = Mat.Lit(new Color(0.42f, 0.43f, 0.5f));
            for (int i = 0; i < 6; i++)
            {
                Vector3 o = RandDir() * R(0f, 5f) + Vector3.up * R(-2f, 2f);
                float s = R(7f, 11f);
                Shapes.Make("Puff", MeshGen.Sphere(1), i % 2 == 0 ? dark : dark2, go.transform, o, new Vector3(s, s * 0.7f, s));
            }
            var bolt = Shapes.Group("Bolt", go.transform, Vector3.zero);
            var boltMat = Mat.Lit(new Color(1f, 0.95f, 0.4f), 1f);
            Vector3 p = Vector3.down * 3f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 n = p + new Vector3(R(-2f, 2f), -3.5f, R(-2f, 2f));
                var seg = Shapes.Make("Seg", MeshGen.Box(), boltMat, bolt.transform, (p + n) * 0.5f, new Vector3(0.35f, (n - p).magnitude, 0.35f));
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, n - p);
                p = n;
            }
            bolt.SetActive(false);
            lv.lightning.Add(bolt);
            var h = Add(lv, go, HazardKind.Storm, "Storm!", 8f);
            h.motion = 1; h.axis = RandDir(); h.amp = 3f; h.freq = 0.3f; h.phase = R(0f, 6f);
        }

        static void BuildTowers(Level lv, Transform root, Vector3 tXZ)
        {
            var th = lv.theme;
            int placed = 0, attempts = 0;
            while (placed < lv.cfg.towers && attempts < 400)
            {
                attempts++;
                float y = R(60f, 260f);
                Vector3 around = PathAt(y * R(0.3f, 1f));
                Vector3 p = new Vector3(around.x, 0f, around.z) + RandDir() * R(14f, 70f);
                if (attempts % 3 == 0) p = tXZ + RandDir() * R(lv.cfg.targetRadius * 2f + 12f, 90f);
                float w = R(9f, 18f);
                bool ok = true;
                // keep a clear corridor along the jump line below this tower's roof
                for (float yy = 5f; yy < y + 20f; yy += 10f)
                {
                    Vector3 c = PathAt(yy);
                    c.y = 0f;
                    if (Vector3.Distance(c, p) < w * 0.75f + 11f) { ok = false; break; }
                }
                Vector3 tt = tXZ; tt.y = 0f;
                if (Vector3.Distance(tt, p) < lv.cfg.targetRadius * 1.7f + w * 0.75f + 10f) ok = false;
                if (!ok) continue;
                var center = new Vector3(p.x, y * 0.5f, p.z);
                var size = new Vector3(w, y, w * R(0.8f, 1.2f));
                var go = Shapes.Box(root, center, size, th.prop1);
                AddWindows(go.transform, root, center, size, th);
                var h = new Hazard { tr = go.transform, kind = HazardKind.Lethal, label = "Building!", box = true, half = size * 0.5f, basePos = center };
                lv.hazards.Add(h);
                placed++;
            }
        }

        // ------------------------------------------------------------------------------ scenery

        static void BuildClouds(Level lv, Transform root)
        {
            var th = lv.theme;
            Color cc = th.night ? new Color(0.3f, 0.27f, 0.45f) : Color.white;
            var m1 = Mat.Lit(cc, th.night ? 0f : 0.6f);
            var m2 = Mat.Lit(Color.Lerp(cc, th.sky, 0.3f), th.night ? 0f : 0.5f);
            float top = lv.spawn.y + 80f;
            for (float y = 220f; y < top; y += 160f)
            {
                int n = th.night ? 4 : 9;
                for (int i = 0; i < n; i++)
                {
                    Vector3 c = PathAt(y);
                    Vector3 p = new Vector3(c.x, y + R(-20f, 20f), c.z) + RandDir() * R(18f, 220f);
                    var g = Shapes.Group("Cloud", root, p);
                    int puffs = RI(3, 6);
                    for (int k = 0; k < puffs; k++)
                    {
                        float s = R(10f, 22f);
                        Shapes.Make("Puff", MeshGen.Sphere(1), k % 2 == 0 ? m1 : m2, g.transform,
                            new Vector3(R(-12f, 12f), R(-2f, 3f), R(-12f, 12f)), new Vector3(s, s * 0.55f, s));
                    }
                }
            }
        }

        static void BuildProps(Level lv, Transform root, Vector3 tXZ)
        {
            var cfg = lv.cfg;
            var th = lv.theme;
            float clear = cfg.targetRadius * 1.6f + 16f;
            int count = cfg.city ? 25 : 70;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = tXZ + RandDir() * R(clear, 380f);
                if (Vector3.Distance(new Vector3(p.x, 0f, p.z), Vector3.zero) < 20f) continue;
                switch (cfg.world)
                {
                    case 0: if (i % 6 == 0) House(lv, root, p, th); else Tree(lv, root, p, th, false); break;
                    case 1: if (i % 4 == 0) Palm(lv, root, p, th); else Cactus(lv, root, p, th); break;
                    case 2: if (i % 5 == 0) Island(lv, root, p, th); break;
                    case 3: if (i % 5 == 0) Rock(lv, root, p, th); else Tree(lv, root, p, th, true); break;
                    case 4:
                        float h = R(10f, 35f);
                        var go = Shapes.Box(root, new Vector3(p.x, h * 0.5f, p.z), new Vector3(R(8f, 16f), h, R(8f, 16f)), th.prop1);
                        AddWindows(go.transform, root, go.transform.position, go.transform.localScale, th);
                        GroundHazard(lv, go.transform.position, go.transform.localScale * 0.5f, "Building!");
                        break;
                }
            }
            if (cfg.world == 1)
            {
                // The pyramids: giant landmarks.
                Vector3 side = new Vector3(tXZ.z, 0f, -tXZ.x).normalized;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = tXZ + side * (i % 2 == 0 ? 1f : -1f) * R(160f, 260f) + tXZ.normalized * R(50f, 220f) * (i == 2 ? 1f : 0.3f);
                    float s = i == 0 ? 150f : R(80f, 120f);
                    Shapes.Make("Pyramid", MeshGen.Pyramid(), Mat.Lit(th.prop1), root, new Vector3(p.x, 0f, p.z), new Vector3(s, s * 0.65f, s),
                        Quaternion.Euler(0f, R(0f, 90f), 0f));
                }
            }
            if (cfg.world == 3)
            {
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = tXZ + RandDir() * R(350f, 700f);
                    float h = R(220f, 420f);
                    Shapes.Make("Mountain", MeshGen.Cone(7), Mat.Lit(th.prop2), root, new Vector3(p.x, 0f, p.z), new Vector3(h * 1.4f, h, h * 1.4f));
                    Shapes.Make("Cap", MeshGen.Cone(7), Mat.Lit(th.prop3), root, new Vector3(p.x, h * 0.6f, p.z), new Vector3(h * 0.56f, h * 0.4f, h * 0.56f));
                }
            }
        }

        static void GroundHazard(Level lv, Vector3 center, Vector3 half, string label)
        {
            var dummy = new GameObject("GH").transform;
            dummy.SetParent(lv.root.transform, false);
            dummy.position = center;
            lv.groundHazards.Add(new Hazard { tr = dummy, kind = HazardKind.Lethal, label = label, box = true, half = half, basePos = center });
        }

        static void Tree(Level lv, Transform root, Vector3 p, WorldTheme th, bool pine)
        {
            float s = R(0.8f, 1.6f);
            var g = Shapes.Group("Tree", root, new Vector3(p.x, 0f, p.z));
            Shapes.Make("Trunk", MeshGen.Cylinder(6), Mat.Lit(new Color(0.45f, 0.3f, 0.2f)), g.transform,
                new Vector3(0f, 1.5f * s, 0f), new Vector3(0.6f * s, 3f * s, 0.6f * s));
            var leaf = Mat.Lit(th.prop1);
            if (pine)
            {
                for (int k = 0; k < 3; k++)
                    Shapes.Make("Leaf", MeshGen.Cone(7), leaf, g.transform, new Vector3(0f, (2.5f + k * 2f) * s, 0f), new Vector3((4.5f - k) * s, 4f * s, (4.5f - k) * s));
                Shapes.Make("Snow", MeshGen.Cone(7), Mat.Lit(th.prop3), g.transform, new Vector3(0f, 7.5f * s, 0f), new Vector3(1.6f * s, 1.5f * s, 1.6f * s));
            }
            else
            {
                Shapes.Make("Leaf", MeshGen.Sphere(1), leaf, g.transform, new Vector3(0f, 4.5f * s, 0f), new Vector3(4.5f * s, 4.5f * s, 4.5f * s));
            }
            GroundHazard(lv, new Vector3(p.x, 3.5f * s, p.z), new Vector3(1.8f * s, 3.5f * s, 1.8f * s), "Tree!");
        }

        static void House(Level lv, Transform root, Vector3 p, WorldTheme th)
        {
            var g = Shapes.Group("House", root, new Vector3(p.x, 0f, p.z));
            g.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            Shapes.Make("Walls", MeshGen.Box(), Mat.Lit(th.prop3), g.transform, new Vector3(0f, 2f, 0f), new Vector3(7f, 4f, 6f));
            Shapes.Make("Roof", MeshGen.Pyramid(), Mat.Lit(new Color(0.75f, 0.3f, 0.25f)), g.transform, new Vector3(0f, 4f, 0f), new Vector3(9f, 3.5f, 8f));
            GroundHazard(lv, new Vector3(p.x, 3.5f, p.z), new Vector3(4.5f, 3.5f, 4.5f), "House!");
        }

        static void Cactus(Level lv, Transform root, Vector3 p, WorldTheme th)
        {
            var g = Shapes.Group("Cactus", root, new Vector3(p.x, 0f, p.z));
            var m = Mat.Lit(th.prop2);
            float h = R(2.5f, 4.5f);
            Shapes.Make("Stem", MeshGen.Cylinder(6), m, g.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.8f, h, 0.8f));
            Shapes.Make("Arm", MeshGen.Cylinder(6), m, g.transform, new Vector3(0.7f, h * 0.6f, 0f), new Vector3(0.5f, h * 0.35f, 0.5f));
            GroundHazard(lv, new Vector3(p.x, h * 0.5f, p.z), new Vector3(1f, h * 0.5f, 1f), "Cactus!");
        }

        static void Palm(Level lv, Transform root, Vector3 p, WorldTheme th)
        {
            var g = Shapes.Group("Palm", root, new Vector3(p.x, 0f, p.z));
            float h = R(5f, 8f);
            Shapes.Make("Trunk", MeshGen.Cylinder(6), Mat.Lit(new Color(0.55f, 0.38f, 0.22f)), g.transform,
                new Vector3(0f, h * 0.5f, 0f), new Vector3(0.5f, h, 0.5f), Quaternion.Euler(R(-8f, 8f), 0f, R(-8f, 8f)));
            var leaf = Mat.Lit(new Color(0.2f, 0.6f, 0.28f));
            for (int k = 0; k < 5; k++)
                Shapes.Make("Frond", MeshGen.Box(), leaf, g.transform, new Vector3(0f, h, 0f) + Quaternion.Euler(0f, k * 72f, 0f) * Vector3.forward * 1.6f,
                    new Vector3(0.9f, 0.15f, 3.4f), Quaternion.Euler(20f, k * 72f, 0f));
            GroundHazard(lv, new Vector3(p.x, h * 0.5f, p.z), new Vector3(1.5f, h * 0.5f, 1.5f), "Palm tree!");
        }

        static void Island(Level lv, Transform root, Vector3 p, WorldTheme th)
        {
            float s = R(25f, 60f);
            Shapes.Make("Island", MeshGen.Cylinder(12), Mat.Lit(th.ground2), root, new Vector3(p.x, 0.2f, p.z), new Vector3(s, 1.2f, s * R(0.6f, 1f)));
            int palms = RI(1, 4);
            for (int k = 0; k < palms; k++) Palm(lv, root, p + RandDir() * R(0f, s * 0.25f), th);
        }

        static void Rock(Level lv, Transform root, Vector3 p, WorldTheme th)
        {
            float s = R(3f, 7f);
            Shapes.Make("Rock", MeshGen.Sphere(0), Mat.Lit(th.prop2), root, new Vector3(p.x, s * 0.25f, p.z), new Vector3(s, s * 0.7f, s * 0.9f),
                Quaternion.Euler(0f, R(0f, 360f), 0f));
            GroundHazard(lv, new Vector3(p.x, s * 0.3f, p.z), new Vector3(s * 0.45f, s * 0.35f, s * 0.45f), "Rock!");
        }

        static void BuildPlane(Level lv, Transform root)
        {
            var g = Shapes.Group("Plane", root, lv.spawn + new Vector3(1.5f, 1.2f, 2.4f));
            var body = Mat.Lit(new Color(0.92f, 0.92f, 0.95f));
            var accent = Mat.Lit(new Color(0.95f, 0.35f, 0.2f));
            Shapes.Make("Fuselage", MeshGen.Box(), body, g.transform, Vector3.zero, new Vector3(16f, 3.2f, 3.2f));
            Shapes.Make("Stripe", MeshGen.Box(), accent, g.transform, new Vector3(0f, -0.6f, 0f), new Vector3(16.1f, 0.6f, 3.3f));
            Shapes.Make("Nose", MeshGen.Cone(8), body, g.transform, new Vector3(8f, 0f, 0f), new Vector3(3.2f, 2.5f, 3.2f), Quaternion.Euler(0f, 0f, -90f));
            Shapes.Make("Wing", MeshGen.Box(), body, g.transform, new Vector3(1f, 1.4f, 0f), new Vector3(3.5f, 0.35f, 22f));
            Shapes.Make("Tail", MeshGen.Box(), accent, g.transform, new Vector3(-7.2f, 2.2f, 0f), new Vector3(2f, 3f, 0.3f));
            Shapes.Make("Stab", MeshGen.Box(), body, g.transform, new Vector3(-7.2f, 0.8f, 0f), new Vector3(2f, 0.25f, 7f));
            Shapes.Make("Door", MeshGen.Box(), Mat.Lit(new Color(0.15f, 0.15f, 0.2f)), g.transform, new Vector3(-1.5f, -0.1f, -1.61f), new Vector3(2.2f, 2.6f, 0.05f));
            // little step the jumper stands on before the jump
            Shapes.Make("Step", MeshGen.Box(), accent, g.transform, new Vector3(-1.5f, -2.42f, -2.2f), new Vector3(1.4f, 0.15f, 1.4f));
            var prop = Shapes.Group("Prop", g.transform, new Vector3(9.8f, 0f, 0f));
            Shapes.Make("Blade", MeshGen.Box(), Mat.Lit(new Color(0.2f, 0.2f, 0.2f)), prop.transform, Vector3.zero, new Vector3(0.1f, 4f, 0.3f));
            lv.plane = g.transform;
        }
    }
}
