using System;
using UnityEngine;

namespace ZombiePile
{
    /// Three fixed views, one per shooter (0 = left tower, 1 = behind Shaun on the wall, 2 = right tower),
    /// plus two cinematics: the establishing shot over the safe-zone camp at the start of a level, and the
    /// same view again when the wall falls. Switching swoops the camera up through the sky and down onto the
    /// other shooter, GTA style. The camera never follows the pointer, so aiming stays steady; narrow
    /// screens keep the same horizontal coverage. All framings were checked in the design preview.
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public int View { get; private set; } = 1;
        public bool Switching { get { return switchT < SwitchTime || mode != Mode.Normal; } }
        public bool InIntro { get { return mode == Mode.Intro; } }

        enum Mode { Normal, Intro, Camp }
        const float SwitchTime = 0.7f, IntroHold = 2.2f, IntroSwoop = 1.2f;
        Camera cam;
        Mode mode;
        float shake, switchT = SwitchTime, introT;
        Vector3 fromPos;
        Quaternion fromRot;
        float fromFov;
        Action introDone;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        struct Pose { public Vector3 pos; public Quaternion rot; public float fov; }

        float Fov(float baseV)
        {
            // designed for 16:9: keep that horizontal coverage on narrower screens
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float halfH = Mathf.Atan(Mathf.Tan(baseV * 0.5f * Mathf.Deg2Rad) * 16f / 9f);
            return Mathf.Clamp(2f * Mathf.Atan(Mathf.Tan(halfH) / aspect) * Mathf.Rad2Deg, baseV, 95f);
        }

        Pose Target(int view)
        {
            Pose p;
            if (view == 1)
            {
                p.pos = new Vector3(0f, Arena.WallHeight + 12.3f, Arena.WallFront - 7.8f);
                p.rot = Quaternion.LookRotation(new Vector3(0f, 0.2f, Arena.WallFront + 12.2f) - p.pos);
                p.fov = Fov(50f);
            }
            else
            {
                // over the tower shooter's shoulder, looking across the pile on the wall face
                float sx = view == 0 ? 1f : -1f;      // the right tower mirrors the left one
                var spot = Arena.TowerSpot; spot.x *= sx;
                p.pos = spot + new Vector3(-0.2f * sx, 3f, 5.6f);
                p.rot = Quaternion.LookRotation(new Vector3(1.6f * sx, 2.8f, Arena.WallFront + 1.5f) - p.pos);
                p.fov = Fov(46f);
            }
            return p;
        }

        static Pose Make(Vector3 pos, Vector3 look, float fov)
        {
            Pose p; p.pos = pos; p.rot = Quaternion.LookRotation(look - pos); p.fov = fov; return p;
        }

        Pose Current() { Pose p; p.pos = transform.position; p.rot = transform.rotation; p.fov = cam.fieldOfView; return p; }

        public void SetView(int view, bool instant)
        {
            view = Mathf.Clamp(view, 0, 2);
            mode = Mode.Normal; introDone = null;
            if (view == View && !instant && switchT >= SwitchTime) return;
            fromPos = transform.position;
            fromRot = transform.rotation;
            fromFov = cam.fieldOfView;
            View = view;
            switchT = instant ? SwitchTime : 0f;
            if (!instant && SoundBank.I != null) SoundBank.I.Play(SoundBank.I.whoosh, 0.7f);
            if (!instant) Stats.Add(Ev.Switch);
            Shooter.SetLocal(view);
        }

        /// TAB: the next shooter to the right (wrapping around).
        public void Toggle() { if (!Switching && Net.CanSwitch) SetView((View + 1) % 3, false); }
        public void Go(int view) { if (!Switching && Net.CanSwitch && view != View) SetView(view, false); }

        // ------------------------------------------------------------------ cinematics
        /// Establishing shot: low over the camp with the survivors, the wall and the burning city beyond,
        /// a slow push in, then a swoop up to the gameplay view. Any click or key skips the hold.
        public void PlayIntro(int startView, Action done)
        {
            View = startView;
            Shooter.SetLocal(startView);
            mode = Mode.Intro;
            introT = 0f;
            introDone = done;
            switchT = SwitchTime;
        }

