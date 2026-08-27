using System;
using System.Collections.Generic;

namespace CPPRogue.Core.Loot
{
    /// <summary>EnemyTable.json 解析结果。version/materials/tiers 里纯说明性的字段不进模型。</summary>
    public sealed class CodexBook
    {
        public int Version;
        public List<CodexEntry> Entries = new List<CodexEntry>();

        /// <summary>材料 ID → 中文名（JSON 里的 materials 段，仅用于展示对照）。</summary>
        public Dictionary<string, string> MaterialNames = new Dictionary<string, string>();
    }

    /// <summary>
    /// EnemyTable.json → CodexBook。格式错误抛 FormatException（带条目 ID 定位），
    /// 图鉴 UI 捕获后回退内置表（EnemyCodex.Default）。
    /// </summary>
    public static class EnemyCodexJson
    {
        public static CodexBook Parse(string json)
        {
            object root = MiniJson.Parse(json);
            var map = root as Dictionary<string, object>
                ?? throw new FormatException("EnemyTable.json: 根节点必须是对象");
            var book = new CodexBook { Version = (int)RequireNumber(map, "version", "$") };

            if (map.TryGetValue("materials", out object mats) && mats is Dictionary<string, object> matMap)
            {
                foreach (KeyValuePair<string, object> pair in matMap)
                    book.MaterialNames[pair.Key] = (string)pair.Value;
            }

            if (!(map.TryGetValue("enemies", out object arr) && arr is List<object> list))
                throw new FormatException("EnemyTable.json: 缺少 enemies 数组");
            foreach (object item in list)
            {
                var e = item as Dictionary<string, object>
                    ?? throw new FormatException("EnemyTable.json: enemies 的元素必须是对象");
                book.Entries.Add(ParseEntry(e));
            }
            return book;
        }

        private static CodexEntry ParseEntry(Dictionary<string, object> map)
        {
            string id = RequireString(map, "id");
            var stats = map.TryGetValue("stats", out object s) && s is Dictionary<string, object>
                ? (Dictionary<string, object>)s
                : throw new FormatException($"EnemyTable.json[{id}]: 缺少 stats 对象");

            var entry = new CodexEntry
            {
                Id = id,
                NameZh = RequireString(map, "nameZh"),
                NameEn = RequireString(map, "nameEn"),
                Tier = ParseTier(RequireString(map, "tier")),
                HpLv = (int)RequireNumber(stats, "hp", id),
                AtkLv = (int)RequireNumber(stats, "atk", id),
                SpdLv = (int)RequireNumber(stats, "spd", id),
                SpawnScore = (int)RequireNumber(map, "spawnScore", id),
                MainDrop = ParseDrop(RequireString(map, "mainDrop")),
                Desc = RequireString(map, "desc"),
                Note = map.TryGetValue("note", out object note) && note is string str ? str : null,
                Summons = ParseSummons(map),
            };

            if (entry.HpLv < 1 || entry.HpLv > 5 || entry.AtkLv < 1 || entry.AtkLv > 5
                || entry.SpdLv < 1 || entry.SpdLv > 5)
                throw new FormatException($"EnemyTable.json[{id}]: 属性等级必须是 1-5");
            return entry;
        }

        private static CodexTier ParseTier(string tier)
        {
            switch (tier)
            {
                case "normal": return CodexTier.Normal;
                case "elite": return CodexTier.Elite;
                case "minion": return CodexTier.Minion;
                default: throw new FormatException($"EnemyTable.json: 未知档位 '{tier}'（normal/elite/minion）");
            }
        }

        private static MaterialKind ParseDrop(string drop)
        {
            if (Materials.TryParseId(drop, out MaterialKind kind))
                return kind;
            throw new FormatException($"EnemyTable.json: 未知主材料 '{drop}'（timeSlice/ram/driver）");
        }

        private static string[] ParseSummons(Dictionary<string, object> map)
        {
            if (!map.TryGetValue("summons", out object arr) || !(arr is List<object> list))
                return null;
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i] as string
                    ?? throw new FormatException("EnemyTable.json: summons 数组的元素必须是字符串");
            }
            return result;
        }

        private static string RequireString(Dictionary<string, object> map, string key)
        {
            if (map.TryGetValue(key, out object value) && value is string s)
                return s;
            throw new FormatException($"EnemyTable.json: 缺少字符串字段 '{key}'");
        }

        private static double RequireNumber(Dictionary<string, object> map, string key, string where)
        {
            if (map.TryGetValue(key, out object value) && value is double d)
                return d;
            throw new FormatException($"EnemyTable.json[{where}]: 缺少数字字段 '{key}'");
        }
    }
}
