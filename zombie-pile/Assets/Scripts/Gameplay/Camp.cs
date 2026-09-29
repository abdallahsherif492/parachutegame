using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// The people we are protecting: a camp behind the wall with a fire, survivors and a sign, and a few
    /// militia on top of the wall. They react to the fight: calm, panic when the wall is failing, cheer
    /// when a level is won, scatter when the horde pours over.
    public class Survivors : MonoBehaviour
    {
        public static Survivors I;
        public enum Mood { Calm, Panic, Cheer }

        class S
        {
            public GameObject go; public Kit.Anim anim; public bool guard;
            public Vector3 home; public float t, yaw; public Vector3 run;
        }

        readonly List<S> list = new List<S>();
        Mood mood;
        float flinchT;

        public static Survivors Build(Transform parent, Kit.Surv[] data)
        {
            var go = new GameObject("Survivors");
            go.transform.SetParent(parent, false);
            var me = go.AddComponent<Survivors>();
            I = me;
            if (data == null) return me;
            foreach (var d in data)
            {
                var pos = new Vector3(-d.p[0], d.p[1], d.p[2]);
                var h = Kit.Place(d.m, pos, -d.r, 1f, go.transform, true);
                if (h == null) continue;
                bool guard = d.a == "Idle_Gun";
                Kit.ShowWeapon(h, guard ? "Pistol" : "None");
                var s = new S { go = h, anim = Kit.Anim.From(h), guard = guard, home = pos, yaw = -d.r, t = Random.Range(0f, 6f) };
                if (s.anim.anim != null)
                {
                    foreach (var c in new[] { s.anim.wave, s.anim.calm, s.anim.idle, s.anim.yes }) if (c != null) s.anim.anim[c].wrapMode = WrapMode.Loop;
                    if (s.anim.duck != null) s.anim.anim[s.anim.duck].wrapMode = WrapMode.ClampForever;
                }
                Base(s, true);
                me.list.Add(s);
            }
            return me;
        }

        static void Base(S s, bool randomStart)
        {
            var a = s.anim;
            string clip = s.guard ? a.idle : (a.calm ?? a.idle);
            if (clip == null) return;
            if (randomStart) a.Start(clip); else a.Play(clip, 0.25f);
        }

        /// Network codes: 0 calm, 1 panic, 2 cheer, 3 flinch, 4 scatter.
        public void Apply(int code)
        {
            switch (code)
            {
                case 0: SetMood(Mood.Calm, false); break;
                case 1: SetMood(Mood.Panic, false); break;
                case 2: SetMood(Mood.Cheer, false); break;
                case 3: Flinch(false); break;
                case 4: Scatter(false); break;
            }
        }

        public void SetMood(Mood m) { SetMood(m, true); }

        void SetMood(Mood m, bool broadcast)
        {
            if (m == mood) return;
            if (broadcast && Net.IsHost) Net.Host.Mood((int)m);
            mood = m;
            foreach (var s in list)
            {
                var a = s.anim;
                if (s.guard) continue;
                if (m == Mood.Panic && a.duck != null) a.Play(a.duck, 0.2f);
                else if (m == Mood.Cheer && (a.wave ?? a.yes) != null) a.Play(a.wave ?? a.yes, 0.2f);
                else Base(s, false);
            }
        }

        /// A wall breach: everyone flinches for a moment.
        public void Flinch() { Flinch(true); }

        void Flinch(bool broadcast)
        {
            if (broadcast && Net.IsHost) Net.Host.Mood(3);
            flinchT = 0.8f; foreach (var s in list) if (s.anim.hit != null) s.anim.Play(s.anim.hit, 0.05f); }

        /// The horde is over the wall: the camp runs.
        public void Scatter() { Scatter(true); }

        void Scatter(bool broadcast)
        {
            if (broadcast && Net.IsHost) Net.Host.Mood(4);
            mood = Mood.Panic;
            foreach (var s in list)
            {
                if (s.anim.run == null) continue;
                var away = (s.home - new Vector3(0f, 0f, -3f)); away.y = 0f;
                s.run = (away.sqrMagnitude < 0.01f ? Random.insideUnitSphere : away.normalized) * Random.Range(3f, 4.5f);
                s.run.y = 0f;
                s.anim.Play(s.anim.run, 0.1f, 1.2f);
                s.go.transform.rotation = Quaternion.LookRotation(s.run);
            }
        }

        public void Reset()
        {
            mood = Mood.Calm; flinchT = 0f;
            foreach (var s in list)
            {
                s.run = Vector3.zero;
                s.go.transform.position = s.home;
                s.go.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                Base(s, true);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (flinchT > 0f)
            {
                flinchT -= dt;
                if (flinchT <= 0f) { var m = mood; mood = (Mood)(-1); SetMood(m, false); }
            }
            foreach (var s in list)
            {
                if (s.run.sqrMagnitude > 0f)
                {
                    var p = s.go.transform.position + s.run * dt;
                    p.x = Mathf.Clamp(p.x, -16f, 16f); p.z = Mathf.Clamp(p.z, -26f, -4f);
                    s.go.transform.position = p;
                }
            }
        }
    }

    /// A flickering flame: a few cones, a glow, and (optionally) a point light and smoke.
    public class Flame : MonoBehaviour
    {
        Transform[] cones;
        Vector3[] baseScale;
        Transform glow;
        Light lamp;
        float seed, baseLight, smokeT, size = 1f;
        bool smoke;

        public static Flame Make(Transform parent, Vector3 pos, float size, bool light, bool smoke)
        {
            var go = new GameObject(light ? "Campfire" : "Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var f = go.AddComponent<Flame>();
            f.size = size; f.smoke = smoke;
            f.seed = Random.value * 50f;
            var outer = Mat.Lit(new Color(1f, 0.45f, 0.1f), 1f);
            var inner = Mat.Lit(new Color(1f, 0.85f, 0.35f), 1f);
            f.cones = new Transform[3];
            f.baseScale = new Vector3[3];
            var scales = new[] { new Vector3(0.9f, 1.7f, 0.9f), new Vector3(0.6f, 1.3f, 0.6f), new Vector3(0.75f, 1.1f, 0.75f) };
            var offs = new[] { Vector3.zero, new Vector3(0.28f, 0f, 0.12f), new Vector3(-0.25f, 0f, -0.15f) };
            for (int i = 0; i < 3; i++)
            {
                var c = Shapes.Make("Flame", MeshGen.Cone(), i == 0 ? outer : inner, go.transform, offs[i] * size + Vector3.up * scales[i].y * 0.5f * size, scales[i] * size);
                f.cones[i] = c.transform;
                f.baseScale[i] = scales[i] * size;
            }
            var halo = Shapes.Make("Glow", MeshGen.Sphere(), Mat.Clear(new Color(1f, 0.55f, 0.2f, 0.22f), 0.3f), go.transform, Vector3.up * size, Vector3.one * 3.2f * size);
            f.glow = halo.transform;
            if (light)
            {
                var l = new GameObject("Light");
                l.transform.SetParent(go.transform, false);
                l.transform.localPosition = Vector3.up * 1.4f;
                f.lamp = l.AddComponent<Light>();
                f.lamp.type = LightType.Point;
                f.lamp.color = new Color(1f, 0.62f, 0.28f);
                f.lamp.range = 15f;
                f.lamp.shadows = LightShadows.None;
                f.baseLight = 2.2f * Theme.Def.campLight;
                f.lamp.intensity = f.baseLight;
            }
            return f;
        }

        public void Retune() { if (lamp != null) baseLight = 2.2f * Theme.Def.campLight; }

        void Update()
        {
            float t = Time.time + seed;
            for (int i = 0; i < cones.Length; i++)
            {
                float k = 0.75f + 0.5f * Mathf.PerlinNoise(t * 7f + i * 9.1f, i);
                cones[i].localScale = new Vector3(baseScale[i].x * (0.9f + 0.2f * k), baseScale[i].y * k, baseScale[i].z * (0.9f + 0.2f * k));
                cones[i].localPosition = new Vector3(cones[i].localPosition.x, baseScale[i].y * k * 0.5f, cones[i].localPosition.z);
            }
            glow.localScale = Vector3.one * (3.2f + 0.5f * Mathf.PerlinNoise(t * 5f, 3f)) * size;
            if (lamp != null) lamp.intensity = baseLight * (0.8f + 0.4f * Mathf.PerlinNoise(t * 9f, 7f));
            if (smoke)
            {
                smokeT -= Time.deltaTime;
                if (smokeT <= 0f && Fx.I != null) { smokeT = 0.5f; Fx.I.Smoke(transform.position + Vector3.up * 1.8f * size, 0.7f * size); }
            }
        }
    }

    /// Smoke columns above the fires in the distance (slowly drifting translucent cylinders).
    public class SmokeColumn : MonoBehaviour
    {
        float seed;
        Vector3 baseScale;
        public static SmokeColumn Make(Transform parent, Vector3 pos, float size)
        {
            var go = Shapes.Make("Smoke", MeshGen.Cylinder(10), Mat.Clear(new Color(0.16f, 0.14f, 0.15f, 0.5f), 0.5f), parent, pos + Vector3.up * 13f * size, new Vector3(3.4f * size, 13f * size, 3.4f * size));
            var s = go.AddComponent<SmokeColumn>();
            s.seed = Random.value * 20f;
            s.baseScale = go.transform.localScale;
            return s;
        }
        void Update()
        {
            float t = Time.time * 0.2f + seed;
            transform.localScale = new Vector3(baseScale.x * (1f + 0.15f * Mathf.Sin(t)), baseScale.y, baseScale.z * (1f + 0.15f * Mathf.Cos(t * 1.3f)));
            transform.Rotate(0f, 6f * Time.deltaTime, 0f);
        }
    }

    /// Snow, spores or embers drifting through the scene (one particle system, retuned per theme).
    public class WeatherFx : MonoBehaviour
    {
        public static WeatherFx I;
        ParticleSystem ps;
        ParticleSystemRenderer psr;

        public static WeatherFx Build(Transform parent)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeatherFx>();
            I = w;
            w.ps = go.GetComponent<ParticleSystem>();
            w.psr = go.GetComponent<ParticleSystemRenderer>();
            w.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return w;
        }

        public void Set(Weather kind)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (kind == Weather.None) return;
            Color c; float size, rate, life, vy, drift, y;
            switch (kind)
            {
                case Weather.Snow: c = new Color(1f, 1f, 1f, 0.9f); size = 0.11f; rate = 110f; life = 6.5f; vy = -2.4f; drift = 0.7f; y = 14f; break;
                case Weather.Spores: c = new Color(0.75f, 1f, 0.35f, 0.4f); size = 0.22f; rate = 40f; life = 9f; vy = 0.15f; drift = 0.5f; y = 1.5f; break;
                default: c = new Color(1f, 0.55f, 0.15f, 0.95f); size = 0.09f; rate = 55f; life = 6f; vy = 2.6f; drift = 0.8f; y = 0.5f; break;
            }
            transform.position = new Vector3(0f, y, 26f);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 900;
            main.startLifetime = life;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(34f, 0.5f, 70f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            vel.y = new ParticleSystem.MinMaxCurve(vy * 0.8f, vy * 1.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-drift, drift);
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = Mat.Clear(c, 0f);
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play(true);
        }
    }
}
