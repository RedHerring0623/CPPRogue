using System.Collections.Generic;
using CPPRogue.Core.Enemies;
using CPPRogue.Core.Loot;

namespace CPPRogue.Game
{
    /// <summary>
    /// 图鉴数据缓存（Game 层）：EnemyKind → 档位/主材料/hp/atk。
    /// 掉落掷骰与怪物数值覆盖都从这里取；数据首次访问时从 Resources/EnemyTable.json
    /// 加载（与图鉴同源），JSON 缺失时回退内置表——改 JSON 的 hp/atk 即刻影响战斗数值。
    /// </summary>
    public static class CodexData
    {
        private static Dictionary<EnemyKind, EntryInfo> _map;

        public struct EntryInfo
        {
            public CodexTier Tier;
            public MaterialKind MainDrop;

            /// <summary>图鉴直值（2026-08-31 改版）：hp/atk 直接作为战斗数值。</summary>
            public float Hp;
            public float Atk;
        }

        public static bool TryGet(EnemyKind kind, out CodexTier tier, out MaterialKind mainDrop)
        {
            if (_map == null)
                Build();
            if (_map.TryGetValue(kind, out EntryInfo info))
            {
                tier = info.Tier;
                mainDrop = info.MainDrop;
                return true;
            }
            tier = CodexTier.Normal;
            mainDrop = MaterialKind.Driver;
            return false;
        }

        /// <summary>构建怪物数值覆盖表（注入 EnemySim：图鉴 hp/atk 直值优先于原型表内置）。</summary>
        public static Dictionary<EnemyKind, EnemyStatOverride> BuildStatOverrides()
        {
            if (_map == null)
                Build();
            var overrides = new Dictionary<EnemyKind, EnemyStatOverride>();
            foreach (KeyValuePair<EnemyKind, EntryInfo> pair in _map)
            {
                overrides[pair.Key] = new EnemyStatOverride { Hp = pair.Value.Hp, Atk = pair.Value.Atk };
            }
            return overrides;
        }

        private static void Build()
        {
            _map = new Dictionary<EnemyKind, EntryInfo>();
            foreach (CodexEntry entry in CodexPanel.LoadBook().Entries)
            {
                _map[EnemyCodex.KindForId(entry.Id)] = new EntryInfo
                {
                    Tier = entry.Tier,
                    MainDrop = entry.MainDrop,
                    Hp = entry.HpLv,
                    Atk = entry.AtkLv,
                };
            }
        }
    }
}
