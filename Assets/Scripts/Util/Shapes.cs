using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// Cached materials for the two project shaders.
    public static class Mat
    {
        static Shader lit, clear;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader LitShader
        {
            get
            {
                if (lit == null) lit = Shader.Find("SkyDrop/Lit");
                return lit;
            }
        }

        static Shader ClearShader
        {
            get
            {
                if (clear == null) clear = Shader.Find("SkyDrop/Transparent");
                return clear;
            }
        }

        public static Material Lit(Color c, float emission = 0f)
        {
            string key = "L" + ColorUtility.ToHtmlStringRGB(c) + emission.ToString("0.00");
            Material m;
            if (!cache.TryGetValue(key, out m) || m == null)
            {
                m = new Material(LitShader) { name = key };
                m.SetColor("_Color", c);
                m.SetFloat("_Emission", emission);
                cache[key] = m;
            }
            return m;
        }

        public static Material Clear(Color c, float fog = 1f)
        {
            string key = "T" + ColorUtility.ToHtmlStringRGBA(c) + fog.ToString("0.00");
            Material m;
            if (!cache.TryGetValue(key, out m) || m == null)
            {
                m = new Material(ClearShader) { name = key };
                m.SetColor("_Color", c);
                m.SetFloat("_FogAmount", fog);
                cache[key] = m;
            }
            return m;
        }

        /// A material instance that is not shared (for colors animated at runtime).
        public static Material UniqueClear(Color c, float fog = 1f)
        {
            var m = new Material(ClearShader);
            m.SetColor("_Color", c);
            m.SetFloat("_FogAmount", fog);
            return m;
        }

        public static Material UniqueLit(Color c, float emission = 0f)
        {
            var m = new Material(LitShader);
            m.SetColor("_Color", c);
            m.SetFloat("_Emission", emission);
            return m;
        }
    }

    /// Helpers to spawn mesh objects without colliders.
    public static class Shapes
    {
        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent,
            Vector3 localPos, Vector3 scale, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent,
            Vector3 localPos, Vector3 scale)
        {
            return Make(name, mesh, mat, parent, localPos, scale, Quaternion.identity);
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color c, float emission = 0f)
        {
            return Make("Box", MeshGen.Box(), Mat.Lit(c, emission), parent, pos, scale);
        }

        public static GameObject Sphere(Transform parent, Vector3 pos, Vector3 scale, Color c, float emission = 0f)
        {
            return Make("Sphere", MeshGen.Sphere(), Mat.Lit(c, emission), parent, pos, scale);
        }

        public static GameObject Cylinder(Transform parent, Vector3 pos, Vector3 scale, Color c, float emission = 0f)
        {
            return Make("Cylinder", MeshGen.Cylinder(), Mat.Lit(c, emission), parent, pos, scale);
        }

        public static GameObject Cone(Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            return Make("Cone", MeshGen.Cone(), Mat.Lit(c), parent, pos, scale);
        }

        public static GameObject Group(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }
    }

    /// Global lighting + fog values read by the project shaders.
    public static class SkyEnv
    {
        public static Color FogColor { get; private set; }

        public static void Apply(Color sky, Color lightColor, Color ambientSky, Color ambientGround)
        {
            FogColor = sky;
            Shader.SetGlobalVector("_SD_LightDir", new Vector4(0.35f, 0.85f, -0.4f, 0f));
            Shader.SetGlobalColor("_SD_LightColor", lightColor);
            Shader.SetGlobalColor("_SD_AmbientSky", ambientSky);
            Shader.SetGlobalColor("_SD_AmbientGround", ambientGround);
            Shader.SetGlobalColor("_SD_FogColor", sky);
            SetFog(120f, 900f);

            // Gradient sky (horizon matches the fog so distant things melt into it).
            if (skyMat == null)
            {
                var sh = Shader.Find("SkyDrop/Sky");
                if (sh != null) skyMat = new Material(sh);
            }
            if (skyMat != null)
            {
                float h, s, v;
                Color.RGBToHSV(sky, out h, out s, out v);
                skyMat.SetColor("_Top", Color.HSVToRGB(h, Mathf.Clamp01(s + 0.25f), v * 0.8f));
                skyMat.SetColor("_Horizon", sky);
                skyMat.SetColor("_Bottom", Color.Lerp(sky, ambientGround, 0.4f));
                RenderSettings.skybox = skyMat;
                if (Camera.main != null) Camera.main.clearFlags = CameraClearFlags.Skybox;
            }

            // Imported models (e.g. a Mixamo character) use Unity's Standard shader:
            // give them a matching sun + ambient so they sit in the same world.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(ambientSky, ambientGround, 0.3f) * 1.1f;
            if (sun == null)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.None;
            }
            sun.color = lightColor;
            sun.intensity = 1.05f;
            sun.transform.rotation = Quaternion.LookRotation(-new Vector3(0.35f, 0.85f, -0.4f));
        }

        static Material skyMat;
        static Light sun;

        public static void SetFog(float start, float end)
        {
            Shader.SetGlobalVector("_SD_FogParams", new Vector4(start, end, 0f, 0f));
        }

        public static void TintFog(Color c)
        {
            Shader.SetGlobalColor("_SD_FogColor", c);
        }
    }
}
