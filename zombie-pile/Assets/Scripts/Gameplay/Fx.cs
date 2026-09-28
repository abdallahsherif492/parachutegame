using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Cheap pooled effects: goo/debris chunks, bullet tracers, muzzle flashes and explosions.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Bit { public Transform t; public Renderer r; public Vector3 v; public float life, max, size; }
        class Flash { public Transform t; public float life, max; public Vector3 from, to; }

        readonly List<Bit> bits = new List<Bit>();
        readonly List<Flash> tracers = new List<Flash>();
        readonly List<Flash> blasts = new List<Flash>();
        int nextBit, nextTracer, nextBlast;
        Transform muzzle;
        float muzzleT;
        readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

        void Awake()
        {
            I = this;
            for (int i = 0; i < 260; i++)
            {
                var go = Shapes.Make("Bit", MeshGen.Box(), Mat.Lit(Color.green), transform, Vector3.zero, Vector3.one * 0.1f);
                go.SetActive(false);
                bits.Add(new Bit { t = go.transform, r = go.GetComponent<Renderer>() });
            }
            for (int i = 0; i < 40; i++)
            {
                var go = Shapes.Make("Tracer", MeshGen.Box(), Mat.Lit(new Color(1f, 0.9f, 0.45f), 1f), transform, Vector3.zero, Vector3.one);
                go.SetActive(false);
                tracers.Add(new Flash { t = go.transform });
            }
            for (int i = 0; i < 8; i++)
            {
                var go = Shapes.Make("Blast", MeshGen.Smooth(), Mat.UniqueClear(new Color(1f, 0.6f, 0.2f, 0.8f), 0.2f), transform, Vector3.zero, Vector3.one);
                go.SetActive(false);
                blasts.Add(new Flash { t = go.transform });
            }
            muzzle = Shapes.Make("Muzzle", MeshGen.Sphere(), Mat.Lit(new Color(1f, 0.85f, 0.4f), 1f), transform, Vector3.zero, Vector3.one * 0.45f).transform;
            muzzle.gameObject.SetActive(false);
        }

        Material MatFor(Color c)
        {
            Material m;
            if (!mats.TryGetValue(c, out m)) { m = Mat.Lit(c); mats[c] = m; }
            return m;
        }

        public void Burst(Vector3 pos, int n, Color c, float speed, float size = 0.12f)
        {
            var mat = MatFor(c);
            var mat2 = MatFor(c * 0.7f);
            for (int i = 0; i < n; i++)
            {
                var b = bits[nextBit];
                nextBit = (nextBit + 1) % bits.Count;
                b.t.gameObject.SetActive(true);
                b.r.sharedMaterial = i % 3 == 0 ? mat2 : mat;
                b.t.position = pos;
                b.t.rotation = Random.rotation;
                b.v = Random.insideUnitSphere * speed + Vector3.up * speed * 0.6f;
                b.size = size * Random.Range(0.6f, 1.4f);
                b.t.localScale = Vector3.one * b.size;
                b.max = b.life = Random.Range(0.5f, 1f);
            }
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            var f = tracers[nextTracer];
            nextTracer = (nextTracer + 1) % tracers.Count;
            f.from = from; f.to = to;
            f.max = f.life = 0.06f;
            f.t.gameObject.SetActive(true);
            Place(f, 1f);
        }

        static void Place(Flash f, float k)
        {
            var d = f.to - f.from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            f.t.position = f.from + d * 0.5f;
            f.t.rotation = Quaternion.LookRotation(d);
            f.t.localScale = new Vector3(0.06f * k, 0.06f * k, len);
        }

        public void MuzzleFlash(Vector3 at)
        {
            muzzle.position = at;
            muzzle.rotation = Random.rotation;
            muzzle.localScale = Vector3.one * Random.Range(0.35f, 0.55f);
            muzzle.gameObject.SetActive(true);
            muzzleT = 0.04f;
        }

        public void Explosion(Vector3 at, float radius)
        {
            var f = blasts[nextBlast];
            nextBlast = (nextBlast + 1) % blasts.Count;
            f.from = at; f.to = Vector3.one * radius * 2f;
            f.max = f.life = 0.35f;
            f.t.gameObject.SetActive(true);
            f.t.position = at;
            Burst(at, 30, new Color(1f, 0.55f, 0.15f), 9f, 0.22f);
            Burst(at, 18, new Color(0.35f, 0.33f, 0.35f), 6f, 0.3f);
            CameraRig.I.Shake(0.9f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < bits.Count; i++)
            {
                var b = bits[i];
                if (b.life <= 0f) continue;
                b.life -= dt;
                if (b.life <= 0f) { b.t.gameObject.SetActive(false); continue; }
                b.v.y -= 22f * dt;
                var p = b.t.position + b.v * dt;
                if (p.y < 0.05f) { p.y = 0.05f; b.v *= 0.35f; b.v.y = Mathf.Abs(b.v.y) * 0.3f; }
                b.t.position = p;
                b.t.localScale = Vector3.one * b.size * Mathf.Clamp01(b.life / b.max * 2f);
            }
            foreach (var f in tracers)
            {
                if (f.life <= 0f) continue;
                f.life -= dt;
                if (f.life <= 0f) f.t.gameObject.SetActive(false);
                else Place(f, f.life / f.max);
            }
            foreach (var f in blasts)
            {
                if (f.life <= 0f) continue;
                f.life -= dt;
                if (f.life <= 0f) { f.t.gameObject.SetActive(false); continue; }
                float k = 1f - f.life / f.max;
                f.t.localScale = f.to * Mathf.Sqrt(k);
                f.t.GetComponent<Renderer>().sharedMaterial.SetColor("_Color", new Color(1f, 0.55f + 0.3f * (1f - k), 0.2f, 0.85f * (1f - k)));
            }
            if (muzzleT > 0f) { muzzleT -= dt; if (muzzleT <= 0f) muzzle.gameObject.SetActive(false); }
        }
    }
}
