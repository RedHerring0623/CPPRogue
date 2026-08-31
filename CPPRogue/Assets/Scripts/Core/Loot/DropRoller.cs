using System;
using System.Collections.Generic;

namespace CPPRogue.Core.Loot
{
    /// <summary>
    /// 掉落掷骰（LootDesign.md §4 定稿数值）：普通 35% 掉 1×主材料；
    /// 精英必掉 2（主材料×1 + 随机一种×1）；附属怪同普通规则。
    /// 只管"掉什么"，掉落物实体与拾取归表现层。
    /// </summary>
    public static class DropRoller
    {
        public static List<MaterialKind> Roll(CodexTier tier, MaterialKind mainDrop, Random rng)
        {
            var drops = new List<MaterialKind>();
            if (tier == CodexTier.Elite)
            {
                drops.Add(mainDrop);
                drops.Add(RandomMaterial(rng));
            }
            else if (rng.NextDouble() < 0.35)
            {
                drops.Add(mainDrop);
            }
            return drops;
        }

        public static MaterialKind RandomMaterial(Random rng)
        {
            switch (rng.Next(3))
            {
                case 0: return MaterialKind.TimeSlice;
                case 1: return MaterialKind.Ram;
                default: return MaterialKind.Driver;
            }
        }
    }
}
