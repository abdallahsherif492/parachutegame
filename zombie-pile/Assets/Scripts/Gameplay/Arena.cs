using UnityEngine;

namespace ZombiePile
{
    /// The battlefield: a street funnelled between ruined buildings into a gate wall.
    /// Zombies come from +z and pile up against the wall face at z = WallFront.
    public class Arena : MonoBehaviour
    {
        public static float WallHeight = 7.5f;
        public static float WallFront = 0.75f;     // z of the face the zombies climb
        public static float HalfWidth = 5.2f;      // the gate: zombies are funnelled into |x| < HalfWidth
        public static float SpawnZ = 27f;
        public const int IgnoreRaycast = 2;        // built-in layer: bullets pass through walls, bodies still collide
        public const int Props = 1;                // built-in "TransparentFX": bullets hit these, zombies run through them

        readonly System.Collections.Generic.List<Transform> flames = new System.Collections.Generic.List<Transform>();

        public static Arena Build()
        {
            Physics.IgnoreLayerCollision(Props, 0, true);
            var a = new GameObject("Arena").AddComponent<Arena>();
            if (Kit.Available) a.MakeFromKit(); else a.Make();
            return a;
        }

        /// The street built from the Zombie Apocalypse Kit models (layout.json), plus invisible colliders.
        /// Nothing solid stands in the lane: the horde only meets the wall and each other.
        void MakeFromKit()
        {
            var L = Kit.Layout;
            WallHeight = L.wallHeight; WallFront = L.wallFront; HalfWidth = L.halfWidth; SpawnZ = 50f;
            var t = transform;
            foreach (var p in L.props)
            {
                var pos = Kit.LayoutPos(p);
                bool small = p.m.StartsWith("Blood") || p.m.StartsWith("Street");
                var go = Kit.Place(p.m, pos, Kit.LayoutYaw(p), p.s <= 0f ? 1f : p.s, t, !small);
                if (go == null) continue;
                bool inLane = pos.y < 0.1f && Mathf.Abs(pos.x) < HalfWidth && pos.z > WallFront + 0.5f;
                if (inLane && p.m == "Barrel") { AddBox(go, Props); go.AddComponent<ExplosiveBarrel>(); }
            }

            // ground under everything
            var groundMat = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.25f, 0.24f) };
            groundMat.SetFloat("_Glossiness", 0.05f);
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(t, false);
            g.transform.position = new Vector3(0f, -0.02f, 40f);
            g.transform.localScale = new Vector3(30f, 1f, 30f);
            g.GetComponent<Renderer>().sharedMaterial = groundMat;
            Destroy(g.GetComponent<Collider>());
            Colliders(t);
        }

        static void AddBox(GameObject go, int layer)
        {
            // measure in the holder's own frame (holders are only rotated about Y)
            var rot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;
            var b = Kit.BoundsOf(go);
            if (b.size.sqrMagnitude < 0.01f) b = new Bounds(go.transform.position + Vector3.up * 0.57f, new Vector3(0.7f, 1.15f, 0.7f));   // a drum
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = new Vector3(Mathf.Max(0.3f, b.size.x), Mathf.Max(0.3f, b.size.y), Mathf.Max(0.3f, b.size.z));
            go.transform.rotation = rot;
            go.layer = layer;
        }

        /// Invisible physics: ground, the wall the horde climbs and the side walls of the funnel.
        static void Colliders(Transform t)
        {
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(t, false);
            var gc = ground.AddComponent<BoxCollider>();
            gc.center = new Vector3(0, -0.5f, 40);
            gc.size = new Vector3(160, 1, 200);
            var wall = new GameObject("WallCollider");
            wall.transform.SetParent(t, false);
            wall.layer = IgnoreRaycast;
            var wc = wall.AddComponent<BoxCollider>();
            wc.center = new Vector3(0, WallHeight / 2f, WallFront - 1.3f);
            wc.size = new Vector3(40f, WallHeight, 2.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                var side = new GameObject("SideCollider");
                side.transform.SetParent(t, false);
                side.layer = IgnoreRaycast;
                var sc = side.AddComponent<BoxCollider>();
                sc.center = new Vector3(s * (HalfWidth + 2f), 6f, 45f);
                sc.size = new Vector3(4f, 12f, 92f);
            }
        }

        void Make()
        {
            var t = transform;
            var asphalt = new Color(0.2f, 0.2f, 0.23f);
            var dirt = new Color(0.3f, 0.26f, 0.24f);

            // ground (visual + collider)
            Shapes.Make("Ground", MeshGen.Plane(), Mat.Lit(dirt), t, new Vector3(0, 0, 40), new Vector3(160, 1, 200));
            Shapes.Make("Road", MeshGen.Plane(), Mat.Lit(asphalt), t, new Vector3(0, 0.01f, 40), new Vector3(HalfWidth * 2f + 1f, 1, 120));
            for (int i = 0; i < 14; i++)   // lane marks
                Shapes.Box(t, new Vector3(0, 0.02f, 4f + i * 5f), new Vector3(0.25f, 0.02f, 2.2f), new Color(0.85f, 0.78f, 0.45f));
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(t, false);
            var gc = ground.AddComponent<BoxCollider>();
            gc.center = new Vector3(0, -0.5f, 40);
            gc.size = new Vector3(160, 1, 200);

            // the wall: concrete blocks with a walkway, sandbags and a gate
            var wall = new GameObject("Wall");
            wall.transform.SetParent(t, false);
            wall.layer = IgnoreRaycast;
            var wc = wall.AddComponent<BoxCollider>();
            wc.center = new Vector3(0, WallHeight / 2f, 0);
            wc.size = new Vector3(40f, WallHeight, WallFront * 2f);
            var concrete = new Color(0.62f, 0.6f, 0.58f);
            for (int row = 0; row < 5; row++)
                for (int i = -12; i <= 12; i++)
                {
                    float off = row % 2 == 0 ? 0f : 0.8f;
                    float shade = 0.92f + ((i * 7 + row * 3) % 5) * 0.03f;
                    Shapes.Box(wall.transform, new Vector3(i * 1.6f + off, row * 1.5f + 0.75f, 0.02f),
                        new Vector3(1.56f, 1.46f, WallFront * 2f), concrete * shade);
                }
            Shapes.Box(wall.transform, new Vector3(0, WallHeight + 0.1f, -0.4f), new Vector3(40f, 0.2f, 2.6f), new Color(0.5f, 0.48f, 0.47f));
            var sand = new Color(0.78f, 0.68f, 0.48f);
            for (int i = -9; i <= 9; i++)
            {
                if (Mathf.Abs(i) < 1) continue;   // gap for the gunner
                Shapes.Make("Sandbag", MeshGen.Capsule(0.3f), Mat.Lit(sand * (0.95f + (i & 1) * 0.06f)), wall.transform,
                    new Vector3(i * 1.1f, WallHeight + 0.45f, WallFront - 0.35f), new Vector3(1.05f, 0.5f, 0.6f), Quaternion.Euler(0, 0, 90));
            }
            // hazard stripes on the wall face so the height reads at a glance
            for (int i = -6; i <= 6; i++)
                Shapes.Box(wall.transform, new Vector3(i * 0.9f, WallHeight - 0.25f, WallFront + 0.02f), new Vector3(0.45f, 0.35f, 0.04f),
                    i % 2 == 0 ? new Color(1f, 0.8f, 0.15f) : new Color(0.15f, 0.15f, 0.17f), 0.2f);

            // side buildings funnel the horde into the gate (invisible colliders + ruined facades)
            for (int s = -1; s <= 1; s += 2)
            {
                var side = new GameObject("SideBlock");
                side.transform.SetParent(t, false);
                side.layer = IgnoreRaycast;
                var sc = side.AddComponent<BoxCollider>();
                sc.center = new Vector3(s * (HalfWidth + 2f), 6f, 45f);
                sc.size = new Vector3(4f, 12f, 92f);
                for (int k = 0; k < 7; k++)
                {
                    float h = 6f + ((k * 5 + (s > 0 ? 2 : 0)) % 4) * 2.5f;
                    float z = 4f + k * 8.5f;
                    var col = Color.Lerp(new Color(0.3f, 0.29f, 0.33f), new Color(0.24f, 0.28f, 0.34f), (k % 3) / 2f);
                    var b = Shapes.Box(t, new Vector3(s * (HalfWidth + 3.5f), h / 2f, z), new Vector3(6f, h, 8f), col);
                    // dark windows
                    for (int wy = 1; wy < (int)(h / 2.2f); wy++)
                        for (int wz = -1; wz <= 1; wz++)
                            Shapes.Box(b.transform, new Vector3(-s * 0.505f, (wy * 2.2f - h / 2f) / h, wz * 0.3f), new Vector3(0.02f, 0.9f / h, 0.14f),
                                (wy + wz + k) % 4 == 0 ? new Color(1f, 0.75f, 0.35f) : new Color(0.16f, 0.14f, 0.18f), (wy + wz + k) % 4 == 0 ? 0.8f : 0f);
                    // broken top
                    Shapes.Make("Rubble", MeshGen.Pyramid(), Mat.Lit(col * 0.85f), t, new Vector3(s * (HalfWidth + 3.5f), h, z + 1.5f), new Vector3(3f, 1.5f, 3f), Quaternion.Euler(0, 20 * k, 0));
                }
            }

            // distant skyline in the fog
            for (int i = 0; i < 18; i++)
            {
                float x = -60f + i * 7f, h = 12f + (i * 37 % 11) * 2.2f;
                Shapes.Box(t, new Vector3(x, h / 2f, 95f + (i % 3) * 8f), new Vector3(6f, h, 6f), new Color(0.22f, 0.18f, 0.22f));
            }

            // street clutter: wrecked cars, burning barrels, cones
            WreckedCar(t, new Vector3(-2.8f, 0, 24f), 20f, new Color(0.7f, 0.25f, 0.2f));
            WreckedCar(t, new Vector3(3.1f, 0, 33f), -160f, new Color(0.25f, 0.45f, 0.7f));
            for (int i = 0; i < 4; i++) FireBarrel(t, new Vector3(i % 2 == 0 ? -HalfWidth + 0.4f : HalfWidth - 0.4f, 0, 8f + i * 9f));
            FireBarrel(t, new Vector3(-6f, WallHeight + 0.2f, -0.6f));
            FireBarrel(t, new Vector3(6f, WallHeight + 0.2f, -0.6f));
        }

        void WreckedCar(Transform t, Vector3 pos, float yaw, Color c)
        {
            var car = Shapes.Group("Car", t, pos);
            car.transform.localRotation = Quaternion.Euler(0, yaw, 4f);
            Shapes.Box(car.transform, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.7f, 4f), c);
            Shapes.Box(car.transform, new Vector3(0, 1.1f, -0.2f), new Vector3(1.6f, 0.55f, 2f), c * 0.85f);
            Shapes.Box(car.transform, new Vector3(0, 1.1f, -0.2f), new Vector3(1.62f, 0.4f, 1.6f), new Color(0.2f, 0.25f, 0.3f));
            for (int i = 0; i < 4; i++)
                Shapes.Make("Wheel", MeshGen.Cylinder(), Mat.Lit(new Color(0.12f, 0.12f, 0.13f)), car.transform,
                    new Vector3(i < 2 ? -0.95f : 0.95f, 0.35f, i % 2 == 0 ? 1.3f : -1.3f), new Vector3(0.7f, 0.25f, 0.7f), Quaternion.Euler(0, 0, 90));
            // cars are obstacles the horde flows around
            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.7f, 0);
            col.size = new Vector3(1.8f, 1.4f, 4f);
            car.layer = IgnoreRaycast;
        }

        void FireBarrel(Transform t, Vector3 pos)
        {
            var g = Shapes.Group("FireBarrel", t, pos);
            Shapes.Cylinder(g.transform, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1f, 0.7f), new Color(0.35f, 0.3f, 0.28f));
            var f = Shapes.Make("Flame", MeshGen.Cone(6), Mat.Lit(new Color(1f, 0.6f, 0.15f), 1f), g.transform, new Vector3(0, 1f, 0), new Vector3(0.55f, 0.9f, 0.55f));
            Shapes.Make("Core", MeshGen.Cone(6), Mat.Lit(new Color(1f, 0.92f, 0.5f), 1f), f.transform, new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.6f, 0.6f));
            flames.Add(f.transform);
        }

        void Update()
        {
            float tt = Time.time;
            for (int i = 0; i < flames.Count; i++)
            {
                float k = 1f + Mathf.Sin(tt * 13f + i * 1.7f) * 0.12f + Mathf.Sin(tt * 23f + i) * 0.08f;
                flames[i].localScale = new Vector3(0.55f / k, 0.9f * k, 0.55f / k);
            }
        }
    }
}
