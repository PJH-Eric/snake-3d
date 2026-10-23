using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Snake3D
{
    /// <summary>
    /// 極簡 JSON：解析成 null / bool / double / string / List&lt;object&gt; / Dictionary&lt;string,object&gt;。
    /// 線上對戰的快照是巢狀陣列，Unity 內建的 JsonUtility 無法處理，所以自己寫（零依賴、WebGL 可用）。
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            var v = ReadValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("JSON 結尾多餘字元");
            return v;
        }

        public static bool TryParse(string text, out object value)
        {
            try { value = Parse(text); return true; }
            catch (Exception) { value = null; return false; }
        }

        static void SkipWs(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 提早結束");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Match(s, i, "true")) { i += 4; return true; }
            if (Match(s, i, "false")) { i += 5; return false; }
            if (Match(s, i, "null")) { i += 4; return null; }
            return ReadNumber(s, ref i);
        }

        static bool Match(string s, int i, string w)
        {
            return string.CompareOrdinal(s, i, w, 0, w.Length) == 0;
        }

        static object ReadObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON 物件鍵必須是字串");
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON 缺少冒號");
                i++;
                d[key] = ReadValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 物件沒有結尾");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("JSON 物件格式錯誤");
            }
        }

        static object ReadArray(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 陣列沒有結尾");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("JSON 陣列格式錯誤");
            }
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (true)
            {
                if (i >= s.Length) throw new FormatException("JSON 字串沒有結尾");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw new FormatException("JSON 跳脫字元不完整");
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("JSON \\u 不完整");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4; break;
                    default: sb.Append(e); break;   // " \ /
                }
            }
        }

        static object ReadNumber(string s, ref int i)
        {
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            if (st == i) throw new FormatException("JSON 無法辨識的內容");
            return double.Parse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ---------- 序列化 ----------
        public static string Serialize(object v)
        {
            var sb = new StringBuilder();
            Write(sb, v);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is int || v is long || v is short || v is byte || v is uint) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            if (v is double || v is float)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
            }
            var dict = v as IDictionary<string, object>;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key); sb.Append(':'); Write(sb, kv.Value);
                }
                sb.Append('}'); return;
            }
            var list = v as System.Collections.IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list) { if (!first) sb.Append(','); first = false; Write(sb, item); }
                sb.Append(']'); return;
            }
            WriteString(sb, v.ToString());
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- 取值小幫手 ----------
        public static object Get(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }
        public static string Str(object o, string key, string def = "")
        {
            var v = Get(o, key); return v is string ? (string)v : def;
        }
        public static double Num(object o, string key, double def = 0)
        {
            var v = Get(o, key); return v is double ? (double)v : def;
        }
        public static int Int(object o, string key, int def = 0)
        {
            var v = Get(o, key); return v is double ? (int)(double)v : def;
        }
        public static bool Bool(object o, string key, bool def = false)
        {
            var v = Get(o, key); return v is bool ? (bool)v : def;
        }
        public static List<object> Arr(object o, string key)
        {
            return Get(o, key) as List<object>;
        }
        public static double N(object v) { return v is double ? (double)v : 0; }
    }
}
