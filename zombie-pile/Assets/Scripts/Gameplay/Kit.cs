using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// The Quaternius "Zombie Apocalypse Kit" (free, CC0) in Assets/Resources/ZombieKit/.
    /// layout.json holds the street layout plus each model's reference size (measured from the glTF version),
    /// so every FBX is fitted to the same size, pivot and facing no matter how it was imported.
    public static class Kit
    {
        [Serializable] public class ModelMeta { public string name; public float[] size, min, center; }
        [Serializable] public class Prop { public string m; public float[] p; public float r; public float s = 1f; }
        [Serializable] public class LayoutData { public float wallHeight, wallFront, halfWidth; public Prop[] props; public ModelMeta[] models; }

        struct Fit { public float yaw, scale; public Vector3 offset; }

        static LayoutData layout;
        static bool loaded;
        static readonly Dictionary<string, ModelMeta> meta = new Dictionary<string, ModelMeta>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Fit> fits = new Dictionary<string, Fit>();

        public static LayoutData Layout { get { Load(); return layout; } }
        public static bool Available { get { Load(); return layout != null && Prefab("Zombie_Basic") != null; } }

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

        static Bounds Measure(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rs)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// Finds the yaw (0/90/180/270), uniform scale and offset that make the imported model match its glTF reference.
        static Fit GetFit(string name, GameObject prefab)
        {
            Fit f;
            if (fits.TryGetValue(name, out f)) return f;
            f = new Fit { yaw = 0f, scale = 1f, offset = Vector3.zero };
            ModelMeta m;
            if (!meta.TryGetValue(name, out m)) { fits[name] = f; return f; }
            var E = new Vector3(m.size[0], m.size[1], m.size[2]);
            var C = new Vector3(m.center[0], m.center[1], m.center[2]);
            var tmp = UnityEngine.Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero;
            Prepare(tmp);
            if (name.StartsWith("Zombie") || name.StartsWith("Characters"))
            {
                // characters: scale by height (reference T-pose includes all weapons, so only height is reliable),
                // and check the facing with the zombie run: the arms reach forward, so the hands must be at +z.
                var bb = Measure(tmp);
                f.scale = E.y / Mathf.Max(0.0001f, bb.size.y);
                f.offset = new Vector3(0f, m.min[1] - bb.min.y * f.scale, 0f);
                var an = Anim.From(tmp);
                Transform hips = null, la = null, ra = null;
                foreach (var t in tmp.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "Hips") hips = t;
                    else if (t.name == "LowerArm.L") la = t;
                    else if (t.name == "LowerArm.R") ra = t;
                }
                if (an.anim != null && an.run != null && hips != null && la != null && ra != null)
                {
                    an.anim.Play(an.run);
                    an.anim[an.run].time = an.anim[an.run].length * 0.3f;
                    an.anim.Sample();
                    var fwd = (la.position + ra.position) * 0.5f - hips.position;
                    if (fwd.z < 0f) f.yaw = 180f;
                }
                UnityEngine.Object.DestroyImmediate(tmp);
                fits[name] = f;
                return f;
            }
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                tmp.transform.rotation = Quaternion.Euler(0f, i * 90f, 0f);
                var b = Measure(tmp);
                float k = E.y > 0.3f ? E.y / Mathf.Max(0.0001f, b.size.y)
                                     : Mathf.Max(E.x, E.z) / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
                var s = b.size * k; var c = b.center * k;
                float score = Mathf.Abs(s.x - E.x) + Mathf.Abs(s.z - E.z) + Mathf.Abs(c.x - C.x) + Mathf.Abs(c.z - C.z);
                if (score < best - 0.001f)
                {
                    best = score;
                    f.yaw = i * 90f; f.scale = k;
                    f.offset = new Vector3(C.x - c.x, m.min[1] - b.min.y * k, C.z - c.z);
                }
            }
            UnityEngine.Object.DestroyImmediate(tmp);
            fits[name] = f;
            return f;
        }

        // characters come with every weapon attached: keep only the rifle
        static readonly string[] Weapons = { "Axe", "Guitar", "Knife", "Pistol", "Shotgun", "SMG", "Spear", "WoodenBat_Barbed", "WoodenBat_Saw" };

        static void Prepare(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (Array.IndexOf(Weapons, t.name) >= 0) t.gameObject.SetActive(false);
        }

        /// Places a kit model: 'holder' gets the layout transform, the model inside is fitted to the reference.
        public static GameObject Place(string name, Vector3 pos, float yawDeg, float scale, Transform parent, bool shadows = true)
        {
            var prefab = Prefab(name);
            if (prefab == null) return null;
            var fit = GetFit(name, prefab);
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = pos;
            holder.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            var go = UnityEngine.Object.Instantiate(prefab, holder.transform);
            go.name = "Model";
            Prepare(go);
            go.transform.localRotation = Quaternion.Euler(0f, fit.yaw, 0f);
            go.transform.localScale = go.transform.localScale * fit.scale * scale;
            go.transform.localPosition = fit.offset * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = false;
            }
            return holder;
        }

        public static Bounds BoundsOf(GameObject go) { return Measure(go); }

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
                a.anim.cullingType = AnimationCullingType.BasedOnRenderers;
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
