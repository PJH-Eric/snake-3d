using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    /// <summary>
    /// 即時合成的音效與背景音樂（沒有外部音檔，授權單純）。
    /// 要換成正式音檔時：保留 Sound.Sfx(name) / Sound.Music(mode) 這兩個介面，內部改成播放 AudioClip 即可。
    /// </summary>
    public static class Sound
    {
        const int RATE = 22050;
        static AudioSource bgmSrc;
        static AudioSource[] sfxSrc;
        static int sfxNext;
        static readonly Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioClip> bgmClips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastAt = new Dictionary<string, float>();
        static string mode;
        static bool bgm = true, sfx = true; static float bgmVol = 0.5f, sfxVol = 0.8f;
        static System.Random rnd = new System.Random(12345);

        public static void Init(GameObject host)
        {
            bgmSrc = host.AddComponent<AudioSource>();
            bgmSrc.loop = true; bgmSrc.playOnAwake = false; bgmSrc.spatialBlend = 0;
            sfxSrc = new AudioSource[6];
            for (int i = 0; i < sfxSrc.Length; i++) { sfxSrc[i] = host.AddComponent<AudioSource>(); sfxSrc[i].playOnAwake = false; sfxSrc[i].spatialBlend = 0; }
            Apply();
        }

        public static void Apply()
        {
            var d = Store.D;
            bgm = d.bgm; bgmVol = d.bgmVol; sfx = d.sfx; sfxVol = d.sfxVol;
            if (bgmSrc == null) return;
            bgmSrc.volume = bgm ? bgmVol * 0.45f : 0f;
            for (int i = 0; i < sfxSrc.Length; i++) sfxSrc[i].volume = sfx ? sfxVol : 0f;
            if (bgmSrc.clip != null && bgm && !bgmSrc.isPlaying && mode != null) bgmSrc.Play();
        }

        // ---------- 合成 ----------
        sealed class Buf
        {
            public float[] D;
            public Buf(double sec) { D = new float[(int)(sec * RATE)]; }
            public void Tone(double freq, double t0, double dur, string type, double vol, double slideTo = 0)
            {
                int s0 = (int)(t0 * RATE), n = (int)((dur + 0.02) * RATE);
                double phase = 0;
                for (int i = 0; i < n && s0 + i < D.Length; i++)
                {
                    double t = (double)i / RATE;
                    double f = freq;
                    if (slideTo > 0 && dur > 0) f = freq * Math.Pow(Math.Max(20, slideTo) / freq, Math.Min(1, t / dur));
                    phase += f / RATE;
                    double p = phase - Math.Floor(phase), w;
                    if (type == "triangle") w = 4 * Math.Abs(p - 0.5) - 1; else w = Math.Sin(p * Math.PI * 2);
                    // 包絡：12ms 淡入、指數衰減
                    double env = t < 0.012 ? t / 0.012 : Math.Exp(-6.9 * (t - 0.012) / Math.Max(0.02, dur));
                    if (s0 >= 0) D[s0 + i] += (float)(w * vol * env);
                }
            }
            public void Noise(double t0, double dur, double vol, bool highpass)
            {
                int s0 = (int)(t0 * RATE), n = (int)(dur * RATE);
                double lp = 0;
                for (int i = 0; i < n && s0 + i < D.Length; i++)
                {
                    double x = rnd.NextDouble() * 2 - 1;
                    lp += (x - lp) * 0.25;
                    double y = highpass ? x - lp : lp;
                    double env = Math.Exp(-6.9 * (double)i / n);
                    D[s0 + i] += (float)(y * vol * env);
                }
            }
            public AudioClip ToClip(string name, bool normalize)
            {
                float peak = 0.0001f;
                for (int i = 0; i < D.Length; i++) peak = Math.Max(peak, Math.Abs(D[i]));
                if (normalize || peak > 0.95f) { float k = 0.9f / peak; if (!normalize && k > 1) k = 1; for (int i = 0; i < D.Length; i++) D[i] *= k; }
                var c = AudioClip.Create(name, D.Length, 1, RATE, false);
                c.SetData(D, 0);
                return c;
            }
        }

        static AudioClip MakeSfx(string name)
        {
            var b = new Buf(1.2);
            switch (name)
            {
                case "click": b.Tone(760, 0, 0.07, "sine", 0.22, 520); break;
                case "eat": b.Tone(520, 0, 0.08, "triangle", 0.4, 880); b.Tone(880, 0.06, 0.1, "sine", 0.3); b.Noise(0, 0.03, 0.08, true); break;
                case "gold": { double[] f = { 784, 988, 1319, 1760 }; for (int i = 0; i < f.Length; i++) b.Tone(f[i], i * 0.05, 0.16, "triangle", 0.34); break; }
                case "boost": b.Noise(0, 0.2, 0.18, true); break;
                case "item": { double[] f = { 660, 880, 1320 }; for (int i = 0; i < f.Length; i++) b.Tone(f[i], i * 0.06, 0.14, "triangle", 0.34); break; }
                case "die": b.Tone(520, 0, 0.45, "triangle", 0.32, 90); b.Noise(0, 0.22, 0.28, false); break;
                case "count": b.Tone(880, 0, 0.12, "sine", 0.4); break;
                case "go": b.Tone(1320, 0, 0.25, "triangle", 0.45); b.Tone(1760, 0.08, 0.35, "triangle", 0.35); break;
                case "win": { double[] f = { 523, 659, 784, 1047, 784, 1047, 1319 }; for (int i = 0; i < f.Length; i++) b.Tone(f[i], i * 0.11, 0.24, "triangle", 0.4); break; }
                case "lose": { double[] f = { 440, 392, 349, 294 }; for (int i = 0; i < f.Length; i++) b.Tone(f[i], i * 0.16, 0.3, "sine", 0.35); break; }
                case "draw": { double[] f = { 523, 523, 392 }; for (int i = 0; i < f.Length; i++) b.Tone(f[i], i * 0.14, 0.26, "triangle", 0.35); break; }
                case "join": b.Tone(660, 0, 0.1, "sine", 0.25); b.Tone(880, 0.08, 0.14, "sine", 0.25); break;
                case "chat": b.Tone(980, 0, 0.06, "sine", 0.15); break;
                default: return null;
            }
            // 裁掉尾端靜音
            int last = b.D.Length - 1;
            while (last > 100 && Math.Abs(b.D[last]) < 0.0005f) last--;
            Array.Resize(ref b.D, Math.Min(b.D.Length, last + 200));
            return b.ToClip("sfx_" + name, false);
        }

        public static void Sfx(string name)
        {
            if (!sfx || sfxSrc == null) return;
            float now = Time.unscaledTime, gap = name == "eat" ? 0.04f : 0.02f, last;
            if (lastAt.TryGetValue(name, out last) && now - last < gap) return;
            lastAt[name] = now;
            AudioClip c;
            if (!sfxClips.TryGetValue(name, out c)) { c = MakeSfx(name); sfxClips[name] = c; }
            if (c == null) return;
            var src = sfxSrc[sfxNext++ % sfxSrc.Length];
            src.volume = sfxVol;
            src.PlayOneShot(c);
        }

        // ---------- 背景音樂：音樂盒風五聲音階，戰鬥版速度加快 ----------
        static readonly int[] PENT = { 0, 2, 4, 7, 9 };
        static int Degree(int d) { return PENT[((d % 5) + 5) % 5] + 12 * (int)Math.Floor(d / 5.0); }
        static double Hz(int semi, double b) { return b * Math.Pow(2, semi / 12.0); }
        const int N = -99;
        static readonly int[] MEL_MENU = {
            4,N,5,N,7,N,5,N,6,N,4,N,2,N,N,N,
            4,N,5,N,7,N,9,N,8,N,7,N,5,N,N,N,
            7,N,8,N,9,N,8,N,7,N,5,N,4,N,N,N,
            2,N,4,N,5,N,4,N,2,N,0,N,0,N,N,N };
        static readonly int[] MEL_BATTLE = {
            7,N,7,9,8,N,7,N,5,N,5,7,6,N,4,N,
            7,N,7,9,10,N,9,N,8,N,7,5,6,N,7,N,
            9,N,9,8,7,N,5,N,4,N,5,7,5,N,N,N,
            2,4,5,7,5,4,2,4,0,N,2,N,0,N,N,N };
        static readonly int[] BASS_MENU = { 0, 0, 0, 0, 3, 3, 3, 3, 4, 4, 4, 4, 2, 2, 2, 2 };
        static readonly int[] BASS_BATTLE = { 0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 3, 4, 4, 2, 2 };

        static AudioClip MakeBgm(string m)
        {
            bool battle = m == "battle";
            var mel = battle ? MEL_BATTLE : MEL_MENU; var bass = battle ? BASS_BATTLE : BASS_MENU;
            double sixteenth = 60.0 / (battle ? 140 : 108) / 4;
            double total = mel.Length * sixteenth;
            var b = new Buf(total + 0.8);
            for (int i = 0; i < mel.Length; i++)
            {
                double t = i * sixteenth;
                if (mel[i] != N)
                {
                    double f = Hz(Degree(mel[i]), 523.25 / 2);
                    b.Tone(f, t, 0.5, "sine", 0.5); b.Tone(f * 2, t, 0.22, "sine", 0.12);
                }
                if (i % 4 == 0) b.Tone(Hz(Degree(bass[(i / 4) % bass.Length]), 130.8), t, sixteenth * 3.6, "triangle", 0.42);
                if (battle ? i % 2 == 1 : i % 8 == 4) b.Noise(t, 0.04, 0.1, true);
            }
            // 尾巴接回開頭，讓循環不斷音
            int loopLen = (int)(total * RATE);
            for (int i = loopLen; i < b.D.Length; i++) b.D[i - loopLen] += b.D[i];
            Array.Resize(ref b.D, loopLen);
            return b.ToClip("bgm_" + m, true);
        }

        public static void Music(string next)
        {
            if (next == mode) return;
            mode = next;
            if (bgmSrc == null) return;
            if (mode == null) { bgmSrc.Stop(); return; }
            AudioClip c;
            if (!bgmClips.TryGetValue(mode, out c)) { c = MakeBgm(mode); bgmClips[mode] = c; }
            bgmSrc.clip = c;
            bgmSrc.volume = bgm ? bgmVol * 0.45f : 0f;
            bgmSrc.Play();
        }
    }
}
