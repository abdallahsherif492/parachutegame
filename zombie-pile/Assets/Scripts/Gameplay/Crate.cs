using UnityEngine;

namespace ZombiePile
{
    public enum PowerUp { RapidFire, DoubleDamage, Reload, Repair }

    /// A supply crate that floats down on a parachute. Shoot it open for coins and a power-up.
    public class Crate : MonoBehaviour
    {
        Transform chute;
        float hp = 3f, groundT, sway;
        bool landed, open;
        Vector3 target;

        public static Crate Drop(Vector3 landAt)
        {
            var go = new GameObject("Crate");
            go.layer = Arena.Props;
            go.transform.position = landAt + Vector3.up * 16f;
            var c = go.AddComponent<Crate>();
            c.target = landAt;
            Kit.Place("Chest_Special", go.transform.position, Random.Range(-30f, 30f), 1.7f, go.transform, true);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.6f, 1.0f, 1.2f);
            box.center = new Vector3(0f, 0.4f, 0f);
            // parachute: a striped dome on four strings
            c.chute = new GameObject("Chute").transform;
            c.chute.SetParent(go.transform, false);
            c.chute.localPosition = Vector3.up * 2.6f;
            Shapes.Make("Canopy", MeshGen.Sphere(), Mat.Lit(new Color(0.95f, 0.45f, 0.2f)), c.chute, Vector3.zero, new Vector3(2.6f, 1.1f, 2.6f));
            Shapes.Make("Stripe", MeshGen.Sphere(), Mat.Lit(new Color(0.96f, 0.93f, 0.85f)), c.chute, Vector3.up * 0.02f, new Vector3(1.2f, 1.14f, 2.62f));
            for (int i = 0; i < 4; i++)
            {
                var a = new Vector3(i < 2 ? -1f : 1f, 0f, i % 2 == 0 ? -1f : 1f);
                var top = a * 0.95f;
                var bottom = new Vector3(a.x * 0.6f, -2.2f, a.z * 0.4f);
                var mid = (top + bottom) * 0.5f;
                Shapes.Make("String", MeshGen.Box(), Mat.Lit(new Color(0.2f, 0.18f, 0.16f)), c.chute, mid, new Vector3(0.03f, 0.03f, (top - bottom).magnitude),
                    Quaternion.LookRotation(top - bottom));
            }
            SoundBank.I.Play(SoundBank.I.chime, 0.5f, 0.8f);
            if (Hud.I != null) Hud.I.Banner("SUPPLY DROP!", "Shoot the crate!", 1.6f);
            return c;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!landed)
            {
                sway += dt;
                var p = transform.position;
                p.y = Mathf.MoveTowards(p.y, target.y, dt * 2.2f);
                p.x = target.x + Mathf.Sin(sway * 1.3f) * 0.6f;
                transform.position = p;
                transform.rotation = Quaternion.Euler(Mathf.Sin(sway * 1.3f) * 8f, 0f, Mathf.Cos(sway * 1.1f) * 6f);
                if (p.y <= target.y + 0.01f)
                {
                    landed = true;
                    transform.rotation = Quaternion.identity;
                    Fx.I.Dust(transform.position, 1.2f);
                    SoundBank.I.Play(SoundBank.I.thump, 0.6f);
                }
            }
            else
            {
                groundT += dt;
                if (chute != null) { chute.localScale = Vector3.one * Mathf.Max(0.01f, 1f - groundT * 2f); if (groundT > 0.5f) Destroy(chute.gameObject); }
                if (groundT > 14f) Destroy(gameObject);   // missed it
            }
        }

        public void Hit()
        {
            if (open) return;
            hp -= 1f;
            Fx.I.Sparks(transform.position + Vector3.up * 0.6f);
            if (hp <= 0f) Open();
        }

        public void Open()
        {
            if (open) return;
            open = true;
            var at = transform.position + Vector3.up * 0.6f;
            Fx.I.Burst(at, 20, new Color(1f, 0.8f, 0.2f), 7f, 0.16f);
            SoundBank.I.Play(SoundBank.I.coin, 0.9f, 0.9f);
            if (Game.I != null)
            {
                Game.I.AddCoins(15 + 5 * Game.I.LevelNumber, at);
                Game.I.GivePowerUp((PowerUp)Random.Range(0, 4));
            }
            Destroy(gameObject);
        }
    }
}
