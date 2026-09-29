using UnityEngine;

namespace ZombiePile
{
    /// Builds the whole game at startup: the scene only holds a camera.
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Game.I != null) return;
            Application.targetFrameRate = 60;

            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            if (cam.GetComponent<CameraRig>() == null) cam.gameObject.AddComponent<CameraRig>();

            // The project shaders light themselves; scene lights and fog are not needed.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            RenderSettings.fog = false;

            // Snappier, more stable stacking for the zombie pile.
            Physics.gravity = new Vector3(0f, -22f, 0f);
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.bounceThreshold = 3f;

            // Dusk over a ruined city.
            // blood-orange apocalypse sunset
            var sky = new Color(0.91f, 0.53f, 0.35f);
            SkyEnv.Apply(sky, new Color(1f, 0.9f, 0.78f), new Color(0.55f, 0.48f, 0.55f), new Color(0.22f, 0.17f, 0.16f));
            SkyEnv.SetFog(32f, 70f);
            if (Kit.Available)
            {
                // real models use Unity's lighting: soft shadows (the sun, ambient and fog are set per level by Theme)
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowDistance = 55f;
                QualitySettings.shadowCascades = 2;
                QualitySettings.pixelLightCount = 4;
            }

            Save.Load();
            Debug.Log("Zombie Pile " + Game.Version + " | " + Kit.Status);
            new GameObject("Sound").AddComponent<SoundBank>();
            new GameObject("Fx").AddComponent<Fx>();
            new GameObject("Game").AddComponent<Game>();
        }
    }
}
