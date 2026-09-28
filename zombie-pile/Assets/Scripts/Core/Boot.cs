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
            // blood-orange apocalypse sunset, dark streets
            SkyEnv.Apply(new Color(0.93f, 0.45f, 0.26f), new Color(1f, 0.82f, 0.6f),
                new Color(0.5f, 0.45f, 0.55f), new Color(0.2f, 0.15f, 0.14f));
            SkyEnv.SetFog(22f, 75f);

            Save.Load();
            new GameObject("Sound").AddComponent<SoundBank>();
            new GameObject("Fx").AddComponent<Fx>();
            new GameObject("Game").AddComponent<Game>();
        }
    }
}
