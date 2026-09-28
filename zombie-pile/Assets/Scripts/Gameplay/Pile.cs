using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// The heap of zombies against the wall, as columns across the street. A zombie that reaches the pile
    /// climbs to the top of a column (zombies like to climb where it is already high, so peaks form) and
    /// clings there; killing one drops everything above it by its height. When a zombie clings on near the
    /// top of the wall it climbs over. Scripted rather than physical, so it always works and reads clearly.
    public static class Pile
    {
        public const int Cols = 12;
        public const float ColW = 0.6f;

        static readonly List<Zombie>[] cols = new List<Zombie>[Cols];
        static readonly float[] baseH = new float[Cols];

        static Pile()
        {
            for (int i = 0; i < Cols; i++) cols[i] = new List<Zombie>();
        }

        public static void Clear()
        {
            for (int i = 0; i < Cols; i++) { cols[i].Clear(); baseH[i] = 0f; }
        }

        public static float X(int c) { return (c - (Cols - 1) * 0.5f) * ColW; }
        public static int ColAt(float x) { return Mathf.Clamp(Mathf.RoundToInt(x / ColW + (Cols - 1) * 0.5f), 0, Cols - 1); }

        public static float Height(int c)
        {
            float h = baseH[c];
            var list = cols[c];
            for (int i = 0; i < list.Count; i++) h += list[i].Step;
            return h;
        }

        public static float MaxHeight
        {
            get { float m = 0f; for (int c = 0; c < Cols; c++) m = Mathf.Max(m, Height(c)); return m; }
        }

        /// Where the heap starts at this column: the taller it is, the further it spills out from the wall.
        public static float FrontZ(int c) { return Arena.WallFront + 0.3f + Mathf.Min(2.4f, Height(c) * 0.42f); }

        /// Column a zombie heads for: usually the tallest one near it.
        public static int Choose(int own)
        {
            if (Random.value < 0.4f) return own;
            int best = own;
            float bh = Height(own);
            for (int c = Mathf.Max(0, own - 2); c <= Mathf.Min(Cols - 1, own + 2); c++)
            {
                float h = Height(c) + Random.Range(0f, 0.2f);
                if (h > bh) { bh = h; best = c; }
            }
            return best;
        }

        /// Adds a zombie on top of a column; returns the height its feet rest at.
        public static float Add(Zombie z, int c)
        {
            float y = Height(c);
            cols[c].Add(z);
            return y;
        }

        public static void Remove(Zombie z, int c)
        {
            if (c < 0 || c >= Cols) return;
            if (!cols[c].Remove(z)) return;
            Recompute(c);
        }

        /// Everything above a removed zombie slides down to close the gap.
        static void Recompute(int c)
        {
            float y = baseH[c];
            var list = cols[c];
            for (int i = 0; i < list.Count; i++)
            {
                list[i].SetPileY(y, i == 0);
                y += list[i].Step;
            }
        }

        /// A boss at the wall acts as a ramp under several columns.
        public static void SetBase(int from, int to, float h)
        {
            for (int c = Mathf.Max(0, from); c <= Mathf.Min(Cols - 1, to); c++) { baseH[c] = h; Recompute(c); }
        }

        /// The zombie closest to climbing over: the top of the tallest column.
        public static Zombie Highest()
        {
            Zombie best = null;
            float bh = 0.4f;
            for (int c = 0; c < Cols; c++)
            {
                var list = cols[c];
                if (list.Count == 0) continue;
                float h = Height(c);
                if (h > bh) { bh = h; best = list[list.Count - 1]; }
            }
            return best;
        }

        public static int Count
        {
            get { int n = 0; for (int c = 0; c < Cols; c++) n += cols[c].Count; return n; }
        }
    }
}
