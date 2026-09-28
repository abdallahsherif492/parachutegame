using UnityEngine;

namespace ZombiePile
{
    /// Fixed high view from behind the wall, looking down the street: the soldier at the bottom, the pile
    /// against the wall and the horde coming up the canyon (framing checked in the design preview).
    /// The camera never follows the pointer, so aiming stays steady. Wider screens see the same width.
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        Camera cam;
        float shake;
        Vector3 basePos;
        Quaternion baseRot;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();

        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        void LateUpdate()
        {
            basePos = new Vector3(0f, Arena.WallHeight + 12.3f, Arena.WallFront - 7.8f);
            baseRot = Quaternion.LookRotation(new Vector3(0f, 0.2f, Arena.WallFront + 12.2f) - basePos);
            // 50 degrees vertical on 16:9; narrower screens keep the same horizontal coverage
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float halfH = Mathf.Atan(Mathf.Tan(25f * Mathf.Deg2Rad) * 16f / 9f);
            float wantV = 2f * Mathf.Atan(Mathf.Tan(halfH) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(wantV, 50f, 90f);

            shake = Mathf.Max(0f, shake - Time.deltaTime * 3f);
            float k = shake * shake;
            transform.position = basePos + Random.insideUnitSphere * k * 0.25f;
            transform.rotation = baseRot * Quaternion.Euler(Random.Range(-1f, 1f) * k * 1.2f, Random.Range(-1f, 1f) * k * 1.2f, 0f);
        }
    }
}
