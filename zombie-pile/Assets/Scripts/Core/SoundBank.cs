using System;
using UnityEngine;

namespace ZombiePile
{
    /// All sounds are synthesized at startup: no audio files in the build.
    public class SoundBank : MonoBehaviour
    {
        public static SoundBank I;
        const int SR = 22050;

        public AudioClip shot, hit, headshot, boom, breach, click, horn, chime, lose, thump, throwS;
        public AudioClip[] groans;
        AudioSource[] pool;
        int next;

        void Awake()
        {
            I = this;
            var rnd = new System.Random(11);
            Func<float> noise = () => (float)rnd.NextDouble() * 2f - 1f;
            float lp = 0f;

            shot = Gen("shot", 0.16f, t =>
            {
                lp += 0.55f * (noise() - lp);
                return lp * Mathf.Exp(-t * 32f) * 0.9f + Sine(160f * t) * Mathf.Exp(-t * 40f) * 0.5f;
            });
            lp = 0f;
            hit = Gen("hit", 0.12f, t =>
            {
                lp += 0.25f * (noise() - lp);
                return lp * Mathf.Exp(-t * 30f) * 0.9f + Sine((260f - 900f * t) * t) * Mathf.Exp(-t * 25f) * 0.4f;
            });
            headshot = Gen("headshot", 0.25f, t => Sine((900f - 1800f * t) * t) * Mathf.Exp(-t * 14f) * 0.4f + Sine(2200f * t) * Mathf.Exp(-t * 30f) * 0.15f);
            lp = 0f;
            float lp2 = 0f;
            boom = Gen("boom", 1.2f, t =>
            {
                lp += 0.12f * (noise() - lp);
                lp2 += 0.6f * (noise() - lp2);
                return lp * Mathf.Exp(-t * 3.2f) * 2.2f + lp2 * Mathf.Exp(-t * 18f) * 0.5f + Sine(48f * t) * Mathf.Exp(-t * 5f) * 0.7f;
            });
            lp = 0f;
            breach = Gen("breach", 0.5f, t =>
            {
                lp += 0.3f * (noise() - lp);
                return lp * Mathf.Exp(-t * 7f) * 1.1f + Square((110f - 60f * t) * t) * Mathf.Exp(-t * 6f) * 0.2f;
            });
            click = Gen("click", 0.04f, t => Sine(1800f * t) * (1f - t / 0.04f) * 0.25f);
            horn = Gen("horn", 1.1f, t =>
            {
                float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-Mathf.Max(0f, t - 0.7f) * 8f);
                return (Square(220f * t) * 0.5f + Square(221.5f * t) * 0.5f + Sine(110f * t)) * env * 0.12f;
            });
            float[] notes = { 523.25f, 659.25f, 783.99f };
            chime = Gen("chime", 0.5f, t =>
            {
                int n = Mathf.Min(2, (int)(t / 0.09f));
                float lt = t - n * 0.09f;
                return (Sine(notes[n] * t) + Sine(notes[n] * 2f * t) * 0.3f) * Mathf.Exp(-lt * 8f) * 0.25f;
            });
            lose = Gen("lose", 1.2f, t => Square((220f - 120f * t) * t) * Mathf.Exp(-t * 2.5f) * 0.12f + Sine((110f - 50f * t) * t) * Mathf.Exp(-t * 2f) * 0.3f);
            lp = 0f;
            thump = Gen("thump", 0.25f, t => { lp += 0.2f * (noise() - lp); return Sine(70f * t) * Mathf.Exp(-t * 18f) * 0.7f + lp * Mathf.Exp(-t * 25f) * 0.6f; });
            lp = 0f;
            throwS = Gen("throw", 0.3f, t => { lp += 0.2f * (noise() - lp); return lp * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.8f; });

            // groans: a low buzzy voice with a falling pitch and a noisy throat
            groans = new AudioClip[4];
            for (int g = 0; g < groans.Length; g++)
            {
                float f0 = 90f + g * 18f, dur = 0.8f + g * 0.15f, ph = 0f;
                lp = 0f;
                groans[g] = Gen("groan" + g, dur, t =>
                {
                    float f = f0 * (1.15f - 0.35f * t / dur) * (1f + 0.04f * Mathf.Sin(t * 30f));
                    ph += f / SR;
                    lp += 0.2f * (noise() - lp);
                    float env = Mathf.Sin(Mathf.PI * t / dur);
                    float saw = Mathf.Repeat(ph, 1f) * 2f - 1f;
                    return (saw * 0.35f + lp * 0.5f) * env * 0.35f;
                });
            }

            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            ApplyMute();
        }

        static float Sine(float cycles) { return Mathf.Sin(cycles * Mathf.PI * 2f); }
        static float Square(float cycles) { return Mathf.Repeat(cycles, 1f) < 0.5f ? 1f : -1f; }

        static AudioClip Gen(string name, float dur, Func<float, float> f)
        {
            int n = Mathf.Max(1, (int)(dur * SR));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = f(i / (float)SR);
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

        public void Groan()
        {
            Play(groans[UnityEngine.Random.Range(0, groans.Length)], 0.45f, UnityEngine.Random.Range(0.8f, 1.15f));
        }

        public static void ApplyMute()
        {
            AudioListener.volume = Save.Data != null && Save.Data.muted ? 0f : 1f;
        }
    }
}
