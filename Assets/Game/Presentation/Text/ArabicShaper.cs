using System.Collections.Generic;
using System.Text;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Arabic shaping + right-to-left reordering for renderers that draw glyphs left to right
    /// (uGUI Text). Two steps:
    ///  1. Shape: pick the isolated / final / initial / medial presentation form of every letter
    ///     (Unicode Arabic Presentation Forms-B) and build lam-alef ligatures.
    ///  2. Reorder: produce visual order for a right-to-left paragraph while keeping Latin words,
    ///     digits, IP addresses and ports left-to-right.
    /// Lines are processed independently; long texts must use explicit '\n' line breaks.
    /// </summary>
    public static class ArabicShaper
    {
        enum Join { None, Right, Dual, Transparent }

        // code -> isolated, final, initial, medial (0 = form does not exist)
        static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            { 'ء', new[] { 'ﺀ', '\0', '\0', '\0' } },
            { 'آ', new[] { 'ﺁ', 'ﺂ', '\0', '\0' } },
            { 'أ', new[] { 'ﺃ', 'ﺄ', '\0', '\0' } },
            { 'ؤ', new[] { 'ﺅ', 'ﺆ', '\0', '\0' } },
            { 'إ', new[] { 'ﺇ', 'ﺈ', '\0', '\0' } },
            { 'ئ', new[] { 'ﺉ', 'ﺊ', 'ﺋ', 'ﺌ' } },
            { 'ا', new[] { 'ﺍ', 'ﺎ', '\0', '\0' } },
            { 'ب', new[] { 'ﺏ', 'ﺐ', 'ﺑ', 'ﺒ' } },
            { 'ة', new[] { 'ﺓ', 'ﺔ', '\0', '\0' } },
            { 'ت', new[] { 'ﺕ', 'ﺖ', 'ﺗ', 'ﺘ' } },
            { 'ث', new[] { 'ﺙ', 'ﺚ', 'ﺛ', 'ﺜ' } },
            { 'ج', new[] { 'ﺝ', 'ﺞ', 'ﺟ', 'ﺠ' } },
            { 'ح', new[] { 'ﺡ', 'ﺢ', 'ﺣ', 'ﺤ' } },
            { 'خ', new[] { 'ﺥ', 'ﺦ', 'ﺧ', 'ﺨ' } },
            { 'د', new[] { 'ﺩ', 'ﺪ', '\0', '\0' } },
            { 'ذ', new[] { 'ﺫ', 'ﺬ', '\0', '\0' } },
            { 'ر', new[] { 'ﺭ', 'ﺮ', '\0', '\0' } },
            { 'ز', new[] { 'ﺯ', 'ﺰ', '\0', '\0' } },
            { 'س', new[] { 'ﺱ', 'ﺲ', 'ﺳ', 'ﺴ' } },
            { 'ش', new[] { 'ﺵ', 'ﺶ', 'ﺷ', 'ﺸ' } },
            { 'ص', new[] { 'ﺹ', 'ﺺ', 'ﺻ', 'ﺼ' } },
            { 'ض', new[] { 'ﺽ', 'ﺾ', 'ﺿ', 'ﻀ' } },
            { 'ط', new[] { 'ﻁ', 'ﻂ', 'ﻃ', 'ﻄ' } },
            { 'ظ', new[] { 'ﻅ', 'ﻆ', 'ﻇ', 'ﻈ' } },
            { 'ع', new[] { 'ﻉ', 'ﻊ', 'ﻋ', 'ﻌ' } },
            { 'غ', new[] { 'ﻍ', 'ﻎ', 'ﻏ', 'ﻐ' } },
            { 'ـ', new[] { 'ـ', 'ـ', 'ـ', 'ـ' } },
            { 'ف', new[] { 'ﻑ', 'ﻒ', 'ﻓ', 'ﻔ' } },
            { 'ق', new[] { 'ﻕ', 'ﻖ', 'ﻗ', 'ﻘ' } },
            { 'ك', new[] { 'ﻙ', 'ﻚ', 'ﻛ', 'ﻜ' } },
            { 'ل', new[] { 'ﻝ', 'ﻞ', 'ﻟ', 'ﻠ' } },
            { 'م', new[] { 'ﻡ', 'ﻢ', 'ﻣ', 'ﻤ' } },
            { 'ن', new[] { 'ﻥ', 'ﻦ', 'ﻧ', 'ﻨ' } },
            { 'ه', new[] { 'ﻩ', 'ﻪ', 'ﻫ', 'ﻬ' } },
            { 'و', new[] { 'ﻭ', 'ﻮ', '\0', '\0' } },
            { 'ى', new[] { 'ﻯ', 'ﻰ', '\0', '\0' } },
            { 'ي', new[] { 'ﻱ', 'ﻲ', 'ﻳ', 'ﻴ' } },
        };

        // alef variant -> (isolated, final) lam-alef ligature
        static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            { 'آ', new[] { 'ﻵ', 'ﻶ' } },
            { 'أ', new[] { 'ﻷ', 'ﻸ' } },
            { 'إ', new[] { 'ﻹ', 'ﻺ' } },
            { 'ا', new[] { 'ﻻ', 'ﻼ' } },
        };

        static readonly Dictionary<char, char> Mirror = new Dictionary<char, char>
        {
            { '(', ')' }, { ')', '(' }, { '[', ']' }, { ']', '[' }, { '{', '}' }, { '}', '{' }, { '<', '>' }, { '>', '<' }, { '«', '»' }, { '»', '«' },
        };

        static Join JoinOf(char c)
        {
            if (IsTransparent(c)) return Join.Transparent;
            if (!Forms.TryGetValue(c, out var f)) return Join.None;
            if (c == 'ء') return Join.None;
            return f[2] == '\0' ? Join.Right : Join.Dual;
        }

        static bool IsTransparent(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';

        public static bool IsArabic(char c) =>
            (c >= '؀' && c <= 'ۿ') || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');

        public static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (IsArabic(c)) return true;
            return false;
        }

        /// <summary>Logical Arabic string -> visual string ready for a left-to-right renderer.</summary>
        public static string Process(string logical)
        {
            if (string.IsNullOrEmpty(logical) || !ContainsArabic(logical)) return logical;
            // uGUI Text cannot place harakat on their letter, so they are dropped for display.
            logical = StripHarakat(logical);
            var lines = logical.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Reorder(Shape(lines[i]));
            return string.Join("\n", lines);
        }

        public static string StripHarakat(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (!IsTransparent(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Contextual shaping in logical order.</summary>
        public static string Shape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                Join j = JoinOf(c);
                if (j == Join.None || j == Join.Transparent)
                {
                    // Non-joining letters (hamza) still use their presentation form.
                    sb.Append(j == Join.None && Forms.TryGetValue(c, out var iso) ? iso[0] : c);
                    continue;
                }

                int prev = PrevLetter(s, i);
                int next = NextLetter(s, i);
                bool joinsPrev = prev >= 0 && JoinOf(s[prev]) == Join.Dual;

                // Lam + alef ligature.
                if (c == 'ل' && next >= 0 && LamAlef.TryGetValue(s[next], out var lig))
                {
                    sb.Append(joinsPrev ? lig[1] : lig[0]);
                    // keep transparent marks between lam and alef
                    for (int k = i + 1; k < next; k++) sb.Append(s[k]);
                    i = next;
                    continue;
                }

                Join nj = next >= 0 ? JoinOf(s[next]) : Join.None;
                bool joinsNext = j == Join.Dual && (nj == Join.Dual || nj == Join.Right);
                var f = Forms[c];
                char shaped;
                if (joinsPrev && joinsNext) shaped = f[3];
                else if (joinsPrev) shaped = f[1];
                else if (joinsNext) shaped = f[2];
                else shaped = f[0];
                sb.Append(shaped == '\0' ? f[0] : shaped);
            }
            return sb.ToString();
        }

        static int PrevLetter(string s, int i)
        {
            for (int k = i - 1; k >= 0; k--)
            {
                var j = JoinOf(s[k]);
                if (j == Join.Transparent) continue;
                return j == Join.None ? -1 : k;
            }
            return -1;
        }

        static int NextLetter(string s, int i)
        {
            for (int k = i + 1; k < s.Length; k++)
            {
                var j = JoinOf(s[k]);
                if (j == Join.Transparent) continue;
                return j == Join.None ? -1 : k;
            }
            return -1;
        }

        enum Dir { L, R, N }

        static Dir DirOf(char c)
        {
            if (IsArabic(c)) return Dir.R;
            if (char.IsLetterOrDigit(c)) return Dir.L;
            return Dir.N;
        }

        /// <summary>Visual reordering of one line with a right-to-left base direction.</summary>
        public static string Reorder(string s)
        {
            int n = s.Length;
            var dirs = new Dir[n];
            for (int i = 0; i < n; i++) dirs[i] = DirOf(s[i]);

            // Neutrals take the direction of their strong neighbours when both agree, else RTL (base).
            for (int i = 0; i < n; i++)
            {
                if (dirs[i] != Dir.N) continue;
                int end = i;
                while (end < n && DirOf(s[end]) == Dir.N) end++;
                Dir left = i > 0 ? dirs[i - 1] : Dir.R;
                Dir right = end < n ? dirs[end] : Dir.R;
                Dir resolved = left == Dir.L && right == Dir.L ? Dir.L : Dir.R;
                for (int k = i; k < end; k++) dirs[k] = resolved;
                i = end - 1;
            }

            var chars = new char[n];
            var cdirs = new Dir[n];
            for (int i = 0; i < n; i++)
            {
                chars[n - 1 - i] = dirs[i] == Dir.R && Mirror.TryGetValue(s[i], out var m) ? m : s[i];
                cdirs[n - 1 - i] = dirs[i];
            }
            // Restore left-to-right runs.
            for (int i = 0; i < n; i++)
            {
                if (cdirs[i] != Dir.L) continue;
                int end = i;
                while (end < n && cdirs[end] == Dir.L) end++;
                System.Array.Reverse(chars, i, end - i);
                i = end - 1;
            }
            // Reversal put harakat (e.g. the damma in حُماة) before their letter; move each letter back
            // in front of its marks so the renderer draws the mark on the right letter.
            for (int i = 0; i < n; i++)
            {
                if (cdirs[i] != Dir.R || !IsTransparent(chars[i])) continue;
                int j = i;
                while (j < n && IsTransparent(chars[j])) j++;
                if (j < n)
                {
                    char letter = chars[j];
                    System.Array.Copy(chars, i, chars, i + 1, j - i);
                    chars[i] = letter;
                }
                i = j;
            }
            return new string(chars);
        }
    }
}
