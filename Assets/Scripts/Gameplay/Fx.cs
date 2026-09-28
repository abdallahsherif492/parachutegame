using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// Pooled particle bursts, speed streaks and pop-away animations. No ParticleSystem needed.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Particle
        {
            public Transform tr;
            public MeshRenderer mr;
            public Vector3 vel;
            public float life, max, size;
            public bool gravity;
        }

        class PopAnim
        {
            public Transform tr;
            public float t;
            public Vector3 scale0;
        }

        readonly List<Particle> particles = new List<Particle>();
        readonly List<PopAnim> pops = new List<PopAnim>();
        readonly List<Transform> streaks = new List<Transform>();
        Material streakMat;
        int next;

        void Awake()
        {
            I = this;
            var root = transform;
            for (int i = 0; i < 180; i++)
            {
                var go = Shapes.Make("P", i % 3 == 0 ? MeshGen.Sphere(0) : MeshGen.Box(), Mat.Lit(Color.white), root, Vector3.zero, Vector3.one);
                go.SetActive(false);
                particles.Add(new Particle { tr = go.transform, mr = go.GetComponent<MeshRenderer>() });
            }
            streakMat = Mat.UniqueClear(new Color(1f, 1f, 1f, 0f), 0f);
            for (int i = 0; i < 26; i++)
            {
                var go = Shapes.Make("Streak", MeshGen.Box(), streakMat, root, Vector3.zero, new Vector3(0.05f, 6f, 0.05f));
                streaks.Add(go.transform);
            }
        }

        public void Burst(Vector3 pos, Color c, int count, float speed, float size, float life, bool gravity)
        {
            var mat = Mat.Lit(c, 0.6f);
            for (int i = 0; i < count; i++)
            {
                var p = particles[next];
                next = (next + 1) % particles.Count;
                p.tr.gameObject.SetActive(true);
                p.tr.position = pos;
                p.tr.rotation = Random.rotation;
                p.mr.sharedMaterial = mat;
                p.vel = Random.insideUnitSphere * speed;
                if (gravity) p.vel.y = Mathf.Abs(p.vel.y) + speed * 0.4f;
                p.size = size * Random.Range(0.6f, 1.3f);
                p.max = life * Random.Range(0.7f, 1.2f);
                p.life = p.max;
                p.gravity = gravity;
                p.tr.localScale = Vector3.one * p.size;
            }
        }

        public void Confetti(Vector3 pos)
        {
            Color[] cols = { new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.85f, 1f), new Color(0.5f, 1f, 0.4f), new Color(0.85f, 0.45f, 1f) };
            for (int i = 0; i < cols.Length; i++) Burst(pos + Vector3.up, cols[i], 10, 12f, 0.35f, 1.8f, true);
        }

        /// Scale up + vanish (rings, coins).
        public void PopAway(Transform tr)
        {
            if (tr == null) return;
            pops.Add(new PopAnim { tr = tr, scale0 = tr.localScale });
        }

        /// Streaks rush past the jumper proportional to fall speed.
        public void UpdateStreaks(Vector3 center, float fallSpeed, float intensity, float dt)
        {
            Color c = streakMat.GetColor("_Color");
            c.a = Mathf.MoveTowards(c.a, 0.35f * intensity, dt * 2f);
            streakMat.SetColor("_Color", c);
            bool visible = c.a > 0.01f;
            for (int i = 0; i < streaks.Count; i++)
            {
                var s = streaks[i];
                if (s.gameObject.activeSelf != visible) s.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 p = s.position + Vector3.up * fallSpeed * 0.6f * dt;
                Vector3 rel = p - center;
                if (rel.y > 18f || rel.y < -45f || new Vector2(rel.x, rel.z).magnitude > 16f)
                {
                    Vector2 r = Random.insideUnitCircle.normalized * Random.Range(3f, 14f);
                    p = center + new Vector3(r.x, Random.Range(-40f, -10f), r.y);
                }
                s.position = p;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.life <= 0f) continue;
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.tr.gameObject.SetActive(false);
                    continue;
                }
                if (p.gravity) p.vel.y -= 20f * dt;
                else p.vel *= 1f - 2.5f * dt;
                p.tr.position += p.vel * dt;
                p.tr.Rotate(200f * dt, 150f * dt, 0f);
                p.tr.localScale = Vector3.one * (p.size * Mathf.Clamp01(p.life / p.max * 1.5f));
            }
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var a = pops[i];
                if (a.tr == null) { pops.RemoveAt(i); continue; }
                a.t += Time.unscaledDeltaTime / 0.25f;
                a.tr.localScale = a.scale0 * (1f + a.t * 0.6f) * (1f - a.t * a.t);
                if (a.t >= 1f)
                {
                    a.tr.gameObject.SetActive(false);
                    pops.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            foreach (var p in particles)
            {
                p.life = 0f;
                p.tr.gameObject.SetActive(false);
            }
            pops.Clear();
        }
    }
}
