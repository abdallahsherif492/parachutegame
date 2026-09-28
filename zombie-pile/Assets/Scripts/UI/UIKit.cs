using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiePile
{
    /// Builds uGUI elements from code: chunky mobile-ad style (Lilita One / Bangers fonts, thick outlines,
    /// 3D buttons with a darker rim) with procedural sprites (rounded rects, circles, rings, gradients).
    public static class UIKit
    {
        static Font font, display;
        static bool fontsLoaded;
        static Sprite round, circle, ring, fade, star;

        public static readonly Color Ink = new Color(0.07f, 0.04f, 0.09f, 1f);
        public static readonly Color Dark = new Color(0.06f, 0.05f, 0.1f, 0.72f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.2f);
        public static readonly Color Lime = new Color(0.6f, 0.95f, 0.25f);
        public static readonly Color Green = new Color(0.3f, 0.78f, 0.25f);
        public static readonly Color Red = new Color(0.9f, 0.2f, 0.15f);
        public static readonly Color Blue = new Color(0.2f, 0.55f, 1f);
        public static readonly Color Orange = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Purple = new Color(0.6f, 0.35f, 1f);
        public static readonly Color Gray = new Color(0.4f, 0.42f, 0.5f);

        static void LoadFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            font = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            display = Resources.Load<Font>("Fonts/Bangers-Regular");
        }

        static Font Builtin
        {
            get
            {
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return f;
            }
        }

        /// Body font (Lilita One): chunky and readable at small sizes.
        public static Font Font { get { LoadFonts(); if (font == null) font = Builtin; return font; } }

        /// Headline font (Bangers): comic-book titles and banners.
        public static Font Display { get { LoadFonts(); if (display == null) display = Font; return display; } }

        public static Sprite Round { get { if (round == null) round = Rounded(64, 24); return round; } }
        public static Sprite Circle { get { if (circle == null) circle = Rounded(128, 64); return circle; } }
        public static Sprite Ring { get { if (ring == null) ring = MakeRing(128, 0.78f); return ring; } }
        public static Sprite Star { get { if (star == null) star = MakeStar(96); return star; } }

        /// Vertical fade: transparent at the top, opaque at the bottom.
        public static Sprite Fade
        {
            get
            {
                if (fade != null) return fade;
                var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[4 * 64];
                for (int y = 0; y < 64; y++)
                {
                    float a = 1f - y / 63f;
                    byte b = (byte)(a * a * 255f);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, b);
                }
                tex.SetPixels32(px);
                tex.Apply();
                fade = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
                return fade;
            }
        }

        static Sprite Rounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            float border = radius < size / 2 ? radius : 0f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static Sprite MakeRing(int size, float inner)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float R = size * 0.5f, r = R * inner;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(R, R));
                    float a = Mathf.Clamp01(R - d) * Mathf.Clamp01(d - r);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeStar(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                pts[i] = new Vector2(size / 2f + Mathf.Cos(a) * r, size / 2f + Mathf.Sin(a) * r - size * 0.03f);
            }
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 4x supersampling for smooth edges
                    int hits = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        var p = new Vector2(x + 0.25f + (s % 2) * 0.5f, y + 0.25f + (s / 2) * 0.5f);
                        if (Inside(pts, p)) hits++;
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 63.75f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public static Color Darker(Color c, float k = 0.4f) { return new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a); }

        // ------------------------------------------------------------------------ layout

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// Anchor + pivot at the same normalized point, then offset and size.
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        /// Stretches inside the parent with a margin (left, bottom, right, top).
        public static RectTransform Inset(RectTransform r, float l, float b, float rt, float t)
        {
            Stretch(r);
            r.offsetMin = new Vector2(l, b);
            r.offsetMax = new Vector2(-rt, -t);
            return r;
        }

        public static Image Image(Transform parent, string name, Color c, Sprite sprite = null, bool raycast = false)
        {
            var r = Node(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// A dark rounded panel with a thin ink border.
        public static Image Panel(Transform parent, string name, Color c)
        {
            var img = Image(parent, name, c, Round);
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.35f);
            o.effectDistance = new Vector2(2f, -2f);
            return img;
        }

        /// Body text: Lilita One with a thick ink outline and a drop shadow, like mobile-ad captions.
        public static Text Text(Transform parent, string s, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            return MakeText(parent, s, size, c, align, outline, Font);
        }

        /// Headline text: Bangers.
        public static Text Title(Transform parent, string s, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter)
        {
            return MakeText(parent, s, size, c, align, true, Display);
        }

        static Text MakeText(Transform parent, string s, int size, Color c, TextAnchor align, bool outline, Font f)
        {
            var r = Node("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = f;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = f == Builtin ? FontStyle.Bold : FontStyle.Normal;
            t.lineSpacing = 0.95f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                float d = Mathf.Clamp(size * 0.055f, 1.5f, 6f);
                var sh = r.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
                sh.effectDistance = new Vector2(0f, -d * 1.6f);
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = Ink;
                o.effectDistance = new Vector2(d, -d);
                if (size >= 40)
                {
                    var o2 = r.gameObject.AddComponent<Outline>();
                    o2.effectColor = Ink;
                    o2.effectDistance = new Vector2(d * 0.7f, d * 0.7f);
                }
            }
            return t;
        }

        /// 3D button: a darker rim under the face, a gloss band on top, ink border. The label is the face's Text.
        public static Button Button(Transform parent, string label, Color c, Vector2 size, UnityAction onClick, int fontSize = 34)
        {
            var rim = Image(parent, "Btn " + label, Darker(c, 0.45f), Round, true);
            rim.rectTransform.sizeDelta = size;
            var border = rim.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.05f, 0.03f, 0.06f, 0.85f);
            border.effectDistance = new Vector2(3f, -3f);
            var b = rim.gameObject.AddComponent<Button>();
            b.targetGraphic = rim;
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(onClick);
            rim.gameObject.AddComponent<ButtonPunch>();

            float lip = Mathf.Clamp(size.y * 0.08f, 5f, 10f);
            var face = Image(rim.transform, "Face", c, Round);
            Inset(face.rectTransform, 0f, lip, 0f, 0f);
            var gloss = Image(face.transform, "Gloss", new Color(1f, 1f, 1f, 0.22f), Round);
            Inset(gloss.rectTransform, 6f, size.y * 0.5f, 6f, 5f);
            var t = Text(face.transform, label, fontSize, Color.white);
            Stretch(t.rectTransform);
            return b;
        }

        public static Image Face(Button b)
        {
            var f = b.transform.Find("Face");
            return f != null ? f.GetComponent<Image>() : (Image)b.targetGraphic;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        // ------------------------------------------------------------------------ little icons (built from shapes)

        public static RectTransform Skull(Transform parent, float size)
        {
            var r = Node("Skull", parent);
            r.sizeDelta = new Vector2(size, size);
            var bone = new Color(0.95f, 0.93f, 0.85f);
            var head = Image(r, "Head", bone, Circle);
            Place(head.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size, size * 0.82f));
            var ho = head.gameObject.AddComponent<Outline>(); ho.effectColor = Ink; ho.effectDistance = new Vector2(2f, -2f);
            var jaw = Image(r, "Jaw", bone, Round);
            Place(jaw.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, size * 0.02f), new Vector2(size * 0.56f, size * 0.36f));
            var jo = jaw.gameObject.AddComponent<Outline>(); jo.effectColor = Ink; jo.effectDistance = new Vector2(2f, -2f);
            head.transform.SetAsLastSibling();
            for (int i = -1; i <= 1; i += 2)
            {
                var eye = Image(r, "Eye", Ink, Circle);
                Place(eye.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i * size * 0.2f, size * 0.08f), new Vector2(size * 0.27f, size * 0.3f));
            }
            var nose = Image(r, "Nose", Ink, Round);
            Place(nose.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -size * 0.14f), new Vector2(size * 0.1f, size * 0.12f));
            for (int i = -1; i <= 1; i++)
            {
                var tooth = Image(r, "Tooth", Ink, null);
                Place(tooth.rectTransform, new Vector2(0.5f, 0f), new Vector2(i * size * 0.12f, size * 0.06f), new Vector2(size * 0.04f, size * 0.16f));
            }
            return r;
        }

        public static RectTransform BarrelIcon(Transform parent, float size)
        {
            var r = Node("BarrelIcon", parent);
            r.sizeDelta = new Vector2(size, size);
            var body = Image(r, "Body", new Color(0.95f, 0.3f, 0.15f), Round);
            Place(body.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.66f, size * 0.9f));
            var bo = body.gameObject.AddComponent<Outline>(); bo.effectColor = Ink; bo.effectDistance = new Vector2(3f, -3f);
            for (int i = -1; i <= 1; i += 2)
            {
                var band = Image(body.transform, "Band", Darker(new Color(0.95f, 0.3f, 0.15f), 0.5f), null);
                Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, i * size * 0.26f), new Vector2(size * 0.66f, size * 0.07f));
            }
            var sign = Image(body.transform, "Sign", Gold, Circle);
            Place(sign.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.34f, size * 0.34f));
            var bang = Text(sign.transform, "!", Mathf.RoundToInt(size * 0.3f), Ink, TextAnchor.MiddleCenter, false);
            Stretch(bang.rectTransform);
            var gloss = Image(body.transform, "Gloss", new Color(1f, 1f, 1f, 0.25f), Round);
            Place(gloss.rectTransform, new Vector2(0f, 0.5f), new Vector2(size * 0.07f, 0f), new Vector2(size * 0.08f, size * 0.6f));
            return r;
        }
    }

    /// Squash on press + click sound.
    public class ButtonPunch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        Vector3 target = Vector3.one;

        public void OnPointerDown(PointerEventData e)
        {
            target = Vector3.one * 0.92f;
            if (SoundBank.I != null) SoundBank.I.Play(SoundBank.I.click, 0.7f);
        }

        public void OnPointerUp(PointerEventData e) { target = Vector3.one; }

        void OnDisable()
        {
            target = Vector3.one;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f));
        }
    }

    /// Gentle breathing scale (unscaled time) for call-to-action elements.
    public class Pulse : MonoBehaviour
    {
        public float amount = 0.06f, speed = 5f;

        void Update()
        {
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * speed) * amount);
        }

        void OnDisable() { transform.localScale = Vector3.one; }
    }
}
