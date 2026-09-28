using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Optional real zombie models. Drop FBX files (e.g. Quaternius "Animated Zombie" pack, free CC0) into
    /// Assets/Resources/Zombies/. Every model found there is used at random, its clips are matched by name
    /// (run / walk / death / attack). With no models the game falls back to the built-in low-poly zombies.
    public static class ZombieModels
    {
        public static float ModelYaw = 180f;   // turn models to face the wall (-z); set 0 if they walk backwards
        static List<GameObject> models;

        public static bool Available { get { Load(); return models.Count > 0; } }

        static void Load()
        {
            if (models != null) return;
            models = new List<GameObject>();
            foreach (var go in Resources.LoadAll<GameObject>("Zombies"))
                if (go.GetComponentInChildren<SkinnedMeshRenderer>() != null || go.GetComponentInChildren<MeshRenderer>() != null) models.Add(go);
        }

        public class Rig
        {
            public Transform root, headBone;
            public Animation anim;
            public string run, walk, death, attack;

            public void Move(float speed)
            {
                if (anim == null) return;
                string clip = speed > 2.2f && run != null ? run : walk ?? run;
                if (clip == null) return;
                if (!anim.IsPlaying(clip)) anim.CrossFade(clip, 0.15f);
                anim[clip].speed = Mathf.Clamp(speed / (clip == run ? 4.5f : 1.6f), 0.6f, 1.8f);
            }

            public void Die()
            {
                if (anim == null) return;
                if (death != null) anim.CrossFade(death, 0.1f);
                else anim.Stop();
            }
        }

        public static Rig Spawn(Transform parent, float height)
        {
            Load();
            if (models.Count == 0) return null;
            var go = Object.Instantiate(models[Random.Range(0, models.Count)], parent);
            go.name = "Model";
            go.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            go.transform.localPosition = Vector3.zero;

            // normalize size and stand the feet on the bottom of the collider
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f) go.transform.localScale *= height / b.size.y;
                b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                go.transform.position += new Vector3(0f, parent.position.y - height * 0.5f - b.min.y, 0f);
                foreach (var r in rs)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null) smr.updateWhenOffscreen = false;
                }
            }

            var rig = new Rig { root = go.transform, anim = go.GetComponentInChildren<Animation>() };
            foreach (var t in go.GetComponentsInChildren<Transform>())
                if (rig.headBone == null && t.name.ToLowerInvariant().Contains("head")) rig.headBone = t;
            if (rig.anim != null)
            {
                foreach (AnimationState st in rig.anim)
                {
                    string n = st.name.ToLowerInvariant();
                    if (n.Contains("__preview__")) continue;
                    if (rig.run == null && (n.Contains("run") || n.Contains("sprint"))) rig.run = st.name;
                    else if (rig.walk == null && n.Contains("walk")) rig.walk = st.name;
                    else if (rig.death == null && (n.Contains("death") || n.Contains("die") || n.Contains("dead"))) rig.death = st.name;
                    else if (rig.attack == null && (n.Contains("attack") || n.Contains("punch") || n.Contains("bite"))) rig.attack = st.name;
                }
                rig.anim.cullingType = AnimationCullingType.BasedOnRenderers;
                var first = rig.run ?? rig.walk;
                if (first != null) { rig.anim.Play(first); rig.anim[first].time = Random.value * rig.anim[first].length; }
            }
            return rig;
        }
    }
}
