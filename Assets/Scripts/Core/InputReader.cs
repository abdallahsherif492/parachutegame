using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyDrop
{
    /// Keyboard (WASD/arrows + Space) and a floating touch/mouse joystick.
    public static class InputReader
    {
        public static Vector2 Steer { get; private set; }
        public static bool Dragging { get; private set; }
        public static Vector2 DragOrigin { get; private set; }
        public static Vector2 DragPos { get; private set; }
        public static bool UsedSteer { get; private set; }

        static bool openQueued;
        static int steerFinger = -1;
        static bool mouseSteer;
        static Vector2 origin;

        public static float JoystickRadius { get { return Mathf.Min(Screen.width, Screen.height) * 0.11f; } }

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
            mouseSteer = false;
            Dragging = false;
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
            Vector2 k = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) k.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) k.y -= 1f;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
                openQueued = true;

            Vector2 drag = Vector2.zero;
            Dragging = false;
            if (Input.touchCount > 0)
            {
                mouseSteer = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began && steerFinger < 0 && !OverUI(t.fingerId))
                    {
                        steerFinger = t.fingerId;
                        origin = t.position;
                    }
                    if (t.fingerId != steerFinger) continue;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        steerFinger = -1;
                        continue;
                    }
                    drag = Joystick(t.position);
                    Dragging = true;
                }
            }
            else
            {
                steerFinger = -1;
                if (Input.GetMouseButtonDown(0) && !OverUI(-1))
                {
                    mouseSteer = true;
                    origin = Input.mousePosition;
                }
                if (!Input.GetMouseButton(0)) mouseSteer = false;
                if (mouseSteer)
                {
                    drag = Joystick(Input.mousePosition);
                    Dragging = true;
                }
            }

            Vector2 s = k + drag;
            if (s.sqrMagnitude > 1f) s.Normalize();
            Steer = s;
            if (s.sqrMagnitude > 0.05f) UsedSteer = true;
        }

        static Vector2 Joystick(Vector2 pos)
        {
            float r = JoystickRadius;
            Vector2 d = pos - origin;
            if (d.magnitude > r) origin = pos - d.normalized * r;   // floating joystick follows the finger
            d = pos - origin;
            DragOrigin = origin;
            DragPos = pos;
            return d / r;
        }
    }
}
