using UnityEngine;

namespace SkyDrop
{
    /// The risk ladder: the lower you open the parachute (relative to the minimum
    /// altitude it needs to fully open), the bigger the multiplier.
    public static class Zones
    {
        public static readonly float[] Max = { 0f, 6f, 15f, 30f, 55f, 90f, float.PositiveInfinity };
        public static readonly float[] Mult = { 10f, 8f, 5f, 3f, 2f, 1.5f, 1f };
        public static readonly string[] Name = { "LEGENDARY", "INSANE", "CRAZY", "RISKY", "BOLD", "NICE", "SAFE" };
        public static readonly Color[] Col =
        {
            new Color(0.75f, 0.3f, 1f),
            new Color(1f, 0.15f, 0.25f),
            new Color(1f, 0.38f, 0.15f),
            new Color(1f, 0.62f, 0.1f),
            new Color(1f, 0.88f, 0.2f),
            new Color(0.55f, 0.95f, 0.35f),
            new Color(0.55f, 0.8f, 1f),
        };

        /// You can't open the parachute higher than this margin (keeps canopy time short).
        public const float Ceiling = 120f;
        public const float BarMin = -10f, BarMax = 130f;

        public static int Of(float margin)
        {
            for (int i = 0; i < Max.Length; i++)
                if (margin < Max[i]) return i;
            return Max.Length - 1;
        }

        public static string MultText(float m)
        {
            return Mathf.Approximately(m, Mathf.Floor(m)) ? "x" + (int)m : "x" + m.ToString("0.0");
        }
    }
}
