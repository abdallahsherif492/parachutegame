using UnityEngine;

namespace ZombiePile
{
    /// Two fixed views, one per shooter: 0 = behind Shaun on the wall (the whole street), 1 = over Lis's
    /// shoulder on the tower (the pile on the wall face). Switching swoops the camera up through the sky and
    /// down onto the other shooter, GTA style. Both framings were checked in the design preview.
    /// The camera never follows the pointer, so aiming stays steady; narrow screens keep the same width.
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public int View { get; private set; }
        public bool Switching { get { return switchT < SwitchTime; } }

        const float SwitchTime = 0.7f;
        Camera cam;
        float shake, switchT = SwitchTime;
        Vector3 fromPos;
        Quaternion fromRot;
        float fromFov;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        struct Pose { public Vector3 pos; public Quaternion rot; public float fov; }

        Pose Target(int view)
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            Pose p;
            float baseV;
            if (view == 0)
            {
                p.pos = new Vector3(0f, Arena.WallHeight + 12.3f, Arena.WallFront - 7.8f);
                p.rot = Quaternion.LookRotation(new Vector3(0f, 0.2f, Arena.WallFront + 12.2f) - p.pos);
                baseV = 50f;
            }
            else
            {
                p.pos = Arena.TowerSpot + new Vector3(-0.2f, 3f, 5.6f);
                p.rot = Quaternion.LookRotation(new Vector3(1.6f, 2.8f, Arena.WallFront + 1.5f) - p.pos);
                baseV = 46f;
            }
            // designed for 16:9: keep that horizontal coverage on narrower screens
            float halfH = Mathf.Atan(Mathf.Tan(baseV * 0.5f * Mathf.Deg2Rad) * 16f / 9f);
            p.fov = Mathf.Clamp(2f * Mathf.Atan(Mathf.Tan(halfH) / aspect) * Mathf.Rad2Deg, baseV, 95f);
            return p;
        }

        public void SetView(int view, bool instant)
        {
            if (view == View && !instant) return;
            fromPos = transform.position;
            fromRot = transform.rotation;
            fromFov = cam.fieldOfView;
            View = view;
            switchT = instant ? SwitchTime : 0f;
            if (!instant) SoundBank.I.Play(SoundBank.I.whoosh, 0.7f);
        }

        public void Toggle() { if (!Switching) SetView(1 - View, false); }

        void LateUpdate()
        {
            var t = Target(View);
            Vector3 pos; Quaternion rot; float fov;
            if (switchT < SwitchTime)
            {
                switchT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(switchT / SwitchTime);
                float e = k * k * (3f - 2f * k);
                // up and over: a curve through a point high above both views
                var mid = (fromPos + t.pos) * 0.5f + Vector3.up * 9f;
                pos = (1f - e) * (1f - e) * fromPos + 2f * (1f - e) * e * mid + e * e * t.pos;
                var down = Quaternion.LookRotation((fromPos + t.pos) * 0.5f + Vector3.down * 6f - pos);
                rot = e < 0.5f ? Quaternion.Slerp(fromRot, down, e * 2f) : Quaternion.Slerp(down, t.rot, (e - 0.5f) * 2f);
                fov = Mathf.Lerp(fromFov, t.fov, e) + Mathf.Sin(e * Mathf.PI) * 12f;
            }
            else { pos = t.pos; rot = t.rot; fov = t.fov; }

            shake = Mathf.Max(0f, shake - Time.deltaTime * 3f);
            float s = shake * shake;
            transform.position = pos + Random.insideUnitSphere * s * 0.25f;
            transform.rotation = rot * Quaternion.Euler(Random.Range(-1f, 1f) * s * 1.2f, Random.Range(-1f, 1f) * s * 1.2f, 0f);
            cam.fieldOfView = fov;
        }
    }
}
