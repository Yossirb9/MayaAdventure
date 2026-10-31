using System;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>All sound effects and the background tune are synthesized at startup.</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        enum Wave { Sine, Square, Triangle }

        AudioSource fx, music;
        AudioClip[] jumps;
        public AudioClip Coin, RedCoin, Stomp, Hurt, Star, Spring, Splash, Pound, Block, OneUp, Checkpoint, Reveal;
        public AudioClip Swoosh, PowerUp, Boom, Warp, Fuse, Tick;
        /// <summary>Maya's voice cries (Resources/Voice): yesh, wow, ouch, yay, hop.</summary>
        public AudioClip SayYesh, SayWow, SayOuch, SayYay, SayHop;
        float lastSay = -10f;
        int world = -1;
        readonly AudioClip[] worldMusic = new AudioClip[5];

        void Awake()
        {
            fx = gameObject.AddComponent<AudioSource>();
            fx.playOnAwake = false;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.2f;

            Coin = Sweep("coin", 0.3f, t => t < 0.07f ? 1319f : 1976f, Wave.Square, 0.18f);
            RedCoin = Seq("redcoin", new[] { 79, 83, 86, 91 }, 0.06f, Wave.Square, 0.18f, 0.2f);
            jumps = new[]
            {
                Sweep("jump1", 0.2f, t => Mathf.Lerp(330f, 660f, t / 0.2f), Wave.Square, 0.14f),
                Sweep("jump2", 0.22f, t => Mathf.Lerp(390f, 820f, t / 0.22f), Wave.Square, 0.14f),
                Sweep("jump3", 0.34f, t => Mathf.Lerp(440f, 1150f, t / 0.34f), Wave.Square, 0.14f),
            };
            Stomp = Sweep("stomp", 0.18f, t => Mathf.Lerp(420f, 90f, t / 0.18f), Wave.Triangle, 0.6f);
            Hurt = Sweep("hurt", 0.4f, t => Mathf.Lerp(700f, 140f, t / 0.4f) + 30f * Mathf.Sin(t * 70f), Wave.Square, 0.16f);
            Star = Seq("star", new[] { 72, 76, 79, 84, 88, 91, 96 }, 0.085f, Wave.Square, 0.16f, 0.6f);
            Spring = Sweep("spring", 0.45f, t => 220f + 900f * t + 90f * Mathf.Sin(t * 110f), Wave.Triangle, 0.5f);
            Splash = Noise("splash", 0.6f, 0.1f, 0.6f);
            Pound = Noise("pound", 0.3f, 0.04f, 1.2f);
            Block = Sweep("block", 0.12f, t => 180f, Wave.Triangle, 0.6f);
            OneUp = Seq("oneup", new[] { 76, 79, 88, 84, 86, 91 }, 0.09f, Wave.Square, 0.16f, 0.2f);
            Checkpoint = Seq("checkpoint", new[] { 72, 79, 84 }, 0.1f, Wave.Triangle, 0.45f, 0.3f);
            Reveal = Seq("reveal", new[] { 84, 83, 84, 88, 91, 96 }, 0.08f, Wave.Triangle, 0.45f, 0.4f);

            Swoosh = Noise("swoosh", 0.16f, 0.35f, 0.35f);
            PowerUp = Seq("powerup", new[] { 67, 71, 74, 79, 83, 86, 91 }, 0.06f, Wave.Square, 0.15f, 0.35f);
            Boom = Noise("boom", 0.7f, 0.03f, 1.4f);
            Warp = Sweep("warp", 0.55f, t => Mathf.Lerp(900f, 120f, t / 0.55f) + 60f * Mathf.Sin(t * 90f), Wave.Square, 0.14f);
            Fuse = Sweep("fuse", 0.08f, t => 1400f, Wave.Square, 0.08f);
            Tick = Sweep("tick", 0.05f, t => 2000f, Wave.Triangle, 0.3f);
            SayYesh = Resources.Load<AudioClip>("Voice/yesh");
            SayWow = Resources.Load<AudioClip>("Voice/wow");
            SayOuch = Resources.Load<AudioClip>("Voice/ouch");
            SayYay = Resources.Load<AudioClip>("Voice/yay");
            SayHop = Resources.Load<AudioClip>("Voice/hop");
            SetWorld(0);
            music.mute = !GameSettings.Music;
        }

        public void SetMusic(bool on)
        {
            if (music != null) music.mute = !on;
        }

        /// <summary>Told about every sound effect played (the trailer recorder rebuilds the soundtrack from these).</summary>
        public static System.Action<AudioClip, float> Listener;

        public AudioClip MusicClip => music != null ? music.clip : null;

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            fx.PlayOneShot(clip, volume);
            Listener?.Invoke(clip, volume);
        }

        public void Jump(int chain) => Play(jumps[Mathf.Clamp(chain, 0, 2)], 0.8f);

        /// <summary>Maya says something (never two cries on top of each other).</summary>
        public void Say(AudioClip clip, float chance = 1f)
        {
            if (clip == null || Time.time - lastSay < 0.7f || UnityEngine.Random.value > chance) return;
            lastSay = Time.time;
            Play(clip, 0.9f);
        }

        /// <summary>Each world has its own tune (built the first time Maya visits it).</summary>
        public void SetWorld(int index)
        {
            index = Mathf.Clamp(index, 0, worldMusic.Length - 1);
            if (index == world && music.isPlaying) return;
            world = index;
            if (worldMusic[index] == null) worldMusic[index] = BuildMusic(index);
            music.clip = worldMusic[index];
            music.Play();
        }

        // ---------- synthesis ----------

        static float Osc(Wave w, float phase)
        {
            switch (w)
            {
                case Wave.Square: return phase < 0.5f ? 1f : -1f;
                case Wave.Triangle: return 4f * Mathf.Abs(phase - 0.5f) - 1f;
                default: return Mathf.Sin(phase * Mathf.PI * 2f);
            }
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Sweep(string name, float dur, Func<float, float> freq, Wave w, float vol)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                phase += freq(t) / Rate;
                phase -= Mathf.Floor(phase);
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Pow(1f - t / dur, 1.5f);
                d[i] = Osc(w, phase) * env * vol;
            }
            return Make(name, d);
        }

        static AudioClip Seq(string name, int[] notes, float noteLen, Wave w, float vol, float lastHold)
        {
            float dur = notes.Length * noteLen + lastHold;
            var d = new float[Mathf.CeilToInt(dur * Rate)];
            for (int k = 0; k < notes.Length; k++)
            {
                float len = noteLen + (k == notes.Length - 1 ? lastHold : 0f);
                AddNote(d, k * noteLen, len, Midi(notes[k]), w, vol);
            }
            return Make(name, d);
        }

        static AudioClip Noise(string name, float dur, float smooth, float vol)
        {
            var rng = new System.Random(name.GetHashCode());
            int n = Mathf.CeilToInt(dur * Rate);
            var d = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                y += ((float)rng.NextDouble() * 2f - 1f - y) * smooth;
                d[i] = y * vol * Mathf.Pow(1f - t / dur, 2f);
            }
            return Make(name, d);
        }

        static void AddNote(float[] d, float start, float len, float freq, Wave w, float vol)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(len * Rate);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Min(1f, (len - t) / 0.03f) * (1f - 0.4f * t / len);
                float phase = t * freq;
                d[s0 + i] += Osc(w, phase - Mathf.Floor(phase)) * env * vol;
            }
        }

        static AudioClip BuildMusic(int index)
        {
            switch (index)
            {
                case 1: // Snowy Peaks: a gentle bell tune in A minor
                    return Tune("music_snow", 112f, Wave.Triangle, 0.13f, Wave.Sine, 0.3f, 0.015f, new[]
                    {
                        69, 72, 76, 72, 74, 72, 71, 69, 67, 71, 74, 71, 72, 71, 69, 67,
                        65, 69, 72, 69, 71, 69, 67, 65, 64, 0, 68, 71, 76, 0, 71, 0,
                        69, 72, 76, 81, 79, 76, 74, 72, 71, 74, 79, 74, 76, 74, 72, 71,
                        69, 72, 77, 72, 76, 72, 71, 68, 69, 0, 64, 0, 69, 0, 0, 0,
                    }, new[]
                    {
                        45, 52, 45, 52, 43, 50, 43, 50, 41, 48, 41, 48, 40, 47, 40, 47,
                        45, 52, 45, 52, 43, 50, 43, 50, 41, 48, 40, 47, 45, 52, 45, 0,
                    });
                case 2: // Coral Lagoon: a lazy island sway
                    return Tune("music_lagoon", 96f, Wave.Sine, 0.16f, Wave.Triangle, 0.28f, 0.035f, new[]
                    {
                        72, 0, 74, 76, 0, 79, 76, 0, 77, 0, 76, 74, 0, 72, 69, 0,
                        72, 0, 74, 76, 0, 79, 81, 0, 79, 0, 76, 0, 74, 0, 72, 0,
                        69, 72, 74, 0, 76, 74, 72, 0, 71, 74, 76, 0, 79, 76, 74, 0,
                        72, 76, 79, 81, 79, 76, 74, 76, 72, 0, 0, 0, 72, 0, 0, 0,
                    }, new[]
                    {
                        48, 0, 55, 48, 53, 0, 60, 53, 48, 0, 55, 48, 55, 0, 50, 55,
                        45, 0, 52, 45, 47, 0, 55, 47, 48, 52, 55, 53, 48, 0, 48, 0,
                    });
                case 3: // Sky Fortress: a quick heroic march in D major
                    return Tune("music_sky", 144f, Wave.Square, 0.085f, Wave.Triangle, 0.26f, 0.04f, new[]
                    {
                        74, 0, 74, 78, 81, 0, 78, 74, 76, 0, 76, 79, 83, 0, 79, 76,
                        78, 81, 86, 81, 79, 78, 76, 74, 73, 0, 76, 0, 81, 0, 0, 0,
                        74, 0, 74, 78, 81, 0, 78, 81, 83, 81, 79, 78, 76, 78, 79, 81,
                        86, 0, 83, 0, 81, 79, 78, 76, 74, 0, 69, 0, 74, 0, 0, 0,
                    }, new[]
                    {
                        50, 57, 50, 57, 52, 59, 52, 59, 54, 61, 55, 62, 57, 64, 57, 0,
                        50, 57, 50, 57, 55, 62, 52, 59, 54, 61, 57, 64, 50, 57, 50, 0,
                    });
                case 4: // Twilight Woods: a mysterious walk in E minor
                    return Tune("music_dusk", 104f, Wave.Triangle, 0.13f, Wave.Sine, 0.32f, 0.02f, new[]
                    {
                        64, 67, 71, 67, 72, 71, 67, 64, 62, 66, 69, 66, 71, 69, 66, 62,
                        60, 64, 67, 64, 69, 67, 64, 60, 59, 0, 63, 66, 71, 0, 66, 0,
                        64, 67, 71, 76, 74, 71, 67, 71, 72, 69, 66, 69, 71, 67, 64, 67,
                        69, 66, 63, 66, 67, 64, 63, 59, 64, 0, 0, 0, 64, 0, 0, 0,
                    }, new[]
                    {
                        40, 47, 40, 47, 38, 45, 38, 45, 36, 43, 36, 43, 35, 42, 35, 42,
                        40, 47, 40, 47, 45, 52, 40, 47, 45, 42, 35, 42, 40, 47, 40, 0,
                    });
            }
            // Sunny Island: an original cheerful 8-bar loop.
            int[] melody =
            {
                72, 76, 79, 76, 77, 76, 74, 72,
                69, 72, 77, 72, 76, 74, 72, 69,
                71, 74, 79, 74, 77, 76, 74, 71,
                72, 0, 76, 79, 84, 0, 79, 0,
                69, 72, 76, 72, 74, 72, 71, 69,
                65, 69, 72, 77, 76, 74, 72, 0,
                74, 76, 77, 79, 81, 79, 77, 74,
                76, 0, 72, 0, 72, 0, 0, 0,
            };
            int[] bass =
            {
                48, 55, 48, 55, 53, 60, 53, 60, 55, 62, 55, 62, 48, 55, 48, 55,
                57, 64, 57, 64, 53, 60, 53, 60, 55, 62, 55, 62, 48, 55, 48, 0,
            };
            return Tune("music", 132f, Wave.Square, 0.09f, Wave.Triangle, 0.25f, 0.03f, melody, bass);
        }

        /// <summary>A looping tune: melody in eighth notes, bass in quarter notes, a soft noise tick on every eighth.</summary>
        static AudioClip Tune(string name, float bpm, Wave lead, float leadVol, Wave low, float bassVol, float tick, int[] melody, int[] bass)
        {
            float eighth = 60f / bpm / 2f;
            var d = new float[Mathf.CeilToInt(melody.Length * eighth * Rate)];
            for (int k = 0; k < melody.Length; k++)
                if (melody[k] != 0) AddNote(d, k * eighth, eighth * 0.9f, Midi(melody[k]), lead, leadVol);
            for (int k = 0; k < bass.Length; k++)
                if (bass[k] != 0) AddNote(d, k * 2 * eighth, eighth * 1.8f, Midi(bass[k]), low, bassVol);
            var rng = new System.Random(5);
            for (int k = 0; k < melody.Length; k++)
            {
                int s0 = (int)(k * eighth * Rate);
                for (int i = 0; i < 1200 && s0 + i < d.Length; i++)
                    d[s0 + i] += ((float)rng.NextDouble() * 2f - 1f) * tick * (1f - i / 1200f);
            }
            return Make(name, d);
        }
    }
}
