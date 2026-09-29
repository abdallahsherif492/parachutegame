using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiePile
{
    /// The battlefield from layout.json: the container wall, the street between two container walls, the two
    /// shooter towers, the safe-zone camp behind the wall, the ruined city around the street and the burning
    /// skyline at its end. Zombies come from +z and pile up against the wall face at z = WallFront. Nothing
    /// solid stands in the street: the horde only meets the wall and each other.
    /// SetTheme() re-colours everything for the level's place (sky, light, fog, containers, buildings,
    /// fires, weather, and the props scattered beside the street).
    public class Arena : MonoBehaviour
    {
        public static float WallHeight = 5.2f;
        public static float WallFront = 0f;        // z of the face the zombies climb
        public static float HalfWidth = 4f;        // zombies stay in |x| < HalfWidth
        public static float SpawnZ = 50f;          // at the far end of the street, in the haze
        public static Vector3 TowerSpot = new Vector3(-7.4f, 7.8f, 14f);   // the left tower; the right one mirrors x
        public static Vector3 CampSpot = new Vector3(0f, 0f, -10.5f);
        public const int IgnoreRaycast = 2;        // built-in layer: bullets pass through, corpses collide
        public const int Props = 1;                // built-in "TransparentFX": bullets hit these, nothing else does

        public static Arena I;
        readonly List<ExplosiveBarrel> drums = new List<ExplosiveBarrel>();
        class Container { public Renderer[] r; public bool red; }
        readonly List<Container> containers = new List<Container>();
        class Building { public Renderer r; public int seed; }
        readonly List<Building> buildings = new List<Building>();
        readonly List<GameObject> fires = new List<GameObject>();
        Transform decor;
        Material groundMat;
        Flame campfire;
        MaterialPropertyBlock mpb;

        public static Arena Build()
        {
            var a = new GameObject("Arena").AddComponent<Arena>();
            I = a;
            for (int l = 0; l < 32; l++) Physics.IgnoreLayerCollision(Props, l, true);
            Physics.IgnoreLayerCollision(Zombie.Layer, Zombie.Layer, true);
            Physics.IgnoreLayerCollision(Zombie.Layer, IgnoreRaycast, true);
            Physics.IgnoreLayerCollision(Zombie.Layer, 0, true);
            Ragdoll.Setup();
            if (Kit.Available) a.MakeFromKit();
            else Debug.LogError("Zombie Pile: " + Kit.Status);
            Colliders(a.transform);
            return a;
        }

        void MakeFromKit()
        {
            var L = Kit.Layout;
            mpb = new MaterialPropertyBlock();
            WallHeight = L.wallHeight; WallFront = L.wallFront; HalfWidth = L.halfWidth;
            if (L.tower != null && L.tower.Length == 3) TowerSpot = new Vector3(-L.tower[0], L.tower[1], L.tower[2]);
            if (L.camp != null && L.camp.Length == 3) CampSpot = new Vector3(-L.camp[0], 0f, L.camp[2]);
            var t = transform;
            foreach (var p in L.props)
            {
                var pos = Kit.LayoutPos(p);
                bool small = p.m.StartsWith("Blood") || p.m.StartsWith("Street");
                var go = Kit.Place(p.m, pos, Kit.LayoutYaw(p), p.s <= 0f ? 1f : p.s, t, !small);
                if (go == null) continue;
                if (p.m.StartsWith("Container_")) containers.Add(new Container { r = go.GetComponentsInChildren<Renderer>(), red = p.m == "Container_Red" });
                bool inLane = pos.y < 0.1f && Mathf.Abs(pos.x) < HalfWidth && pos.z > WallFront + 0.5f;
                if (inLane && p.m == "Barrel") { AddBox(go, Props); drums.Add(go.AddComponent<ExplosiveBarrel>()); }
            }

            // ground under everything
            groundMat = new Material(Shader.Find("Standard")) { color = new Color(0.3f, 0.25f, 0.24f) };
            groundMat.SetFloat("_Glossiness", 0.05f);
            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground";
            g.transform.SetParent(t, false);
            g.transform.position = new Vector3(0f, -0.02f, 60f);
            g.transform.localScale = new Vector3(40f, 1f, 40f);
            g.GetComponent<Renderer>().sharedMaterial = groundMat;
            Destroy(g.GetComponent<Collider>());

            BuildCity(L);
            BuildCamp(L);
            WeatherFx.Build(t);
        }

        // ------------------------------------------------------------------ the ruined city
        void BuildCity(Kit.LayoutData L)
        {
            if (L.city == null) return;
            var root = new GameObject("City").transform;
            root.SetParent(transform, false);
            var sh = Shader.Find("ZombiePile/Facade");
            var mat = sh != null ? new Material(sh) : Mat.Lit(new Color(0.3f, 0.26f, 0.3f));
            foreach (var b in L.city)
            {
                var pivot = new GameObject("Building").transform;
                pivot.SetParent(root, false);
                pivot.position = new Vector3(-b.x, 0f, b.z);
                pivot.rotation = Quaternion.Euler(0f, 0f, -b.t);
                var box = Shapes.Make("Block", MeshGen.Box(), mat, pivot, new Vector3(0f, b.h * 0.5f, 0f), new Vector3(b.w, b.h, b.d));
                buildings.Add(new Building { r = box.GetComponent<Renderer>(), seed = b.s });
            }
            // fires in the distance, with smoke above them
            foreach (var f in L.fires)
            {
                var pos = new Vector3(-f.x, f.y, f.z);
                var fl = Flame.Make(root, pos, f.s, false, false);
                SmokeColumn.Make(fl.transform, pos, f.s);
                fires.Add(fl.gameObject);
            }
        }

        // ------------------------------------------------------------------ the safe zone
        void BuildCamp(Kit.LayoutData L)
        {
            Survivors.Build(transform, L.survivors);
            campfire = Flame.Make(transform, CampSpot + Vector3.up * 0.1f, 0.9f, true, true);

            // the sign on the back of the wall: what all of this is for
            var go = new GameObject("HavenSign", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(800f, 260f);
            rt.position = new Vector3(0f, 3.1f, WallFront - 2.64f);
            rt.localScale = Vector3.one * 0.01f;
            var board = UIKit.Image(rt, "Board", new Color(0.07f, 0.11f, 0.19f, 0.96f), UIKit.Round);
            UIKit.Stretch(board.rectTransform);
            var edge = board.gameObject.AddComponent<Outline>();
            edge.effectColor = UIKit.Gold;
            edge.effectDistance = new Vector2(6f, -6f);
            var title = UIKit.Title(rt, "NEW HAVEN", 170, UIKit.Gold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(780f, 190f));
            var sub = UIKit.Text(rt, "SAFE ZONE  ·  2,400 SURVIVORS", 46, Color.white);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(780f, 56f));
        }

        // ------------------------------------------------------------------ themes
        /// Re-dresses the whole battlefield for a place. Gameplay geometry never changes.
        public void SetTheme(int index)
        {
            var t = Theme.All[index];
            Theme.Apply(index);
            if (groundMat != null) groundMat.color = t.ground;
            foreach (var c in containers)
            {
                var col = c.red ? t.tintRed : t.tintGreen;
                foreach (var r in c.r)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_Color", col);
                    r.SetPropertyBlock(mpb);
                }
            }
            foreach (var b in buildings)
            {
                float j = 0.85f + 0.3f * Frac(b.seed * 0.618f);
                b.r.GetPropertyBlock(mpb);
                mpb.SetColor("_WallColor", new Color(t.wall.r * j, t.wall.g * j, t.wall.b * j, 1f));
                mpb.SetColor("_WinLit", t.win);
                mpb.SetColor("_WinDark", new Color(t.wall.r * 0.22f, t.wall.g * 0.22f, t.wall.b * 0.25f, 1f));
                mpb.SetFloat("_Lit", Mathf.Clamp01(t.lit * (0.4f + 1.2f * Frac(b.seed * 0.37f))));
                mpb.SetFloat("_Seed", b.seed % 97);
                mpb.SetFloat("_Glow", t.glow);
                b.r.SetPropertyBlock(mpb);
            }
            for (int i = 0; i < fires.Count; i++) fires[i].SetActive(i < t.fires);
            if (campfire != null) campfire.Retune();
            if (WeatherFx.I != null) WeatherFx.I.Set(t.weather);
            BuildDecor(index);
        }

        static float Frac(float v) { return v - Mathf.Floor(v); }

        /// Wrecks, barricades and junk beside the street: a different arrangement for every place.
        void BuildDecor(int index)
        {
            if (decor != null) Destroy(decor.gameObject);
            decor = new GameObject("Decor").transform;
            decor.SetParent(transform, false);
            var t = Theme.All[index];
            var rng = new System.Random(4242 + index * 97);
            var lastZ = new[] { 32f, 32f };
            for (int i = 0; i < 18; i++)
            {
                int si = i % 2;
                float side = si == 0 ? 1f : -1f;
                string m = t.decor[rng.Next(t.decor.Length)];
                bool car = m.StartsWith("Vehicle");
                lastZ[si] += (car ? 7f : 3f) + (float)rng.NextDouble() * 5f;
                if (lastZ[si] > 88f) continue;
                float x = side * (8.6f + (float)rng.NextDouble() * (car ? 2.4f : 3.6f));
                float yaw = car ? (rng.NextDouble() < 0.5 ? 0f : 180f) + (float)(rng.NextDouble() * 40.0 - 20.0) : (float)(rng.NextDouble() * 360.0);
                Kit.Place(m, new Vector3(x, 0f, lastZ[si]), yaw, 1f, decor, true);
            }
        }

        /// New level: the oil drums on the street are back.
        public void ResetDrums() { foreach (var d in drums) if (d != null) d.Restore(); }

        static void AddBox(GameObject go, int layer)
        {
            // measure in the holder's own frame (holders are only rotated about Y)
            var rot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;
            var b = Kit.BoundsOf(go);
            if (b.size.sqrMagnitude < 0.01f) b = new Bounds(go.transform.position + Vector3.up * 0.57f, new Vector3(0.7f, 1.15f, 0.7f));   // a drum
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = new Vector3(Mathf.Max(0.4f, b.size.x), Mathf.Max(0.4f, b.size.y), Mathf.Max(0.4f, b.size.z)) * 1.1f;
            go.transform.rotation = rot;
            go.layer = layer;
        }

        /// Invisible physics for the ragdolls: the ground, the wall (tall, so nothing is ever thrown over it
        /// onto the shooters) and the sides of the street. Bullets pass through all of it but the ground.
        static void Colliders(Transform t)
        {
            var ground = new GameObject("GroundCollider");
            ground.transform.SetParent(t, false);
            var gc = ground.AddComponent<BoxCollider>();
            gc.center = new Vector3(0, -0.5f, 40);
            gc.size = new Vector3(160, 1, 200);
            Box(t, "WallCollider", new Vector3(0f, 15f, WallFront - 1.3f), new Vector3(40f, 30f, 2.6f));
            for (int s = -1; s <= 1; s += 2)
                Box(t, "SideCollider", new Vector3(s * (HalfWidth + 2f), 15f, 45f), new Vector3(4f, 30f, 110f));
            Box(t, "FarCollider", new Vector3(0f, 15f, SpawnZ + 20f), new Vector3(40f, 30f, 2f));
        }

        static void Box(Transform t, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.layer = IgnoreRaycast;
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }
    }
}
