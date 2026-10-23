using System;
using System.Collections.Generic;
using UnityEngine;

namespace Snake3D
{
    [Serializable]
    public sealed class SoloConfig
    {
        public string[] levels = { "easy", "normal", "hard" };
        public string speed = "normal"; public string obstacles = "few"; public int timeLimit; public int theme = -1;
    }

    [Serializable]
    public sealed class ChallengeConfig
    {
        public string speed = "normal"; public string obstacles = "few"; public int theme = -1;
    }

    [Serializable]
    public sealed class Counter { public int play; public int win; }

    [Serializable]
    public sealed class SaveData
    {
        public bool bgm = true; public float bgmVol = 0.5f; public bool sfx = true; public float sfxVol = 0.8f;
        public bool vibrate = true, reduceMotion = false;
        public string touchMode = "auto";    // auto / on / off
        public string stickMode = "float";   // float / fixed
        public string stickSide = "left";
        public string zoom = "normal";       // near / normal / far
        public bool minimap = true;
        public string quality = "normal";    // high / normal / low
        public string lang = "";
        public string nickname = ""; public string animal = "";
        public SoloConfig solo = new SoloConfig();
        public ChallengeConfig challenge = new ChallengeConfig();
        public List<string> bestKeys = new List<string>(); public List<int> bestVals = new List<int>();
        public Counter statSolo = new Counter(), statOnline = new Counter(), statChallenge = new Counter();
        public string clientKey = "";
    }

    /// <summary>本機設定與戰績（PlayerPrefs，只存在這台裝置）。</summary>
    public static class Store
    {
        const string KEY = "snake3d.save.v1";
        public static SaveData D;

        public static void Load()
        {
            SaveData d = null;
            try
            {
                string raw = PlayerPrefs.GetString(KEY, "");
                if (!string.IsNullOrEmpty(raw)) d = JsonUtility.FromJson<SaveData>(raw);
            }
            catch (Exception) { d = null; }
            bool fresh = d == null;
            if (d == null) d = new SaveData();
            if (d.solo == null) d.solo = new SoloConfig();
            if (d.solo.levels == null || d.solo.levels.Length == 0) d.solo.levels = new[] { "easy", "normal", "hard" };
            if (d.challenge == null) d.challenge = new ChallengeConfig();
            if (d.bestKeys == null) d.bestKeys = new List<string>();
            if (d.bestVals == null) d.bestVals = new List<int>();
            if (d.statSolo == null) d.statSolo = new Counter();
            if (d.statOnline == null) d.statOnline = new Counter();
            if (d.statChallenge == null) d.statChallenge = new Counter();
            if (string.IsNullOrEmpty(d.animal)) d.animal = Data.AnimalIds[UnityEngine.Random.Range(0, Data.AnimalIds.Length)];
            if (string.IsNullOrEmpty(d.clientKey)) d.clientKey = "u" + Guid.NewGuid().ToString("N").Substring(0, 20);
            D = d;
            if (fresh) Save();
        }

        public static void Save()
        {
            try { PlayerPrefs.SetString(KEY, JsonUtility.ToJson(D)); PlayerPrefs.Save(); }
            catch (Exception) { /* 儲存失敗就算了，不影響遊戲 */ }
        }

        public static void ResetSettings()
        {
            var f = new SaveData();
            D.bgm = f.bgm; D.bgmVol = f.bgmVol; D.sfx = f.sfx; D.sfxVol = f.sfxVol; D.vibrate = f.vibrate; D.reduceMotion = f.reduceMotion;
            D.touchMode = f.touchMode; D.stickMode = f.stickMode; D.stickSide = f.stickSide; D.zoom = f.zoom; D.minimap = f.minimap; D.quality = f.quality;
            Save();
        }

        public static string BestKey(string speed, string obstacles) { return "free-" + speed + "-" + obstacles; }

        public static int Best(string speed, string obstacles)
        {
            int i = D.bestKeys.IndexOf(BestKey(speed, obstacles));
            return i >= 0 ? D.bestVals[i] : 0;
        }

        public static bool SetBest(string speed, string obstacles, int score)
        {
            string k = BestKey(speed, obstacles);
            int i = D.bestKeys.IndexOf(k);
            if (i < 0) { D.bestKeys.Add(k); D.bestVals.Add(score); Save(); return score > 0; }
            if (score > D.bestVals[i]) { D.bestVals[i] = score; Save(); return true; }
            return false;
        }

        public static string ClientKey() { return D.clientKey; }
        public static void SetClientKey(string k) { if (!string.IsNullOrEmpty(k) && k != D.clientKey) { D.clientKey = k; Save(); } }

        public static void Record(Counter c, bool win) { c.play++; if (win) c.win++; Save(); }
    }
}
