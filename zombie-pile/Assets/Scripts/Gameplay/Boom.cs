using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombiePile
{
    /// Explosions (barrels, drums, airstrikes, boomers): damage and throw zombies, chain other drums, open crates.
    public static class Boom
    {
        static readonly Collider[] found = new Collider[256];
        static readonly HashSet<Zombie> done = new HashSet<Zombie>();

        /// Just the look and sound of an explosion (clients get this from the host).
        public static void Fx_(Vector3 at, float radius, bool big)
        {
            Fx.I.Explosion(at, radius);
            SoundBank.I.Play(SoundBank.I.boom, big ? 1f : 0.8f, Random.Range(0.88f, 1.05f));
            CameraRig.I.Shake(big ? 0.9f : 0.6f);
        }

        public static void Explode(Vector3 at, float radius, float dmg, float force, bool big)
        {
            Fx_(at, radius, big);
            if (Net.IsHost) Net.Host.Explosion(at, radius, big);
            int mask = (1 << Zombie.Layer) | (1 << Arena.Props);
            int n = Physics.OverlapSphereNonAlloc(at, radius, found, mask, QueryTriggerInteraction.Collide);
            done.Clear();
            for (int i = 0; i < n; i++)
            {
                var c = found[i];
                if (c == null) continue;
                var drum = c.GetComponentInParent<ExplosiveBarrel>();
                if (drum != null) { drum.Detonate(0.12f); continue; }   // chain reaction
                var crate = c.GetComponentInParent<Crate>();
                if (crate != null) { crate.Open(); continue; }
                var z = c.GetComponentInParent<Zombie>();
                if (z == null || done.Contains(z)) continue;
                done.Add(z);
                if (z.Blast(at, radius, dmg * Game.DamageBoost, force) && Hud.I != null) Game.Dmg(z.Center, Mathf.RoundToInt(dmg * Game.DamageBoost), false);
            }
        }
    }

    /// An explosive barrel lobbed by the wall gunner.
    public class ThrownBarrel : MonoBehaviour
    {
        Vector3 from, to;
        float t, flight, radius, dmg;

        bool explodes = true;

        /// explodes = false: only the flight (a client showing the host's barrel; the blast arrives as its own event).
        public static void Throw(Vector3 from, Vector3 to, float radius, float dmg, bool explodes = true)
        {
            var go = new GameObject("ThrownBarrel");
            var b = go.AddComponent<ThrownBarrel>();
            b.from = from; b.to = to; b.radius = radius; b.dmg = dmg; b.explodes = explodes;
            b.flight = Mathf.Clamp(Vector3.Distance(from, to) / 20f, 0.45f, 1.05f);
            var m = Kit.Place("Barrel", Vector3.zero, 0f, 0.85f, go.transform, true);
            if (m != null) m.transform.localPosition = Vector3.down * 0.45f;
            go.transform.position = from;
        }

        void Update()
        {
            t += Time.deltaTime / flight;
            var p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 3.5f;
            transform.position = p;
            transform.Rotate(new Vector3(420f, 80f, 0f) * Time.deltaTime);
            if (t < 1f) return;
            if (explodes) Boom.Explode(to + Vector3.up * 0.4f, radius, dmg, 12f, true);
            Destroy(gameObject);
        }
    }

    /// A carpet of bombs along the street, centred on the aim point.
    public static class Airstrike
    {
        public static void Call(Vector3 aim)
        {
            if (Game.I == null) return;
            SoundBank.I.Play(SoundBank.I.jet, 0.9f);
            Game.I.StartCoroutine(Run(aim));
        }

        /// A client only hears the jet: the bombs arrive as explosions.
        public static void CallFxOnly() { SoundBank.I.Play(SoundBank.I.jet, 0.9f); }

        static IEnumerator Run(Vector3 aim)
        {
            yield return new WaitForSeconds(0.55f);
            float x = Mathf.Clamp(aim.x, -Arena.HalfWidth + 1f, Arena.HalfWidth - 1f);
            float z0 = Mathf.Max(Arena.WallFront + 1f, aim.z - 7f);
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(x + Random.Range(-1.2f, 1.2f), 0.4f, z0 + i * 2.3f);
                Boom.Explode(p, 3.3f, 140f, 12f, i == 3);
                yield return new WaitForSeconds(0.09f);
            }
        }
    }
}
