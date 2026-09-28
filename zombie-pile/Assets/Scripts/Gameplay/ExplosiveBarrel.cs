using UnityEngine;

namespace ZombiePile
{
    /// Oil drums on the street: shoot one and it blows up the zombies around it (and other drums: chain reactions).
    public class ExplosiveBarrel : MonoBehaviour
    {
        bool gone;

        public void Detonate(float delay = 0f)
        {
            if (gone) return;
            gone = true;
            if (delay > 0f) Game.I.Delay(delay, Boom); else Boom();
        }

        void Boom()
        {
            var at = transform.position + Vector3.up * 0.6f;
            ThrownBarrel.Explode(at, 4.2f, 16f, 20f);
            Destroy(gameObject);
        }
    }
}
