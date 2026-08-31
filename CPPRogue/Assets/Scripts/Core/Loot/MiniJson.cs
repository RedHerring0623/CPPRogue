using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CPPRogue.Core.Loot
{
    /// <summary>
    /// 极简 JSON 解析器：只为本项目的数据表服务（对象 / 数组 / 字符串 / 数字 / 布尔 / null）。
    /// Core 层不能引第三方库（netstandard2.1 兼容），Unity 内置 JsonUtility 又不支持字典，
    /// 与其迁就序列化器，不如手写一个够用的读表器。解析结果映射：
    /// 对象 → <see cref="Dictionary{String,Object}"/>，数组 → <see cref="List{Object}"/>，
    /// 数字 → double，字符串 → string，布尔 → bool，null → null。
    /// 不支持：注释、尾逗号、单引号字符串——数据表保持严格 JSON。
    /// <see cref="Write(object)"/> 是它的逆运算（存档落盘用）。
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            int pos = 0;
            object value = ParseValue(text, ref pos);
            SkipWs(text, ref pos);
            if (pos != text.Length)
                throw Error(text, pos, "内容结束后还有剩余字符");
            return value;
        }

        /// <summary>把 Dictionary/List/string/double/bool/null 组装回 JSON 文本（与 Parse 成对的写入器）。</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(value, sb);
            return sb.ToString();
        }

        private static void WriteValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }
            if (value is string str)
            {
                WriteString(str, sb);
                return;
            }
            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }
            if (value is double d)
            {
                // 整数写成长整型（2 而不是 2.0），人读友好；Parse 侧统一还原 double
                if (!double.IsInfinity(d) && !double.IsNaN(d)
                    && d == Math.Floor(d) && Math.Abs(d) < 1e15)
                    sb.Append(((long)d).ToString(CultureInfo.InvariantCulture));
                else
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is List<object> list)
            {
                sb.Append('[');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    WriteValue(list[i], sb);
                }
                sb.Append(']');
                return;
            }
            if (value is Dictionary<string, object> map)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> pair in map)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    WriteString(pair.Key, sb);
                    sb.Append(':');
                    WriteValue(pair.Value, sb);
                }
                sb.Append('}');
                return;
            }
            throw new FormatException($"MiniJson.Write: 不支持的类型 {value.GetType()}");
        }

        private static void WriteString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length)
                throw Error(s, pos, "意外结束");
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject(s, ref pos);
                case '[': return ParseArray(s, ref pos);
                case '"': return ParseString(s, ref pos);
                case 't': return ParseLiteral(s, ref pos, "true", true);
                case 'f': return ParseLiteral(s, ref pos, "false", false);
                case 'n': return ParseLiteral(s, ref pos, "null", null);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                        return ParseNumber(s, ref pos);
                    throw Error(s, pos, $"意外字符 '{c}'");
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            var result = new Dictionary<string, object>();
            pos++;    // '{'
            SkipWs(s, ref pos);
            if (Take(s, ref pos, '}'))
                return result;
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != '"')
                    throw Error(s, pos, "对象的键必须是字符串");
                string key = ParseString(s, ref pos);
                SkipWs(s, ref pos);
                if (!Take(s, ref pos, ':'))
                    throw Error(s, pos, "键后缺少 ':'");
                result[key] = ParseValue(s, ref pos);
                SkipWs(s, ref pos);
                if (Take(s, ref pos, ','))
                {
                    SkipWs(s, ref pos);
                    if (pos < s.Length && s[pos] == '}')
                        throw Error(s, pos, "对象不允许尾逗号");
                    continue;
                }
                if (Take(s, ref pos, '}'))
                    return result;
                throw Error(s, pos, "对象中缺少 ',' 或 '}'");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            var result = new List<object>();
            pos++;    // '['
            SkipWs(s, ref pos);
            if (Take(s, ref pos, ']'))
                return result;
            while (true)
            {
                result.Add(ParseValue(s, ref pos));
                SkipWs(s, ref pos);
                if (Take(s, ref pos, ','))
                {
                    SkipWs(s, ref pos);
                    if (pos < s.Length && s[pos] == ']')
                        throw Error(s, pos, "数组不允许尾逗号");
                    continue;
                }
                if (Take(s, ref pos, ']'))
                    return result;
                throw Error(s, pos, "数组中缺少 ',' 或 ']'");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            pos++;    // 开头引号
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (pos >= s.Length)
                    break;
                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > s.Length)
                            throw Error(s, pos, "\\u 转义不完整");
                        int hex = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            int d = HexValue(s[pos + i]);
                            if (d < 0)
                                throw Error(s, pos + i, "\\u 转义含非十六进制字符");
                            hex = hex * 16 + d;
                        }
                        sb.Append((char)hex);
                        pos += 4;
                        break;
                    default:
                        throw Error(s, pos - 1, $"不支持的转义 '\\{e}'");
                }
            }
            throw Error(s, pos, "字符串没有结束引号");
        }

        private static double ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && s[pos] == '-')
                pos++;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
                pos++;
            if (pos < s.Length && s[pos] == '.')
            {
                pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
                    pos++;
            }
            if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
            {
                pos++;
                if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                    pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9')
                    pos++;
            }
            string num = s.Substring(start, pos - start);
            double value;
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw Error(s, start, $"非法数字 '{num}'");
            return value;
        }

        private static object ParseLiteral(string s, ref int pos, string word, object value)
        {
            if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0)
                throw Error(s, pos, $"非法字面量（期望 {word}）");
            pos += word.Length;
            return value;
        }

        private static bool Take(string s, ref int pos, char c)
        {
            if (pos < s.Length && s[pos] == c)
            {
                pos++;
                return true;
            }
            return false;
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    pos++;
                else
                    break;
            }
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        private static FormatException Error(string s, int pos, string message)
        {
            int at = Math.Max(0, Math.Min(pos, s.Length));
            int from = Math.Max(0, at - 12);
            string snippet = s.Substring(from, Math.Min(24, s.Length - from)).Replace("\n", "\\n");
            return new FormatException($"MiniJson: {message}（偏移 {at}，附近 \"…{snippet}…\"）");
        }
    }
}
