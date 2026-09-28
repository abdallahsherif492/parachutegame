using System;
using UnityEngine;

namespace SkyDrop
{
    /// All sounds are synthesized at startup: no audio files in the build.
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank I;
        const int SR = 22050;

        public AudioClip coin, ring, pop, thud, land, beep, perfect, click, splash, shield, whoosh, buy, fail;
        AudioSource wind;
        AudioSource[] pool;
        int next;
        float coinChain;
        float lastCoin;

        void Awake()
        {
            I = this;
            var rnd = new System.Random(7);
            Func<float> noise = () => (float)rnd.NextDouble() * 2f - 1f;

            coin = Gen("coin", 0.22f, t => Sine((t < 0.06f ? 1320f : 1760f) * t) * Mathf.Exp(-t * 16f) * 0.35f);
            beep = Gen("beep", 0.07f, t => Square(1000f * t) * (1f - t / 0.07f) * 0.18f);
            click = Gen("click", 0.04f, t => Sine(1800f * t) * (1f - t / 0.04f) * 0.25f);
            float lp = 0f;
            ring = Gen("ring", 0.45f, t =>
            {
                lp += 0.25f * (noise() - lp);
                return lp * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.5f + Sine((880f + 900f * t) * t) * Mathf.Exp(-t * 7f) * 0.25f;
            });
            lp = 0f;
            pop = Gen("pop", 0.55f, t =>
            {
                lp += 0.15f * (noise() - lp);
                return Sine(95f * t) * Mathf.Exp(-t * 9f) * 0.7f + lp * Mathf.Exp(-t * 14f) * 1.2f;
            });
            lp = 0f;
            thud = Gen("thud", 0.7f, t =>
            {
                lp += 0.1f * (noise() - lp);
                return Sine(55f * t) * Mathf.Exp(-t * 6f) * 0.8f + lp * Mathf.Exp(-t * 8f) * 1.6f;
            });
            land = Gen("land", 0.3f, t => Sine(130f * t) * Mathf.Exp(-t * 14f) * 0.6f);
            lp = 0f;
            splash = Gen("splash", 0.9f, t =>
            {
                lp += 0.35f * (noise() - lp);
                return lp * Mathf.Exp(-t * 4f) * 0.9f;
            });
            shield = Gen("shield", 0.35f, t => Sine((1500f - 2500f * t) * t) * Mathf.Exp(-t * 5f) * 0.3f);
            lp = 0f;
            whoosh = Gen("whoosh", 0.45f, t =>
            {
                lp += 0.2f * (noise() - lp);
                return lp * Mathf.Sin(Mathf.PI * t / 0.45f) * 0.9f;
            });
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            perfect = Gen("perfect", 0.6f, t =>
            {
                int n = Mathf.Min(3, (int)(t / 0.1f));
                float lt = t - n * 0.1f;
                return (Sine(notes[n] * t) + Sine(notes[n] * 2f * t) * 0.3f) * Mathf.Exp(-lt * 8f) * 0.25f;
            });
            buy = Gen("buy", 0.3f, t => Sine((t < 0.08f ? 660f : 990f) * t) * Mathf.Exp(-t * 9f) * 0.3f);
            fail = Gen("fail", 0.6f, t => Square((300f - 250f * t) * t) * Mathf.Exp(-t * 4f) * 0.12f);

            // Wind loop: brown noise with a crossfaded seam.
            int n0 = (int)(SR * 2.2f), fade = (int)(SR * 0.2f);
            var data = new float[n0];
            float b = 0f, lp2 = 0f;
            for (int i = 0; i < n0; i++)
            {
                b = (b + 0.03f * noise()) / 1.015f;
                lp2 += 0.3f * (b - lp2);
                data[i] = lp2 * 3.5f;
            }
            var loop = new float[n0 - fade];
            for (int i = 0; i < loop.Length; i++)
            {
                float v = data[i];
                if (i < fade)
                {
                    float k = i / (float)fade;
                    v = data[i] * k + data[n0 - fade + i] * (1f - k);
                }
                loop[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var windClip = AudioClip.Create("wind", loop.Length, 1, SR, false);
            windClip.SetData(loop, 0);

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = windClip;
            wind.loop = true;
            wind.volume = 0f;
            wind.playOnAwake = false;
            wind.Play();

            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
        }

        static float Sine(float phaseCycles) { return Mathf.Sin(phaseCycles * Mathf.PI * 2f); }
        static float Square(float phaseCycles) { return Mathf.Repeat(phaseCycles, 1f) < 0.5f ? 1f : -1f; }

        static AudioClip Gen(string name, float dur, Func<float, float> f)
        {
            int n = Mathf.Max(1, (int)(dur * SR));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = f(i / (float)SR);
                // tiny fade-in/out to avoid clicks
                float edge = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / 60f);
                data[i] = Mathf.Clamp(v * edge, -1f, 1f);
            }
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume);
        }

        /// Coins rise in pitch when collected in quick succession.
        public void Coin()
        {
            coinChain = Time.unscaledTime - lastCoin < 0.6f ? Mathf.Min(coinChain + 1f, 12f) : 0f;
            lastCoin = Time.unscaledTime;
            Play(coin, 0.8f, 1f + coinChain * 0.06f);
        }

        public void SetWind(float intensity, float pitch)
        {
            wind.volume = Mathf.Lerp(wind.volume, Mathf.Clamp01(intensity) * 0.55f, 0.1f);
            wind.pitch = Mathf.Lerp(wind.pitch, pitch, 0.1f);
        }

        public static void ApplyMute()
        {
            AudioListener.volume = SaveSystem.Data != null && SaveSystem.Data.muted ? 0f : 1f;
        }
    }
}
