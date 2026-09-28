using UnityEngine;

namespace ZombiePile
{
    /// Fixed view from above and behind the wall, looking down at the pile. Handles shake and
    /// portrait screens (keeps the whole gate in view on phones).
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        Camera cam;
        float shake;
        Vector3 basePos;
        Quaternion baseRot;
        Vector2 lean;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();

        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            // above the gunner, looking down the wall face at the pile and up the street at the horde
            basePos = new Vector3(0f, Arena.WallHeight + 3.7f, Arena.WallFront - 1.5f);
            baseRot = Quaternion.LookRotation(new Vector3(0f, 2.5f, Arena.WallFront + 8f) - basePos);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            // keep ~13 m of the gate visible horizontally at the pile's distance
            float wantV = 2f * Mathf.Atan(Mathf.Tan(34f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(wantV, 62f, 95f);

            // lean a little toward where the player aims
            var mp = Input.mousePosition;
            var target = new Vector2(mp.x / Mathf.Max(1, Screen.width) - 0.5f, mp.y / Mathf.Max(1, Screen.height) - 0.5f);
            lean = Vector2.Lerp(lean, target, 1f - Mathf.Exp(-Time.deltaTime * 2f));
            shake = Mathf.Max(0f, shake - Time.deltaTime * 2.2f);
            var jitter = Random.insideUnitSphere * shake * shake * 0.35f;
            transform.position = basePos + new Vector3(lean.x * 0.8f, 0f, 0f) + jitter;
            transform.rotation = baseRot * Quaternion.Euler(-lean.y * 2f, lean.x * 3f, 0f);
        }
    }
}
