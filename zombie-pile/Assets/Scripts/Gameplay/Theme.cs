using UnityEngine;

namespace ZombiePile
{
    public enum Weather { None, Snow, Spores, Embers }

    /// One place the horde can attack from: sky, light, fog, colours, fires, weather and the props scattered
    /// beside the street. Level n takes theme (n - 1) % Count, so every level looks and feels different.
    public class ThemeDef
    {
        public string name, line;
        public Color sky, sun, ambSky, ambEq, ambGround, ground, tintRed, tintGreen, wall, win;
        public float sunI = 1.25f, fogStart = 32f, fogEnd = 100f, lit = 0.15f, glow = 0.2f, campLight = 1.2f;
        public Vector3 sunDir = new Vector3(12f, -22f, -4f);
        public int fires = 5;
        public Weather weather;
        public string[] decor;
    }

    public static class Theme
    {
        public static readonly ThemeDef[] All =
        {
            new ThemeDef
            {
                name = "HIGHWAY 9", line = "The horde follows the road to Haven.",
                sky = new Color(0.91f, 0.53f, 0.35f), sun = new Color(1f, 0.93f, 0.85f), sunI = 1.25f,
                ambSky = new Color(0.95f, 0.78f, 0.66f), ambEq = new Color(0.6f, 0.46f, 0.42f), ambGround = new Color(0.24f, 0.2f, 0.22f),
                ground = new Color(0.30f, 0.25f, 0.24f), tintRed = Color.white, tintGreen = Color.white,
                wall = new Color(0.42f, 0.34f, 0.38f), win = new Color(1f, 0.76f, 0.36f), lit = 0.15f, glow = 0.2f, fires = 5,
                decor = new[] { "Vehicle_Pickup", "Vehicle_Sports", "Barrel", "TrashBag_2", "PlasticBarrier", "TrafficCone_1" },
            },
            new ThemeDef
            {
                name = "DOWNTOWN AT NIGHT", line = "The lights draw them in.",
                sky = new Color(0.07f, 0.09f, 0.2f), sun = new Color(0.55f, 0.65f, 1f), sunI = 1.05f,
                ambSky = new Color(0.42f, 0.5f, 0.8f), ambEq = new Color(0.3f, 0.35f, 0.56f), ambGround = new Color(0.16f, 0.18f, 0.28f),
                ground = new Color(0.13f, 0.14f, 0.2f), tintRed = new Color(0.9f, 0.78f, 1f), tintGreen = new Color(0.75f, 0.9f, 1.05f),
                wall = new Color(0.22f, 0.26f, 0.42f), win = new Color(1f, 0.82f, 0.45f), lit = 0.55f, glow = 0.9f, fires = 6, campLight = 2.4f,
                fogStart = 30f, fogEnd = 90f, sunDir = new Vector3(40f, -35f, 10f),
                decor = new[] { "TrafficCone_1", "TrafficBarrier_1", "Vehicle_Pickup", "Wheels_Stack", "TrashBag_1", "PlasticBarrier" },
            },
            new ThemeDef
            {
                name = "TOXIC DISTRICT", line = "Something in the air makes them worse.",
                sky = new Color(0.55f, 0.68f, 0.28f), sun = new Color(0.85f, 1f, 0.6f), sunI = 1.15f,
                ambSky = new Color(0.72f, 0.85f, 0.5f), ambEq = new Color(0.45f, 0.55f, 0.35f), ambGround = new Color(0.2f, 0.26f, 0.18f),
                ground = new Color(0.22f, 0.28f, 0.17f), tintRed = new Color(0.9f, 1.15f, 0.6f), tintGreen = new Color(0.7f, 1.15f, 0.75f),
                wall = new Color(0.28f, 0.36f, 0.26f), win = new Color(0.8f, 1f, 0.4f), lit = 0.12f, glow = 0.4f, fires = 3, weather = Weather.Spores,
                fogStart = 24f, fogEnd = 80f, sunDir = new Vector3(20f, -30f, 5f),
                decor = new[] { "Barrel", "Barrel", "TrashBag_1", "TrashBag_2", "Pipes", "Wheels_Stack" },
            },
            new ThemeDef
            {
                name = "THE BURNING QUARTER", line = "The fires drive them toward the wall.",
                sky = new Color(0.78f, 0.24f, 0.14f), sun = new Color(1f, 0.6f, 0.4f), sunI = 1.2f,
                ambSky = new Color(0.9f, 0.5f, 0.4f), ambEq = new Color(0.55f, 0.32f, 0.28f), ambGround = new Color(0.22f, 0.13f, 0.13f),
                ground = new Color(0.22f, 0.14f, 0.14f), tintRed = new Color(1.1f, 0.9f, 0.85f), tintGreen = new Color(1f, 0.85f, 0.75f),
                wall = new Color(0.32f, 0.19f, 0.19f), win = new Color(1f, 0.55f, 0.2f), lit = 0.3f, glow = 0.6f, fires = 9, weather = Weather.Embers,
                fogStart = 24f, fogEnd = 85f, sunDir = new Vector3(5f, -25f, -12f),
                decor = new[] { "Vehicle_Sports", "Vehicle_Pickup", "TrafficBarrier_2", "Barrel", "TrashBag_2", "Wheels_Stack" },
            },
            new ThemeDef
            {
                name = "FROZEN BRIDGE", line = "They don't feel the cold.",
                sky = new Color(0.72f, 0.83f, 0.92f), sun = new Color(0.92f, 0.96f, 1f), sunI = 1.35f,
                ambSky = new Color(0.82f, 0.9f, 1f), ambEq = new Color(0.62f, 0.7f, 0.8f), ambGround = new Color(0.5f, 0.55f, 0.62f),
                ground = new Color(0.86f, 0.9f, 0.94f), tintRed = new Color(0.85f, 0.9f, 1.15f), tintGreen = new Color(0.85f, 1.05f, 1.2f),
                wall = new Color(0.4f, 0.45f, 0.56f), win = new Color(1f, 0.88f, 0.6f), lit = 0.1f, glow = 0.3f, fires = 2, weather = Weather.Snow,
                fogStart = 30f, fogEnd = 95f, sunDir = new Vector3(25f, -30f, -6f),
                decor = new[] { "Vehicle_Pickup", "Vehicle_Truck", "TrafficCone_2", "Wheels_Stack", "TrafficBarrier_1", "Barrel" },
            },
            new ThemeDef
            {
                name = "LAST LIGHT", line = "Dawn is close. Hold until sunrise.",
                sky = new Color(0.9f, 0.55f, 0.66f), sun = new Color(1f, 0.82f, 0.85f), sunI = 1.3f,
                ambSky = new Color(1f, 0.78f, 0.85f), ambEq = new Color(0.65f, 0.5f, 0.6f), ambGround = new Color(0.28f, 0.22f, 0.3f),
                ground = new Color(0.33f, 0.26f, 0.31f), tintRed = new Color(1.05f, 0.9f, 1f), tintGreen = new Color(0.95f, 1f, 1.05f),
                wall = new Color(0.42f, 0.3f, 0.42f), win = new Color(1f, 0.82f, 0.56f), lit = 0.2f, glow = 0.3f, fires = 4,
                sunDir = new Vector3(8f, -14f, -10f),
                decor = new[] { "Wheels_Stack", "Pallet", "Couch", "Chest", "TrashBag_1", "Vehicle_Sports" },
            },
        };

