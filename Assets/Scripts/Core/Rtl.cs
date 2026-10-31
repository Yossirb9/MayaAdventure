using System;
using System.Collections.Generic;
using System.Text;

namespace MayaAdventure
{
    /// <summary>
    /// TextMeshPro has no bidi support, so Hebrew strings are converted from logical to visual order:
    /// runs are reversed, Hebrew runs are mirrored, Latin/number runs are kept. (Same in the editor and on phones.)
    /// </summary>
    public static class Rtl
    {
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static string Visual(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (cache.TryGetValue(s, out var hit)) return hit;
            if (cache.Count > 512) cache.Clear();

            bool hasRtl = false;
            foreach (char ch in s)
                if (IsRtl(ch)) { hasRtl = true; break; }
            if (!hasRtl) return cache[s] = s;

            int n = s.Length;
            var strong = new int[n]; // 1 = RTL, 0 = LTR, -1 = neutral
            for (int i = 0; i < n; i++) strong[i] = IsRtl(s[i]) ? 1 : IsLtr(s[i]) ? 0 : -1;

            var cls = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (strong[i] != -1) { cls[i] = strong[i]; continue; }
                int prev = 1, next = 1;
                for (int j = i - 1; j >= 0; j--) if (strong[j] != -1) { prev = strong[j]; break; }
                for (int j = i + 1; j < n; j++) if (strong[j] != -1) { next = strong[j]; break; }
                cls[i] = prev == 0 && next == 0 ? 0 : 1;
            }

            var runs = new List<string>();
            var sb = new StringBuilder();
            int cur = cls[0];
            for (int i = 0; i < n; i++)
            {
                if (cls[i] != cur)
                {
                    runs.Add(Finish(sb, cur));
                    sb.Clear();
                    cur = cls[i];
                }
                sb.Append(s[i]);
            }
            runs.Add(Finish(sb, cur));
            runs.Reverse();
            return cache[s] = string.Concat(runs);
        }

        static string Finish(StringBuilder sb, int cls)
        {
            string str = sb.ToString();
            if (cls == 0) return str;
            var arr = str.ToCharArray();
            Array.Reverse(arr);
            for (int i = 0; i < arr.Length; i++)
            {
                switch (arr[i])
                {
                    case '(': arr[i] = ')'; break;
                    case ')': arr[i] = '('; break;
                    case '[': arr[i] = ']'; break;
                    case ']': arr[i] = '['; break;
                    case '<': arr[i] = '>'; break;
                    case '>': arr[i] = '<'; break;
                }
            }
            return new string(arr);
        }

        static bool IsRtl(char c) => c >= 0x0590 && c <= 0x05FF;
        static bool IsLtr(char c) => char.IsLetterOrDigit(c) && !IsRtl(c);
    }
}
