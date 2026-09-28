using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// The battlefield from layout.json: the container wall, the street between two container walls, the
    /// sniper tower, and invisible colliders. Zombies come from +z and pile up against the wall face at
    /// z = WallFront. Nothing solid stands in the street: the horde only meets the wall and each other.
    public class Arena : MonoBehaviour
    {
        public static float WallHeight = 5.2f;
        public static float WallFront = 0f;        // z of the face the zombies climb
        public static float HalfWidth = 4f;        // zombies stay in |x| < HalfWidth
        public static float SpawnZ = 50f;          // at the far end of the street, in the haze
        public static Vector3 TowerSpot = new Vector3(-7.4f, 7.8f, 14f);
        public const int IgnoreRaycast = 2;        // built-in layer: bullets pass through, corpses collide
        public const int Props = 1;                // built-in "TransparentFX": bullets hit these, nothing else does

        public static Arena I;
        readonly List<ExplosiveBarrel> drums = new List<ExplosiveBarrel>();

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
            WallHeight = L.wallHeight; WallFront = L.wallFront; HalfWidth = L.halfWidth;
            if (L.tower != null && L.tower.Length == 3) TowerSpot = new Vector3(-L.tower[0], L.tower[1], L.tower[2]);
            var t = transform;
            foreach (var p in L.props)
            {
                var pos = Kit.LayoutPos(p);
                bool small = p.m.StartsWith("Blood") || p.m.StartsWith("Street");
                var go = Kit.Place(p.m, pos, Kit.LayoutYaw(p), p.s <= 0f ? 1f : p.s, t, !small);
                if (go == null) continue;
                bool inLane = pos.y < 0.1f && Mathf.Abs(pos.x) < HalfWidth && pos.z > WallFront + 0.5f;
                if (inLane && p.m == "Barrel") { AddBox(go, Props); drums.Add(go.AddComponent<ExplosiveBarrel>()); }
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
