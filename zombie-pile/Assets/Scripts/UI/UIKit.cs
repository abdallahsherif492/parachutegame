using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombiePile
{
    /// Builds uGUI elements from code: rendered sprites from Resources/UI (panels, 3D buttons, icons, logo),
    /// Lilita One / Bangers text with a thick ink stroke, and a few procedural shapes (circle, ring, fade).
    public static class UIKit
    {
        static Font font, display;
        static bool fontsLoaded;
        static Sprite round, circle, ring, fade;

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

        // ------------------------------------------------------------------------ rendered sprites (Resources/UI, 2x)

        static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// 9-slice borders in 1x pixels (left, bottom, right, top); sprites not listed are drawn whole.
        static Vector4 BorderOf(string name)
        {
            switch (name)
            {
                case "panel": return Vector4.one * 32f;
                case "track": return Vector4.one * 16f;
                case "fill": return Vector4.one * 12f;
                case "card": return Vector4.one * 44f;
                case "keycap": return Vector4.one * 12f;
                case "ribbon": return new Vector4(100f, 0f, 100f, 0f);
            }
            return name.StartsWith("btn_") ? Vector4.one * 36f : Vector4.zero;
        }

        /// A sprite from Assets/Resources/UI. The PNGs are rendered at 2x, so 200 pixels per unit keeps
        /// them at their design size on the 1280x720 canvas while staying sharp on big screens.
        public static Sprite Spr(string name)
        {
            Sprite sp;
            if (sprites.TryGetValue(name, out sp)) return sp;
            var tex = Resources.Load<Texture2D>("UI/" + name);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, BorderOf(name) * 2f);
            }
            else
            {
                Debug.LogWarning("UI sprite missing: Resources/UI/" + name + ".png");
                sp = name.StartsWith("ic_") || name.StartsWith("round") ? Circle : Round;
            }
            sprites[name] = sp;
            return sp;
        }

        /// Image from a rendered sprite: sliced when it has borders, otherwise whole (icons keep their aspect).
        public static Image Pic(Transform parent, string name, Color c, bool raycast = false)
        {
            var sp = Spr(name);
            var img = Image(parent, name, c, sp, raycast);
            if (sp.border.sqrMagnitude <= 0f) { img.type = UnityEngine.UI.Image.Type.Simple; img.preserveAspect = true; }
            return img;
        }

        /// Positions a rect with the coordinates of the 1280x720 design mock-up (x, y = top-left corner, y down),
        /// anchored at 'anchor' so it sticks to that side of the screen on other aspect ratios.
        public static RectTransform At(RectTransform r, Vector2 anchor, float x, float y, float w, float h)
        {
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = anchor;
            r.sizeDelta = new Vector2(w, h);
            float px = x + w * anchor.x, py = 720f - y - h + h * anchor.y;
            r.anchoredPosition = new Vector2(px - 1280f * anchor.x, py - 720f * anchor.y);
            return r;
        }

        /// A 3D sprite button (btn_green / btn_red / btn_dark / btn_gold / round_red ...) with a Bangers label.
        public static Button SpriteButton(Transform parent, string sprite, string label, int fontSize, UnityAction onClick)
        {
            var img = Pic(parent, sprite, Color.white, true);
            img.preserveAspect = false;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            if (onClick != null) b.onClick.AddListener(onClick);
            img.gameObject.AddComponent<ButtonPunch>();
            if (!string.IsNullOrEmpty(label))
            {
                var t = Title(img.transform, label, fontSize, Color.white);
                Inset(t.rectTransform, 0f, 9f, 0f, 0f);   // centred on the face, above the 9px lip
            }
            return b;
        }

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
                var st = r.gameObject.AddComponent<Stroke>();
                st.width = Mathf.Clamp(size * 0.08f, 2f, 9f);
                st.drop = Mathf.Clamp(size * 0.07f, 2f, 7f);
            }
            return t;
        }

    }

    /// Thick ink outline (8 directions) plus a drop shadow, in one pass: the mobile-ad caption look.
    /// Copies take the text's alpha, so fading text fades its outline too.
    public class Stroke : BaseMeshEffect
    {
        public float width = 3f, drop = 3f;
        public Color color = UIKit.Ink, shadow = new Color(0f, 0f, 0f, 0.45f);
        static readonly System.Collections.Generic.List<UIVertex> src = new System.Collections.Generic.List<UIVertex>();
        static readonly System.Collections.Generic.List<UIVertex> dst = new System.Collections.Generic.List<UIVertex>();
        static readonly Vector2[] Dirs = { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
                                           new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f) };

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            src.Clear(); dst.Clear();
            vh.GetUIVertexStream(src);
            if (drop > 0f)
            {
                Copy(shadow, new Vector2(width * 0.7f, -width - drop));
                Copy(shadow, new Vector2(-width * 0.7f, -width - drop));
            }
            for (int d = 0; d < Dirs.Length; d++) Copy(color, Dirs[d] * width);
            dst.AddRange(src);
            vh.Clear();
            vh.AddUIVertexTriangleStream(dst);
        }

        void Copy(Color c, Vector2 off)
        {
            for (int i = 0; i < src.Count; i++)
            {
                var v = src[i];
                v.position += new Vector3(off.x, off.y, 0f);
                var a = v.color.a / 255f;
                v.color = new Color(c.r, c.g, c.b, c.a * a);
                dst.Add(v);
            }
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
