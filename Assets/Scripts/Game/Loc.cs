using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Snake3D
{
    /// <summary>繁體中文／English 兩種語言。字典由 Backend~/public/js/locales.js 轉出（LocData）。</summary>
    public static class Loc
    {
        public const string ZH = "zh-Hant", EN = "en";
        static Dictionary<string, string> zh, en;
        public static string Lang = ZH;
        public static event Action Changed;

        static Dictionary<string, string> Build(string[] flat)
        {
            var d = new Dictionary<string, string>(flat.Length / 2);
            for (int i = 0; i + 1 < flat.Length; i += 2) d[flat[i]] = flat[i + 1];
            return d;
        }

        public static void Init(string saved, string systemLang)
        {
            zh = Build(LocData.Zh); en = Build(LocData.En);
            if (saved == ZH || saved == EN) Lang = saved;
            else Lang = systemLang == "English" ? EN : ZH;
        }

        public static void Set(string lang)
        {
            if (lang != ZH && lang != EN) return;
            if (lang == Lang) return;
            Lang = lang;
            Store.D.lang = lang; Store.Save();
            if (Changed != null) Changed();
        }

        static string Raw(string key)
        {
            string v;
            var cur = Lang == EN ? en : zh;
            if (cur.TryGetValue(key, out v)) return v;
            if (zh.TryGetValue(key, out v)) return v;
            return null;
        }

        /// <summary>T("res.time", "m", 1, "s", 5) → 參數成對傳入（名稱, 值）。找不到字串時回傳 key。</summary>
        public static string T(string key, params object[] kv)
        {
            string v = Raw(key);
            if (v == null) return key;
            if (kv == null || kv.Length == 0) return v;
            for (int i = 0; i + 1 < kv.Length; i += 2) v = v.Replace("{" + kv[i] + "}", Convert.ToString(kv[i + 1]));
            return v;
        }

        public static bool Has(string key) { return Raw(key) != null; }

        // ---------- 伺服器傳來的繁中系統句子：用 srv.* 句型轉成目前語言 ----------
        static List<KeyValuePair<Regex, string>> srv;

        static void BuildSrv()
        {
            srv = new List<KeyValuePair<Regex, string>>();
            foreach (var kv in zh)
            {
                if (!kv.Key.StartsWith("srv.")) continue;
                var names = new List<string>();
                var sb = new StringBuilder("^");
                string s = kv.Value; int i = 0;
                while (i < s.Length)
                {
                    int a = s.IndexOf('{', i);
                    if (a < 0) { sb.Append(Regex.Escape(s.Substring(i))); break; }
                    int b = s.IndexOf('}', a);
                    if (b < 0) { sb.Append(Regex.Escape(s.Substring(i))); break; }
                    sb.Append(Regex.Escape(s.Substring(i, a - i)));
                    names.Add(s.Substring(a + 1, b - a - 1));
                    sb.Append("(.+?)");
                    i = b + 1;
                }
                sb.Append("$");
                if (names.Count == 0 && sb.Length < 4) continue;
                srv.Add(new KeyValuePair<Regex, string>(new Regex(sb.ToString()), kv.Key + "|" + string.Join(",", names.ToArray())));
            }
        }

        /// <summary>把伺服器的繁中系統訊息換成目前語言；認不得的句子原樣回傳。</summary>
        public static string Message(string text)
        {
            if (string.IsNullOrEmpty(text) || Lang == ZH) return text;
            if (srv == null) BuildSrv();
            foreach (var p in srv)
            {
                var m = p.Key.Match(text);
                if (!m.Success) continue;
                var parts = p.Value.Split('|');
                string key = parts[0];
                var names = parts[1].Length > 0 ? parts[1].Split(',') : new string[0];
                var args = new List<object>();
                for (int i = 0; i < names.Length; i++) { args.Add(names[i]); args.Add(m.Groups[i + 1].Value); }
                return T(key, args.ToArray());
            }
            return text;
        }
    }
}
