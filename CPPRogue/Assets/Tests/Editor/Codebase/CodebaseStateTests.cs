using System;
using CPPRogue.Core.Codebase;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Codebase
{
    /// <summary>局外代码库单元测试：阶梯目录解析、仓库记账、合成规则（LootDesign.md §3）。</summary>
    [TestFixture]
    public class CodebaseStateTests
    {
        // —— FragmentCatalog ——

        [Test]
        public void 目录_阶梯ID生成与解析往返()
        {
            Assert.AreEqual("attack(1)", FragmentCatalog.TierId("attack", 1));
            Assert.AreEqual("attack()", FragmentCatalog.FreeId("attack"));

            FragmentDef tierDef = FragmentCatalog.Parse("heal(3)");
            Assert.AreEqual("heal", tierDef.Family);
            Assert.AreEqual(3, tierDef.Tier);
            Assert.AreEqual("heal(3)", tierDef.Id);

            FragmentDef freeDef = FragmentCatalog.Parse("attack()");
            Assert.AreEqual("attack", freeDef.Family);
            Assert.AreEqual(0, freeDef.Tier);
            Assert.AreEqual("attack()", freeDef.Id);
        }

        [Test]
        public void 目录_阶梯外ID返回null()
        {
            Assert.IsNull(FragmentCatalog.Parse(null));
            Assert.IsNull(FragmentCatalog.Parse(""));
            Assert.IsNull(FragmentCatalog.Parse("attack"), "无括号");
            Assert.IsNull(FragmentCatalog.Parse("goto()"), "非阶梯家族");
            Assert.IsNull(FragmentCatalog.Parse("attack(0)"), "档位下界");
            Assert.IsNull(FragmentCatalog.Parse("attack(6)"), "超过顶档");
            Assert.IsNull(FragmentCatalog.Parse("attack(x)"), "非数字");
        }

        // —— 仓库与材料记账 ——

        [Test]
        public void 仓库_计数与未知ID()
        {
            var state = new CodebaseState();
            Assert.AreEqual(0, state.Count("attack(1)"));
            state.Add("attack(1)", 3);
            state.Add("attack(1)");
            Assert.AreEqual(4, state.Count("attack(1)"));
            Assert.Throws<ArgumentException>(() => state.Add("goto()"));
        }

        [Test]
        public void 材料_记账()
        {
            var state = new CodebaseState();
            Assert.AreEqual(0, state.CountMaterial(MaterialKind.Ram));
            state.AddMaterial(MaterialKind.Ram, 4);
            state.AddMaterial(MaterialKind.Ram);
            Assert.AreEqual(5, state.CountMaterial(MaterialKind.Ram));
        }

        // —— 合成 ——

        [Test]
        public void 合成_两块同级升一档()
        {
            var state = new CodebaseState();
            state.Add("attack(1)", 4);

            Assert.IsTrue(state.CanMerge("attack", 1));
            Assert.IsTrue(state.Merge("attack", 1));
            Assert.AreEqual(2, state.Count("attack(1)"));
            Assert.AreEqual(1, state.Count("attack(2)"));

            state.Merge("attack", 1);
            Assert.AreEqual(0, state.Count("attack(1)"));
            Assert.AreEqual(2, state.Count("attack(2)"));

            Assert.IsFalse(state.Merge("attack", 1), "数量不足");
            Assert.AreEqual(0, state.Count("attack(1)"), "失败时状态不变");
            Assert.AreEqual(2, state.Count("attack(2)"));
        }

        [Test]
        public void 合成_顶档合成自由形参()
        {
            var state = new CodebaseState();
            state.Add("attack(5)", 2);

            Assert.AreEqual("attack()", CodebaseState.MergeTargetId("attack", 5));
            Assert.AreEqual("attack(3)", CodebaseState.MergeTargetId("attack", 2));

            Assert.IsTrue(state.Merge("attack", 5));
            Assert.AreEqual(0, state.Count("attack(5)"));
            Assert.AreEqual(1, state.Count("attack()"));
            Assert.IsFalse(state.CanMerge("attack", 0), "自由形参不可再合成");
        }

        [Test]
        public void 合成_非法家族或档位()
        {
            var state = new CodebaseState();
            state.Add("attack(1)", 4);
            Assert.IsFalse(state.CanMerge("for", 1));
            Assert.IsFalse(state.CanMerge("attack", 0));
            Assert.IsFalse(state.CanMerge("attack", 6));
            Assert.IsFalse(state.Merge("for", 1));
        }

        // —— 材料花费 ——

        [Test]
        public void 材料_Spend不足拒绝且不部分扣除()
        {
            var state = new CodebaseState();
            state.AddMaterial(MaterialKind.Ram, 3);
            Assert.IsFalse(state.Spend(MaterialKind.Ram, 4));
            Assert.AreEqual(3, state.CountMaterial(MaterialKind.Ram), "失败不扣");
            Assert.IsTrue(state.Spend(MaterialKind.Ram, 3));
            Assert.AreEqual(0, state.CountMaterial(MaterialKind.Ram));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.Spend(MaterialKind.Ram, -1));
        }
    }
}
