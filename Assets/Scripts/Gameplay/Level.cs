using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    public enum HazardKind { Tumble, Lethal, Storm }

    public class Hazard
    {
        public HazardKind kind;
        public string label;
        public Transform tr;
        public bool box;
        public float radius;          // sphere radius
        public Vector3 half;          // box half extents (axis aligned)
        public Vector3 basePos;
        public int motion;            // 0 static, 1 oscillate, 2 circle, 3 bob
        public Vector3 axis;
        public float amp, freq, phase;
        public bool faceMotion;
        public bool nearMissDone, hit;
        public Transform[] spinners;  // rotors / wings
        public float spinSpeed;
        public bool flap;
        Vector3 lastPos;

        public Vector3 Center { get { return tr.position; } }

        public void Tick(float t, float dt)
        {
            Vector3 p = basePos;
            switch (motion)
            {
                case 1: p = basePos + axis * (Mathf.Sin(t * freq + phase) * amp); break;
                case 2: p = basePos + new Vector3(Mathf.Cos(t * freq + phase), 0f, Mathf.Sin(t * freq + phase)) * amp; break;
                case 3: p = basePos + Vector3.up * (Mathf.Sin(t * freq + phase) * amp); break;
            }
            tr.position = p;
            if (faceMotion && dt > 0f)
            {
                Vector3 v = p - lastPos;
                v.y = 0f;
                if (v.sqrMagnitude > 0.0001f)
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(v), dt * 6f);
            }
            lastPos = p;
            if (spinners != null)
            {
                for (int i = 0; i < spinners.Length; i++)
                {
                    if (flap)
                    {
                        float a = Mathf.Sin(t * spinSpeed + phase) * 45f * (i == 0 ? 1f : -1f);
                        spinners[i].localRotation = Quaternion.Euler(0f, 0f, a);
                    }
                    else spinners[i].Rotate(0f, spinSpeed * dt, 0f, Space.Self);
                }
            }
        }

        /// Signed distance from a point to the hazard surface.
        public float Distance(Vector3 p)
        {
            if (!box) return Vector3.Distance(p, tr.position) - radius;
            Vector3 q = p - tr.position;
            q = new Vector3(Mathf.Abs(q.x) - half.x, Mathf.Abs(q.y) - half.y, Mathf.Abs(q.z) - half.z);
            Vector3 outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
        }
    }

    public class Ring
    {
        public Vector3 pos;
        public float radius;
        public Transform tr;
        public bool done;
    }

    public class Coin
    {
        public Transform tr;
        public bool collected;
        public bool pulled;
    }

    public class Target
    {
        public Transform tr;           // positioned at the pad center, on the pad surface
        public Vector3 start;
        public Vector3 axis;
        public float amp, freq;
        public float radius;
        public float top;
        public Vector3 velocity;

        public Vector3 Center { get { return tr.position; } }

        public void Tick(float t, float dt)
        {
            if (amp <= 0f) return;
            Vector3 p = start + axis * (Mathf.Sin(t * freq) * amp);
            velocity = dt > 0f ? (p - tr.position) / dt : Vector3.zero;
            tr.position = p;
        }

        public float HorizontalDistance(Vector3 p)
        {
            Vector3 d = p - tr.position;
            d.y = 0f;
            return d.magnitude;
        }
    }

    /// A built, playable level: all runtime objects plus the per-frame animation.
    public class Level
    {
        public LevelConfig cfg;
        public WorldTheme theme;
        public GameObject root;
        public Vector3 spawn;
        public Vector3 wind;
        public Target target;
        public Transform targetBuilding;  // city only
        public float roofHalf;            // city only: half width of the target rooftop
        public readonly List<Ring> rings = new List<Ring>();
        public readonly List<Coin> coins = new List<Coin>();
        public readonly List<Hazard> hazards = new List<Hazard>();
        public readonly List<Hazard> groundHazards = new List<Hazard>();
        public readonly List<GameObject> lightning = new List<GameObject>();
        public Transform windsock;
        public Transform plane;
        public float time;

        public void Tick(float dt)
        {
            time += dt;
            target.Tick(time, dt);
            for (int i = 0; i < hazards.Count; i++) hazards[i].Tick(time, dt);
            float spin = time * 180f;
            for (int i = 0; i < coins.Count; i++)
            {
                var c = coins[i];
                if (!c.collected && c.tr != null) c.tr.localRotation = Quaternion.Euler(60f, spin + i * 20f, 0f);
            }
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (r.done || r.tr == null) continue;
                float s = r.radius * 2f * (1f + Mathf.Sin(time * 4f + i) * 0.03f);
                r.tr.localScale = new Vector3(s, s * 0.6f, s);
            }
            for (int i = 0; i < lightning.Count; i++)
            {
                bool on = Mathf.Repeat(time * 0.7f + i * 0.37f, 1f) < 0.05f;
                if (lightning[i].activeSelf != on) lightning[i].SetActive(on);
            }
        }

        /// Height of whatever you would land on at this XZ position.
        public float SurfaceHeight(Vector3 p, out bool onTarget)
        {
            onTarget = target.HorizontalDistance(p) <= target.radius;
            if (onTarget || InsideRoof(p)) return target.top;
            return 0f;
        }

        public bool InsideRoof(Vector3 p)
        {
            if (roofHalf <= 0f) return false;
            Vector3 d = p - target.tr.position;
            return Mathf.Abs(d.x) < roofHalf && Mathf.Abs(d.z) < roofHalf;
        }

        /// Altitude above the landing pad surface (the number the altimeter shows).
        public float AltitudeAbovePad(Vector3 p) { return p.y - target.top; }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
