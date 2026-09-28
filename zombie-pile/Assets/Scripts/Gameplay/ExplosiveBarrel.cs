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
            if (delay > 0f && Game.I != null) Game.I.Delay(delay, Boom_);
            else Boom_();
        }

        void Boom_()
        {
            if (this == null) return;
            var at = transform.position + Vector3.up * 0.6f;
            Boom.Explode(at, 4.2f, 160f, 13f, true);
            gameObject.SetActive(false);   // comes back next level
        }

        public void Restore() { gone = false; gameObject.SetActive(true); }
    }
}
