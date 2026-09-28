using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// The Quaternius "Zombie Apocalypse Kit" (free, CC0) in Assets/Resources/ZombieKit/.
    /// The FBX files import at real size (metres) with the same orientation as the glTF versions, so a model
    /// is placed as-is: its own import transform is kept and only the holder is moved, turned and scaled.
    /// layout.json is written in glTF (right-handed) coordinates; Unity mirrors X on import, so layout
    /// positions and yaws are mirrored with LayoutPos / LayoutYaw to look exactly like the design preview.
    public static class Kit
    {
        [Serializable] public class ModelMeta { public string name; public float[] size, min, center; }
        [Serializable] public class Prop { public string m; public float[] p; public float r; public float s = 1f; }
        [Serializable] public class LayoutData { public float wallHeight, wallFront, halfWidth; public Prop[] props; public ModelMeta[] models; }

        /// Per-model correction, measured once: 'scale' only differs from 1 if the import scale is off,
        /// 'yaw' is 180 only if a character turns out to face -Z (it should face +Z like the glTF).
        struct Fix { public float scale, yaw; }

        static LayoutData layout;
        static bool loaded;
        static readonly Dictionary<string, ModelMeta> meta = new Dictionary<string, ModelMeta>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Fix> fixes = new Dictionary<string, Fix>();

        public static LayoutData Layout { get { Load(); return layout; } }
        public static bool Available { get { Load(); return layout != null && Prefab("Zombie_Basic") != null; } }

        /// Why the kit is (not) in use: shown on the menu and logged, so a missing import is obvious.
        public static string Status
        {
            get
            {
                Load();
                if (Resources.Load<TextAsset>("ZombieKit/layout") == null) return "models OFF: Assets/Resources/ZombieKit/layout.json missing (pull the latest version)";
                if (layout == null) return "models OFF: layout.json could not be read";
                if (Prefab("Zombie_Basic") == null) return "models OFF: ZombieKit/Characters/Zombie_Basic.fbx not imported";
                return "models ON";
            }
        }

        public static Vector3 LayoutPos(Prop p) { return new Vector3(-p.p[0], p.p[1], p.p[2]); }
        public static float LayoutYaw(Prop p) { return -p.r; }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var ta = Resources.Load<TextAsset>("ZombieKit/layout");
            if (ta == null) return;
            try { layout = JsonUtility.FromJson<LayoutData>(ta.text); }
            catch (Exception e) { Debug.LogWarning("ZombieKit layout unreadable: " + e.Message); layout = null; return; }
            foreach (var m in layout.models) meta[m.name] = m;
        }

        public static GameObject Prefab(string name)
        {
            GameObject go;
            if (prefabs.TryGetValue(name, out go)) return go;
            foreach (var folder in new[] { "Characters", "Environment", "Vehicles" })
            {
                go = Resources.Load<GameObject>("ZombieKit/" + folder + "/" + name);
                if (go != null) break;
            }
            prefabs[name] = go;
            return go;
        }

        /// World-space vertices of everything visible (skinned meshes baked in their current pose).
        static List<Vector3> Vertices(GameObject go)
        {
            var list = new List<Vector3>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var smr = r as SkinnedMeshRenderer;
                Mesh mesh = null;
                Matrix4x4 m;
                bool temp = false;
                if (smr != null)
                {
                    if (smr.sharedMesh == null) continue;
                    mesh = new Mesh(); temp = true;
                    smr.BakeMesh(mesh);   // scaled, but not rotated/translated
                    m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    mesh = mf.sharedMesh;
                    m = r.transform.localToWorldMatrix;
                }
                var vs = mesh.vertices;
                for (int i = 0; i < vs.Length; i++) list.Add(m.MultiplyPoint3x4(vs[i]));
                if (temp) UnityEngine.Object.DestroyImmediate(mesh);
            }
            return list;
        }

        static Bounds Measure(List<Vector3> vs)
        {
            if (vs.Count == 0) return new Bounds();
            var b = new Bounds(vs[0], Vector3.zero);
            for (int i = 1; i < vs.Count; i++) b.Encapsulate(vs[i]);
            return b;
        }

        static Fix GetFix(string name, GameObject prefab)
        {
            Fix f;
            if (fixes.TryGetValue(name, out f)) return f;
            f = new Fix { scale = 1f, yaw = 0f };
            ModelMeta m;
            if (!meta.TryGetValue(name, out m)) { fixes[name] = f; return f; }
            var tmp = UnityEngine.Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            Prepare(tmp);
            var vs = Vertices(tmp);
            if (vs.Count == 0)
            {
                // mesh data not readable (Read/Write off): trust the import as it is
                UnityEngine.Object.DestroyImmediate(tmp);
                Debug.LogWarning("ZombieKit " + name + ": mesh not readable, placed without checks (reimport Assets/Resources/ZombieKit)");
                fixes[name] = f;
                return f;
            }
            var b = Measure(vs);
            // the import should already be in metres: only correct it if it is clearly off
            float ratio = m.size[1] / Mathf.Max(0.0001f, b.size.y);
            if (m.size[1] > 0.2f && (ratio < 0.8f || ratio > 1.25f)) f.scale = ratio;
            if (IsCharacter(name))
            {
                // toes point forward: the lowest vertices sit in front of the shins
                float h = b.size.y, y0 = b.min.y, feet = 0f, shin = 0f; int nf = 0, ns = 0;
                foreach (var v in vs)
                {
                    float k = (v.y - y0) / Mathf.Max(0.0001f, h);
                    if (k < 0.06f) { feet += v.z; nf++; }
                    else if (k > 0.15f && k < 0.25f) { shin += v.z; ns++; }
                }
                if (nf > 0 && ns > 0 && feet / nf - shin / ns < -0.02f) f.yaw = 180f;
            }
            UnityEngine.Object.DestroyImmediate(tmp);
            if (f.scale != 1f || f.yaw != 0f)
                Debug.LogWarning("ZombieKit " + name + ": corrected import (scale x" + f.scale.ToString("0.###") + ", yaw " + f.yaw + "), measured " + b.size.ToString("0.00"));
            fixes[name] = f;
            return f;
        }

        static bool IsCharacter(string name) { return name.StartsWith("Zombie") || name.StartsWith("Characters"); }

        // characters come with every weapon attached: keep only the rifle
        static readonly string[] Weapons = { "Axe", "Guitar", "Knife", "Pistol", "Shotgun", "SMG", "Spear", "WoodenBat_Barbed", "WoodenBat_Saw" };

        static void Prepare(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(Weapons, t.name) >= 0) t.gameObject.SetActive(false);
        }

        /// Places a kit model: 'holder' gets position, yaw and scale; the model inside keeps its import transform.
        public static GameObject Place(string name, Vector3 pos, float yawDeg, float scale, Transform parent, bool shadows = true)
        {
            var prefab = Prefab(name);
            if (prefab == null) return null;
            var fix = GetFix(name, prefab);
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = pos;
            holder.transform.rotation = Quaternion.Euler(0f, yawDeg + fix.yaw, 0f);
            holder.transform.localScale = Vector3.one * scale * fix.scale;
            var go = UnityEngine.Object.Instantiate(prefab, holder.transform);
            go.name = "Model";
            Prepare(go);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                // bounds from the animated bones, so a zombie is never culled by stale import bounds
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
            }
            return holder;
        }

        public static Bounds BoundsOf(GameObject go) { return Measure(Vertices(go)); }

        public static float Height(string name)
        {
            Load();
            ModelMeta m;
            return meta.TryGetValue(name, out m) ? m.size[1] : 1.6f;
        }

        // ------------------------------------------------------------------ animation helpers (legacy clips)
        public class Anim
        {
            public Animation anim;
            public string run, climb, death, punch, idle, hit;
            string current;

            public static Anim From(GameObject model)
            {
                var a = new Anim { anim = model.GetComponentInChildren<Animation>() };
                if (a.anim == null) return a;
                foreach (AnimationState st in a.anim)
                {
                    string n = st.name.ToLowerInvariant();
                    if (n.Contains("__preview__")) continue;
                    if (n.EndsWith("run_arms")) a.run = st.name;
                    else if (a.run == null && n.EndsWith("run")) a.run = st.name;
                    else if (n.EndsWith("jump_idle")) a.climb = st.name;
                    else if (n.EndsWith("death")) a.death = st.name;
                    else if (n.EndsWith("punch") || (a.punch == null && n.EndsWith("run_attack"))) a.punch = st.name;
                    else if (n.EndsWith("idle_gun") || (a.idle == null && n.EndsWith("idle"))) a.idle = st.name;
                    else if (n.EndsWith("hitreact")) a.hit = st.name;
                }
                a.anim.cullingType = AnimationCullingType.AlwaysAnimate;
                return a;
            }

            public void Play(string clip, float fade = 0.15f, float speed = 1f)
            {
                if (anim == null || clip == null) return;
                if (current != clip) { anim.CrossFade(clip, fade); current = clip; }
                anim[clip].speed = speed;
            }

            public void Start(string clip)
            {
                if (anim == null || clip == null) return;
                anim.Play(clip);
                anim[clip].time = UnityEngine.Random.value * anim[clip].length;
                current = clip;
            }
        }
    }
}
