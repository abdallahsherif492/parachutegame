using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyDrop
{
    /// <summary>
    /// Procedural flat-shaded low-poly meshes (cached). Everything in the game is built
    /// from these, so the build ships with zero model/texture assets.
    /// All shapes are unit sized (fit in a 1x1x1 box) and are scaled by their transform.
    /// </summary>
    public static class MeshGen
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        sealed class Builder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector3> norms = new List<Vector3>();
            readonly List<int> tris = new List<int>();

            // Adds a triangle whose face points roughly along 'outward' (winding is fixed automatically).
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    Vector3 tmp = b; b = c; c = tmp;
                    n = -n;
                }
                n.Normalize();
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(verts);
                m.SetNormals(norms);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Mesh Cached(string key, Func<Mesh> make)
        {
            Mesh m;
            if (!cache.TryGetValue(key, out m) || m == null)
            {
                m = make();
                cache[key] = m;
            }
            return m;
        }

        /// Unit cube centered on the origin.
        public static Mesh Box()
        {
            return Cached("box", () =>
            {
                var b = new Builder();
                const float h = 0.5f;
                var p = new Vector3[8];
                for (int i = 0; i < 8; i++)
                    p[i] = new Vector3((i & 1) == 0 ? -h : h, (i & 2) == 0 ? -h : h, (i & 4) == 0 ? -h : h);
                b.Quad(p[0], p[2], p[6], p[4], Vector3.left);
                b.Quad(p[1], p[3], p[7], p[5], Vector3.right);
                b.Quad(p[0], p[1], p[5], p[4], Vector3.down);
                b.Quad(p[2], p[3], p[7], p[6], Vector3.up);
                b.Quad(p[0], p[1], p[3], p[2], Vector3.back);
                b.Quad(p[4], p[5], p[7], p[6], Vector3.forward);
                return b.Build("Box");
            });
        }

        /// Cylinder of diameter 1 from y=-0.5 to y=0.5.
        public static Mesh Cylinder(int sides = 10)
        {
            return Cached("cyl" + sides, () =>
            {
                var b = new Builder();
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f;
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f;
                    Vector3 up = Vector3.up * 0.5f;
                    b.Quad(d0 - up, d1 - up, d1 + up, d0 + up, d0 + d1);
                    b.Tri(up, d0 + up, d1 + up, Vector3.up);
                    b.Tri(-up, d0 - up, d1 - up, Vector3.down);
                }
                return b.Build("Cylinder");
            });
        }

        /// Cone with base diameter 1 at y=0 and apex at y=1.
        public static Mesh Cone(int sides = 8)
        {
            return Cached("cone" + sides, () =>
            {
                var b = new Builder();
                var apex = Vector3.up;
                float offset = sides == 4 ? Mathf.PI * 0.25f : 0f;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = offset + i * Mathf.PI * 2f / sides, a1 = offset + (i + 1) * Mathf.PI * 2f / sides;
                    var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f;
                    var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f;
                    b.Tri(apex, d0, d1, d0 + d1 + Vector3.up * 0.3f);
                    b.Tri(Vector3.zero, d0, d1, Vector3.down);
                }
                return b.Build("Cone");
            });
        }

        /// Square pyramid (base 1x1 at y=0, apex at y=1).
        public static Mesh Pyramid() { return Cone(4); }

        /// Low-poly sphere of diameter 1.
        public static Mesh Sphere(int subdiv = 1)
        {
            return Cached("ico" + subdiv, () =>
            {
                float t = (1f + Mathf.Sqrt(5f)) / 2f;
                var v = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
                };
                for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * 0.5f;
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
                };
                var faces = new List<Vector3[]>();
                for (int i = 0; i < f.Length; i += 3) faces.Add(new[] { v[f[i]], v[f[i + 1]], v[f[i + 2]] });
                for (int s = 0; s < subdiv; s++)
                {
                    var next = new List<Vector3[]>();
                    foreach (var tri in faces)
                    {
                        Vector3 a = tri[0], bb = tri[1], c = tri[2];
                        Vector3 ab = ((a + bb) * 0.5f).normalized * 0.5f;
                        Vector3 bc = ((bb + c) * 0.5f).normalized * 0.5f;
                        Vector3 ca = ((c + a) * 0.5f).normalized * 0.5f;
                        next.Add(new[] { a, ab, ca });
                        next.Add(new[] { bb, bc, ab });
                        next.Add(new[] { c, ca, bc });
                        next.Add(new[] { ab, bc, ca });
                    }
                    faces = next;
                }
                var b = new Builder();
                foreach (var tri in faces) b.Tri(tri[0], tri[1], tri[2], tri[0] + tri[1] + tri[2]);
                return b.Build("Sphere");
            });
        }

        /// Ring lying flat in the XZ plane. Outer diameter ~1; 'thickness' is the tube radius.
        public static Mesh Torus(float thickness = 0.06f, int segments = 24, int sides = 6)
        {
            return Cached("torus" + thickness + "_" + segments + "_" + sides, () =>
            {
                var b = new Builder();
                float major = 0.5f - thickness;
                Func<int, int, Vector3> P = (i, j) =>
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float phi = j * Mathf.PI * 2f / sides;
                    var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    return radial * major + (radial * Mathf.Cos(phi) + Vector3.up * Mathf.Sin(phi)) * thickness;
                };
                for (int i = 0; i < segments; i++)
                {
                    float am = (i + 0.5f) * Mathf.PI * 2f / segments;
                    var tubeCenter = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am)) * major;
                    for (int j = 0; j < sides; j++)
                    {
                        Vector3 p0 = P(i, j), p1 = P(i + 1, j), p2 = P(i + 1, j + 1), p3 = P(i, j + 1);
                        b.Quad(p0, p1, p2, p3, (p0 + p1 + p2 + p3) * 0.25f - tubeCenter);
                    }
                }
                return b.Build("Torus");
            });
        }

        /// Smooth-shaded capsule: height 1 (y -0.5..0.5), radius r (so x/z extent is 2r).
        /// r = 0.5 gives a smooth sphere. Use for anything organic (body, clouds, balloons).
        public static Mesh Capsule(float r, int segs = 14, int hemi = 6)
        {
            r = Mathf.Clamp(Mathf.Round(r * 100f) / 100f, 0.02f, 0.5f);
            return Cached("cap" + r + "_" + segs + "_" + hemi, () =>
            {
                var ringY = new List<float>();
                var ringR = new List<float>();
                var ringNy = new List<float>();
                for (int i = 0; i <= hemi; i++)
                {
                    float a = Mathf.PI * 0.5f * i / hemi;
                    ringY.Add(0.5f - r + Mathf.Cos(a) * r);
                    ringR.Add(Mathf.Sin(a) * r);
                    ringNy.Add(Mathf.Cos(a));
                }
                int start = r >= 0.5f ? hemi - 1 : hemi;   // a sphere shares its equator ring
                for (int i = start; i >= 0; i--)
                {
                    float a = Mathf.PI * 0.5f * i / hemi;
                    ringY.Add(-0.5f + r - Mathf.Cos(a) * r);
                    ringR.Add(Mathf.Sin(a) * r);
                    ringNy.Add(-Mathf.Cos(a));
                }
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i < ringY.Count; i++)
                {
                    float ny = ringNy[i];
                    float nr = Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny));
                    for (int j = 0; j <= segs; j++)
                    {
                        float phi = j * Mathf.PI * 2f / segs;
                        float cx = Mathf.Cos(phi), cz = Mathf.Sin(phi);
                        verts.Add(new Vector3(cx * ringR[i], ringY[i], cz * ringR[i]));
                        norms.Add(new Vector3(cx * nr, ny, cz * nr));
                    }
                }
                for (int i = 0; i < ringY.Count - 1; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = i * (segs + 1) + j, b = a + segs + 1;
                        tris.Add(a); tris.Add(a + 1); tris.Add(b);
                        tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                    }
                var m = new Mesh { name = "Capsule" };
                m.SetVertices(verts);
                m.SetNormals(norms);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        /// Smooth sphere of diameter 1.
        public static Mesh Smooth() { return Capsule(0.5f); }

        /// Flat upward-facing disc of diameter 1 at y=0.
        public static Mesh Disc(int sides = 24)
        {
            return Cached("disc" + sides, () =>
            {
                var b = new Builder();
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    b.Tri(Vector3.zero, new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.5f,
                        new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.5f, Vector3.up);
                }
                return b.Build("Disc");
            });
        }

        /// Flat upward-facing quad 1x1 at y=0.
        public static Mesh Plane()
        {
            return Cached("plane", () =>
            {
                var b = new Builder();
                b.Quad(new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f),
                    new Vector3(-0.5f, 0, 0.5f), Vector3.up);
                return b.Build("Plane");
            });
        }
    }
}
