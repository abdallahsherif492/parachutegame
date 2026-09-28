using UnityEngine;

namespace SkyDrop
{
    public enum CamMode { Menu, Freefall, Canopy, Landed, Crash }

    /// Follows the jumper with per-mode offsets, speed FOV, shake and kicks.
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public Camera cam;
        public Transform target;
        public CamMode mode;
        public float speed01;

        Vector3 offset, lookOffset;
        Vector3 crashPoint;
        float shake, kick;
        float orbit;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
        }

        public void SetMode(CamMode m, bool snap = false)
        {
            mode = m;
            if (m == CamMode.Crash && target != null) crashPoint = target.position;
            if (snap)
            {
                Vector3 o, l;
                Desired(out o, out l);
                offset = o;
                lookOffset = l;
                Place();
            }
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }
        public void Kick(float amount) { kick = amount; }

        void Desired(out Vector3 o, out Vector3 l)
        {
            switch (mode)
            {
                case CamMode.Menu:
                    float a = 3.6f + Mathf.Sin(orbit * 0.15f) * 0.5f;   // sway, never behind the plane
                    o = new Vector3(Mathf.Sin(a) * 9f, 2.2f, Mathf.Cos(a) * 9f);
                    l = new Vector3(0f, 0.2f, 0f);
                    break;
                case CamMode.Freefall:
                    // Nearly straight down: rings rush up at you as circles you steer into.
                    o = new Vector3(0f, 13f, -2.5f);
                    l = new Vector3(0f, -22f, 2.5f);
                    break;
                case CamMode.Canopy:
                    // Still steep, so the landing marker and the pad are easy to line up.
                    o = new Vector3(0f, 11f, -6f);
                    l = new Vector3(0f, -9f, 2f);
                    break;
                case CamMode.Landed:
                    float b = orbit * 0.35f;
                    o = new Vector3(Mathf.Sin(b) * 8f, 3.2f, -Mathf.Cos(b) * 8f);
                    l = new Vector3(0f, 0.5f, 0f);
                    break;
                default:
                    o = new Vector3(0f, 14f, -12f);
                    l = Vector3.zero;
                    break;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            orbit += Time.deltaTime;
            Vector3 o, l;
            Desired(out o, out l);
            float k = 1f - Mathf.Exp(-dt * (mode == CamMode.Freefall ? 3f : 2.2f));
            offset = Vector3.Lerp(offset, o, k);
            lookOffset = Vector3.Lerp(lookOffset, l, k);
            Place();

            float fov = 60f + speed01 * 16f + kick * 12f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-dt * 5f));
            kick = Mathf.MoveTowards(kick, 0f, dt * 3f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
        }

        void Place()
        {
            Vector3 basePos = mode == CamMode.Crash ? crashPoint : target.position;
            Vector3 pos = basePos + offset;
            if (shake > 0f)
            {
                float t = Time.unscaledTime * 40f;
                pos += new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, Mathf.PerlinNoise(t, t) - 0.5f) * shake * 2f;
            }
            if (pos.y < 1.5f) pos.y = 1.5f;
            transform.position = pos;
            Vector3 look = basePos + lookOffset - pos;
            if (look.sqrMagnitude > 0.001f)
            {
                // Looking straight down, "up" on screen must be +Z (the direction W / swipe-up moves you).
                Vector3 dir = look.normalized;
                float steep = Mathf.Clamp01((-dir.y - 0.75f) / 0.2f);
                Vector3 up = Vector3.Slerp(Vector3.up, Vector3.forward, steep);
                transform.rotation = Quaternion.LookRotation(dir, up);
            }
        }
    }
}
