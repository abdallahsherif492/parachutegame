using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Pooled effects: blood and debris chunks, tracers, muzzle flashes, explosions (flash, fireball,
    /// shockwave, smoke, scorch mark), dust puffs and blood stains on the street.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Bit { public Transform t; public Renderer r; public Vector3 v; public float life, max, size; }
        class Flash { public Transform t; public float life, max; public Vector3 from, to; }
        class Puff { public Transform t; public Material m; public Color c; public Vector3 v; public float life, max, s0, s1; public bool flat; }

        readonly List<Bit> bits = new List<Bit>();
        readonly List<Flash> tracers = new List<Flash>();
        readonly List<Puff> puffs = new List<Puff>();
        readonly List<GameObject> stains = new List<GameObject>();
        int nextBit, nextTracer, nextPuff, nextStain;
        Transform muzzle;
        float muzzleT;
        Light flashLight;
        float lightT;
        readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        Material tracerMat, sniperMat;

        static readonly Color Blood1 = new Color(0.62f, 0.07f, 0.05f), Blood2 = new Color(0.34f, 0.02f, 0.03f);

        void Awake()
        {
            I = this;
            for (int i = 0; i < 300; i++)
            {
                var go = Shapes.Make("Bit", MeshGen.Box(), Mat.Lit(Color.green), transform, Vector3.zero, Vector3.one * 0.1f);
                go.SetActive(false);
                bits.Add(new Bit { t = go.transform, r = go.GetComponent<Renderer>() });
            }
            tracerMat = Mat.Lit(new Color(1f, 0.9f, 0.45f), 1f);
            sniperMat = Mat.Lit(new Color(0.75f, 0.95f, 1f), 1f);
            for (int i = 0; i < 48; i++)
            {
                var go = Shapes.Make("Tracer", MeshGen.Box(), tracerMat, transform, Vector3.zero, Vector3.one);
                go.SetActive(false);
                tracers.Add(new Flash { t = go.transform });
            }
            for (int i = 0; i < 90; i++)
            {
                var m = Mat.UniqueClear(Color.white, 0.4f);
                var go = Shapes.Make("Puff", MeshGen.Smooth(), m, transform, Vector3.zero, Vector3.one);
                go.SetActive(false);
                puffs.Add(new Puff { t = go.transform, m = m });
            }
            muzzle = Shapes.Make("Muzzle", MeshGen.Sphere(), Mat.Lit(new Color(1f, 0.85f, 0.4f), 1f), transform, Vector3.zero, Vector3.one * 0.45f).transform;
            muzzle.gameObject.SetActive(false);
            var lg = new GameObject("BlastLight");
            lg.transform.SetParent(transform, false);
            flashLight = lg.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, 0.7f, 0.35f);
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;
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

        /// A splash of blood thrown back toward the shooter.
        public void Blood(Vector3 pos, Vector3 toward, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var b = bits[nextBit];
                nextBit = (nextBit + 1) % bits.Count;
                b.t.gameObject.SetActive(true);
                b.r.sharedMaterial = MatFor(i % 2 == 0 ? Blood1 : Blood2);
                b.t.position = pos;
                b.t.rotation = Random.rotation;
                b.v = (toward.normalized * 2.5f + Random.insideUnitSphere * 2.2f + Vector3.up * 1.5f);
                b.size = Random.Range(0.05f, 0.12f);
                b.t.localScale = Vector3.one * b.size;
                b.max = b.life = Random.Range(0.35f, 0.7f);
            }
        }

        public void Sparks(Vector3 pos) { Burst(pos, 6, new Color(1f, 0.85f, 0.35f), 5f, 0.05f); }

        public void Tracer(Vector3 from, Vector3 to, bool sniper)
        {
            var f = tracers[nextTracer];
            nextTracer = (nextTracer + 1) % tracers.Count;
            f.from = from; f.to = to;
            f.max = f.life = sniper ? 0.12f : 0.06f;
            f.t.GetComponent<Renderer>().sharedMaterial = sniper ? sniperMat : tracerMat;
            f.t.gameObject.SetActive(true);
            Place(f, 1f, sniper ? 0.1f : 0.06f);
        }

        static void Place(Flash f, float k, float w)
        {
            var d = f.to - f.from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            f.t.position = f.from + d * 0.5f;
            f.t.rotation = Quaternion.LookRotation(d);
            f.t.localScale = new Vector3(w * k, w * k, len);
        }

        public void MuzzleFlash(Vector3 at, float size)
        {
            muzzle.position = at;
            muzzle.rotation = Random.rotation;
            muzzle.localScale = Vector3.one * size * Random.Range(0.8f, 1.2f);
            muzzle.gameObject.SetActive(true);
            muzzleT = 0.04f;
        }

        Puff NextPuff()
        {
            var p = puffs[nextPuff];
            nextPuff = (nextPuff + 1) % puffs.Count;
            p.t.gameObject.SetActive(true);
            p.flat = false;
            return p;
        }

        void Spawn(Vector3 at, Color c, Vector3 v, float s0, float s1, float life, bool flat = false)
        {
            var p = NextPuff();
            p.t.position = at;
            p.t.rotation = Random.rotation;
            p.c = c; p.v = v; p.s0 = s0; p.s1 = s1; p.max = p.life = life; p.flat = flat;
            if (flat) p.t.rotation = Quaternion.identity;
            p.m.SetColor("_Color", c);
            p.t.localScale = flat ? new Vector3(s0, 0.05f, s0) : Vector3.one * s0;
        }

        public void Explosion(Vector3 at, float radius)
        {
            // light flash, fireball (hot core inside), shockwave on the ground, rising smoke, debris, a scorch mark
            flashLight.transform.position = at + Vector3.up * 1.5f;
            flashLight.range = radius * 4f;
            flashLight.intensity = 5f;
            flashLight.enabled = true;
            lightT = 0.22f;
            Spawn(at, new Color(1f, 0.55f, 0.15f, 0.9f), Vector3.up * 1.5f, 0.4f, radius * 1.25f, 0.45f);
            Spawn(at, new Color(1f, 0.92f, 0.5f, 1f), Vector3.up * 1f, 0.3f, radius * 0.75f, 0.3f);
            var ground = new Vector3(at.x, 0.12f, at.z);
            Spawn(ground, new Color(1f, 0.85f, 0.6f, 0.55f), Vector3.zero, 0.6f, radius * 2.8f, 0.3f, true);
            for (int i = 0; i < 7; i++)
            {
                var o = Random.insideUnitSphere * radius * 0.4f; o.y = Mathf.Abs(o.y);
                Spawn(at + o, new Color(0.24f, 0.21f, 0.21f, 0.7f), Vector3.up * Random.Range(1.2f, 2.4f) + o * 0.6f, 0.5f, Random.Range(1.6f, 2.6f), Random.Range(1.2f, 1.9f));
            }
            Spawn(ground + Vector3.down * 0.08f, new Color(0.05f, 0.04f, 0.04f, 0.55f), Vector3.zero, radius * 1.1f, radius * 1.2f, 6f, true);
            Burst(at, 26, new Color(1f, 0.55f, 0.15f), 10f, 0.2f);
            Burst(at, 16, new Color(0.3f, 0.28f, 0.3f), 7f, 0.26f);
        }

        public void Smoke(Vector3 at, float size)
        {
            Spawn(at, new Color(0.3f, 0.28f, 0.28f, 0.45f), Vector3.up * 1.3f + Random.insideUnitSphere * 0.3f, 0.3f * size, 1.4f * size, 2.4f);
        }

        public void Dust(Vector3 at, float size)
        {
            for (int i = 0; i < 3; i++)
                Spawn(at + Random.insideUnitSphere * 0.2f * size, new Color(0.62f, 0.55f, 0.5f, 0.55f), Vector3.up * 0.8f + Random.insideUnitSphere * 0.5f,
                      0.2f * size, 0.9f * size, 0.6f);
        }

        /// A kit blood decal where a body came to rest (the oldest one is reused).
        public void BloodStain(Vector3 at)
        {
            var pos = new Vector3(at.x, 0.02f, at.z);
            float yaw = Random.Range(0f, 360f);
            if (stains.Count < 20)
            {
                var names = new[] { "Blood_1", "Blood_2", "Blood_3" };
                var go = Kit.Place(names[Random.Range(0, names.Length)], pos, yaw, Random.Range(0.5f, 0.8f), transform, false);
                if (go != null) stains.Add(go);
                return;
            }
            var s = stains[nextStain];
            nextStain = (nextStain + 1) % stains.Count;
            if (s == null) return;
            s.transform.position = pos;
            s.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void ClearStains()
        {
            foreach (var s in stains) if (s != null) Destroy(s);
            stains.Clear();
            nextStain = 0;
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
                else Place(f, f.life / f.max, f.max > 0.1f ? 0.1f : 0.06f);
            }
            foreach (var p in puffs)
            {
                if (p.life <= 0f) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.t.gameObject.SetActive(false); continue; }
                float k = 1f - p.life / p.max;
                float sc = Mathf.Lerp(p.s0, p.s1, 1f - (1f - k) * (1f - k));
                p.t.localScale = p.flat ? new Vector3(sc, 0.05f, sc) : Vector3.one * sc;
                p.t.position += p.v * dt;
                p.v *= 1f - dt * 1.5f;
                var c = p.c;
                // scorch marks stay a while, then fade; everything else fades as it grows
                c.a = p.flat && p.max > 2f ? p.c.a * Mathf.Clamp01(p.life / 1.5f) : p.c.a * (1f - k);
                p.m.SetColor("_Color", c);
            }
            if (lightT > 0f)
            {
                lightT -= dt;
                flashLight.intensity = Mathf.Max(0f, lightT / 0.22f) * 5f;
                if (lightT <= 0f) flashLight.enabled = false;
            }
            if (muzzleT > 0f) { muzzleT -= dt; if (muzzleT <= 0f) muzzle.gameObject.SetActive(false); }
        }
    }
}