        public static int Count { get { return All.Length; } }
        public static int Current { get; private set; } = -1;
        public static ThemeDef Def { get { return All[Mathf.Max(0, Current)]; } }
        public static int ForLevel(int level) { return ((level - 1) % All.Length + All.Length) % All.Length; }

        /// Sky, sun, ambient light and fog. Two lighting systems are kept in step: the project's own shaders
        /// (globals set by SkyEnv) and Unity's lighting for the imported kit models.
        public static void Apply(int index)
        {
            Current = index;
            var t = All[index];
            SkyEnv.Apply(t.sky, t.sun, t.ambSky, t.ambGround);
            SkyEnv.SetFog(t.fogStart, t.fogEnd);

            var sun = GameObject.Find("Sun");
            if (sun != null)
            {
                var l = sun.GetComponent<Light>();
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.7f;
                l.intensity = t.sunI;
                l.color = t.sun;
                sun.transform.rotation = Quaternion.LookRotation(t.sunDir);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = t.ambSky;
            RenderSettings.ambientEquatorColor = t.ambEq;
            RenderSettings.ambientGroundColor = t.ambGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = t.sky;
            RenderSettings.fogStartDistance = t.fogStart;
            RenderSettings.fogEndDistance = t.fogEnd;
        }
    }
}
