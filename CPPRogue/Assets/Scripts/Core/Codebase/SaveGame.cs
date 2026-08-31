using System;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Loot;

namespace CPPRogue.Core.Codebase
{
    /// <summary>
    /// 存档序列化：PlayerProfile ↔ JSON 文本（MiniJson 往返，格式见 doc/CODEBASE.md）。
    /// 版本策略：v1 读档时缺字段按零值补——只向后兼容，不做迁移。
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 1;

        public static string ToJson(PlayerProfile profile)
        {
            var blocks = new Dictionary<string, object>();
            foreach (KeyValuePair<string, int> pair in profile.Codebase.Blocks)
            {
                if (pair.Value > 0)
                    blocks[pair.Key] = (double)pair.Value;
            }

            var materials = new Dictionary<string, object>
            {
                [Materials.Id(MaterialKind.TimeSlice)] = (double)profile.Codebase.CountMaterial(MaterialKind.TimeSlice),
                [Materials.Id(MaterialKind.Ram)] = (double)profile.Codebase.CountMaterial(MaterialKind.Ram),
                [Materials.Id(MaterialKind.Driver)] = (double)profile.Codebase.CountMaterial(MaterialKind.Driver),
            };

            var root = new Dictionary<string, object>
            {
                ["version"] = (double)Version,
                ["blocks"] = blocks,
                ["materials"] = materials,
                ["lineBonus"] = (double)profile.Progress.LineBonus,
                ["windowSteps"] = (double)profile.Progress.WindowSteps,
                ["loadout"] = BlockJson.ToJson(profile.Loadout.ToArray()),
            };
            return MiniJson.Write(root);
        }

        public static PlayerProfile Parse(string json)
        {
            object root = MiniJson.Parse(json);
            var map = root as Dictionary<string, object>
                ?? throw new FormatException("存档：根节点必须是对象");
            if (GetInt(map, "version") > Version)
                throw new FormatException("存档：版本比当前游戏新，无法读取");

            var profile = new PlayerProfile();

            if (map.TryGetValue("blocks", out object blocksJson) && blocksJson is Dictionary<string, object> blocks)
            {
                foreach (KeyValuePair<string, object> pair in blocks)
                {
                    try
                    {
                        profile.Codebase.Add(pair.Key, (int)AsDouble(pair.Value));
                    }
                    catch (ArgumentException)
                    {
                        // 未来版本写入的未知块：跳过而不是整个存档报废
                    }
                }
            }

            if (map.TryGetValue("materials", out object matsJson) && matsJson is Dictionary<string, object> mats)
            {
                foreach (KeyValuePair<string, object> pair in mats)
                {
                    if (Materials.TryParseId(pair.Key, out MaterialKind kind))
                        profile.Codebase.AddMaterial(kind, (int)AsDouble(pair.Value));
                }
            }

            profile.Progress.LineBonus = GetInt(map, "lineBonus");
            profile.Progress.WindowSteps = GetInt(map, "windowSteps");

            if (map.TryGetValue("loadout", out object loadoutJson) && loadoutJson != null)
                profile.Loadout = new List<Block>(BlockJson.FromJson(loadoutJson));

            return profile;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out object value) && value is double d ? (int)d : 0;
        }

        private static double AsDouble(object value)
        {
            return value is double d ? d : 0d;
        }
    }
}
