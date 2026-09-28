using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyDrop
{
    /// Steering input, tuned for a top-down falling camera:
    /// - Desktop: the jumper flies toward the mouse cursor (no clicking), or WASD / arrows.
    /// - Touch: slide a finger and the jumper follows the finger's motion 1:1.
    /// Space / Enter / E (or the OPEN button) opens the parachute.
    public static class InputReader
    {
        public static Vector2 Steer { get; private set; }
        public static bool UsedSteer { get; private set; }

        /// Jumper position on screen (pixels), written by the GameManager every frame.
        public static Vector2 PlayerScreen;

        static bool openQueued;
        static int steerFinger = -1;
        static Vector2 lastTouch;
        static Vector2 touchSteer;
        static Vector2 lastMouse;
        static bool mouseActive;     // mouse moved since the last keyboard use
        static bool keyboardActive;
        static bool touchSeen;       // touch devices never use cursor-follow (emulated mouse would jump)

        static float MinDim { get { return Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)); } }

        /// Called by the OPEN button.
        public static void QueueOpen() { openQueued = true; }

        public static bool ConsumeOpen()
        {
            bool o = openQueued;
            openQueued = false;
            return o;
        }

        public static void ResetState()
        {
            openQueued = false;
            steerFinger = -1;
            touchSteer = Vector2.zero;
            Steer = Vector2.zero;
        }

        static bool OverUI(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }

        public static void Update()
        {
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);

            Vector2 k = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) k.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) k.y -= 1f;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
                openQueued = true;
            if (k != Vector2.zero)
            {
                keyboardActive = true;
                mouseActive = false;
            }

            Vector2 s;
            if (Input.touchCount > 0) touchSeen = true;
            if (Input.touchCount > 0 || steerFinger >= 0)
            {
                s = TouchSteer(dt);
            }
            else if (k != Vector2.zero || keyboardActive && !MouseMoved())
            {
                s = k;
            }
            else
            {
                s = MouseSteer();
            }

            if (s.sqrMagnitude > 1f) s.Normalize();
            Steer = s;
            if (s.sqrMagnitude > 0.05f) UsedSteer = true;
        }

        static bool MouseMoved()
        {
            Vector2 m = Input.mousePosition;
            bool moved = (m - lastMouse).sqrMagnitude > 9f;
            lastMouse = m;
            if (moved)
            {
                mouseActive = true;
                keyboardActive = false;
            }
            return moved;
        }

        /// The jumper flies toward the cursor; the further away, the faster.
        static Vector2 MouseSteer()
        {
            MouseMoved();
            if (!mouseActive || touchSeen || Application.isMobilePlatform) return Vector2.zero;
            Vector2 m = Input.mousePosition;
            if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;
            if (OverUI(-1)) return Vector2.zero;
            Vector2 d = (m - PlayerScreen) / (MinDim * 0.2f);
            float mag = d.magnitude;
            if (mag < 0.08f) return Vector2.zero;          // dead zone: sit still under the cursor
            return d / mag * Mathf.Clamp01((mag - 0.08f) / 0.92f);
        }

        /// Relative drag: finger speed maps to jumper speed, so the jumper tracks the finger.
        static Vector2 TouchSteer(float dt)
        {
            bool found = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && steerFinger < 0 && !OverUI(t.fingerId))
                {
                    steerFinger = t.fingerId;
                    lastTouch = t.position;
                    touchSteer = Vector2.zero;
                }
                if (t.fingerId != steerFinger) continue;
                found = true;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    steerFinger = -1;
                    break;
                }
                Vector2 vel = (t.position - lastTouch) / dt;   // pixels per second
                lastTouch = t.position;
                Vector2 target = vel / (MinDim * 0.8f);
                if (target.sqrMagnitude > 1f) target.Normalize();
                touchSteer = Vector2.Lerp(touchSteer, target, 1f - Mathf.Exp(-dt * 18f));
            }
            if (!found) steerFinger = -1;
            if (steerFinger < 0) touchSteer = Vector2.Lerp(touchSteer, Vector2.zero, 1f - Mathf.Exp(-dt * 10f));
            return touchSteer;
        }
    }
}