        /// The wall has fallen: back over the camp to watch.
        public void ShowCamp()
        {
            fromPos = transform.position; fromRot = transform.rotation; fromFov = cam.fieldOfView;
            mode = Mode.Camp;
            introT = 0f;
            introDone = null;
        }

        static readonly Vector3 IntroA = new Vector3(-4.5f, 3.0f, -19.5f), IntroB = new Vector3(-1.5f, 3.4f, -16.5f), IntroLook = new Vector3(0f, 4.5f, 8f);
        static readonly Vector3 CampPos = new Vector3(3f, 6f, -25f), CampLook = new Vector3(0f, 3.5f, 6f);

        /// Up and over: a curve through a point high above both ends, with a dip of the gaze between.
        void Swoop(Pose a, Pose b, float e, out Vector3 pos, out Quaternion rot, out float fov)
        {
            var mid = (a.pos + b.pos) * 0.5f + Vector3.up * 9f;
            pos = (1f - e) * (1f - e) * a.pos + 2f * (1f - e) * e * mid + e * e * b.pos;
            var down = Quaternion.LookRotation((a.pos + b.pos) * 0.5f + Vector3.down * 6f - pos);
            rot = e < 0.5f ? Quaternion.Slerp(a.rot, down, e * 2f) : Quaternion.Slerp(down, b.rot, (e - 0.5f) * 2f);
            fov = Mathf.Lerp(a.fov, b.fov, e) + Mathf.Sin(e * Mathf.PI) * 12f;
        }

        void LateUpdate()
        {
            var t = Target(View);
            Vector3 pos; Quaternion rot; float fov;
            if (mode == Mode.Intro)
            {
                introT += Time.unscaledDeltaTime;
                bool skip = introT > 0.5f && introT < IntroHold && (Input.GetMouseButtonDown(0) || Input.anyKeyDown || Input.touchCount > 0);
                if (skip) introT = IntroHold;
                var a = Make(IntroA, IntroLook, Fov(62f));
                var b = Make(IntroB, IntroLook, Fov(58f));
                if (introT < IntroHold)
                {
                    float k = introT / IntroHold;
                    k = k * k * (3f - 2f * k);
                    pos = Vector3.Lerp(a.pos, b.pos, k);
                    rot = Quaternion.Slerp(a.rot, b.rot, k);
                    fov = Mathf.Lerp(a.fov, b.fov, k);
                }
                else
                {
                    float k = Mathf.Clamp01((introT - IntroHold) / IntroSwoop);
                    Swoop(b, t, k * k * (3f - 2f * k), out pos, out rot, out fov);
                    if (k >= 1f)
                    {
                        mode = Mode.Normal;
                        var d = introDone; introDone = null;
                        if (d != null) d();
                    }
                }
            }
            else if (mode == Mode.Camp)
            {
                introT += Time.unscaledDeltaTime;
                var c = Make(CampPos, CampLook, Fov(58f));
                var from = new Pose { pos = fromPos, rot = fromRot, fov = fromFov };
                float k = Mathf.Clamp01(introT / 1.1f);
                Swoop(from, c, k * k * (3f - 2f * k), out pos, out rot, out fov);
                if (k >= 1f) { pos += new Vector3(Mathf.Sin(Time.unscaledTime * 0.3f) * 0.6f, 0f, 0f); }
            }
            else if (switchT < SwitchTime)
            {
                switchT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(switchT / SwitchTime);
                Swoop(new Pose { pos = fromPos, rot = fromRot, fov = fromFov }, t, k * k * (3f - 2f * k), out pos, out rot, out fov);
            }
            else { pos = t.pos; rot = t.rot; fov = t.fov; }

            shake = Mathf.Max(0f, shake - Time.deltaTime * 3f);
            float s = shake * shake;
            transform.position = pos + UnityEngine.Random.insideUnitSphere * s * 0.25f;
            transform.rotation = rot * Quaternion.Euler(UnityEngine.Random.Range(-1f, 1f) * s * 1.2f, UnityEngine.Random.Range(-1f, 1f) * s * 1.2f, 0f);
            cam.fieldOfView = fov;
        }
    }
}
