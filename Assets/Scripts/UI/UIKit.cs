using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkyDrop
{
    /// Builds uGUI elements from code with procedural sprites (rounded rects, circles, stars).
    public static class UIKit
    {
        static Font font;
        static Sprite round, circle, star;

        public static readonly Color Dark = new Color(0.08f, 0.09f, 0.16f, 0.82f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.15f);
        public static readonly Color Green = new Color(0.25f, 0.8f, 0.4f);
        public static readonly Color Blue = new Color(0.2f, 0.55f, 1f);
        public static readonly Color Orange = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Purple = new Color(0.6f, 0.35f, 1f);
        public static readonly Color Gray = new Color(0.45f, 0.47f, 0.55f);

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
            }
        }

        public static Sprite Round
        {
            get
            {
                if (round == null) round = Rounded(64, 24);
                return round;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = Rounded(128, 64);
                return circle;
            }
        }

        public static Sprite Star
        {
            get
            {
                if (star == null) star = MakeStar(96);
                return star;
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

        public static Text Text(Transform parent, string s, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            var r = Node("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.55f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Button Button(Transform parent, string label, Color c, Vector2 size, UnityAction onClick, int fontSize = 34)
        {
            var img = Image(parent, "Btn " + label, c, Round, true);
            img.rectTransform.sizeDelta = size;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            img.gameObject.AddComponent<ButtonPunch>();
            // bottom shade for a chunky look
            var shade = Image(img.transform, "Shade", new Color(0f, 0f, 0f, 0.18f), Round);
            Stretch(shade.rectTransform);
            shade.rectTransform.offsetMax = new Vector2(0f, -size.y * 0.55f);
            var t = Text(img.transform, label, fontSize, Color.white);
            Stretch(t.rectTransform);
            return b;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
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
