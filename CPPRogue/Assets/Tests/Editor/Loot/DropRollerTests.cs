using System;
using System.Collections.Generic;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Loot
{
    /// <summary>掉落掷骰测试（LootDesign.md §4 定稿数值）。</summary>
    [TestFixture]
    public class DropRollerTests
    {
        [Test]
        public void 精英_必掉两个_主材料在前()
        {
            var rng = new Random(7);
            for (int i = 0; i < 100; i++)
            {
                List<MaterialKind> drops = DropRoller.Roll(CodexTier.Elite, MaterialKind.Ram, rng);
                Assert.AreEqual(2, drops.Count);
                Assert.AreEqual(MaterialKind.Ram, drops[0], "主材料必掉");
            }
        }

        [Test]
        public void 普通与附属_35概率掉1个主材料()
        {
            var rng = new Random(42);
            int dropped = 0;
            const int rolls = 20000;
            for (int i = 0; i < rolls; i++)
            {
                List<MaterialKind> drops = DropRoller.Roll(CodexTier.Normal, MaterialKind.TimeSlice, rng);
                Assert.LessOrEqual(drops.Count, 1);
                if (drops.Count == 1)
                {
                    dropped++;
                    Assert.AreEqual(MaterialKind.TimeSlice, drops[0], "只掉主材料");
                }

                // 附属怪与普通怪同规则（僵尸进程）
                List<MaterialKind> minionDrops = DropRoller.Roll(CodexTier.Minion, MaterialKind.TimeSlice, rng);
                Assert.LessOrEqual(minionDrops.Count, 1);
                if (minionDrops.Count == 1)
                    Assert.AreEqual(MaterialKind.TimeSlice, minionDrops[0]);
            }
            Assert.That((double)dropped / rolls, Is.EqualTo(0.35).Within(0.02), "统计上接近 35%");
        }

        [Test]
        public void 随机材料_三种都可能出现()
        {
            var rng = new Random(1);
            var seen = new HashSet<MaterialKind>();
            for (int i = 0; i < 200 && seen.Count < 3; i++)
                seen.Add(DropRoller.RandomMaterial(rng));
            Assert.AreEqual(3, seen.Count);
        }
    }
}
