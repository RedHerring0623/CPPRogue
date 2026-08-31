using System;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Codebase;
using CPPRogue.Core.Loot;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Codebase
{
    /// <summary>局外档案全家桶：成长兑换（等比）、新档锚点、存档往返、战备 BD 校验。</summary>
    [TestFixture]
    public class ProfileTests
    {
        // —— MetaProgress：等比兑换 ——

        [Test]
        public void 成长_基础值与等比花费()
        {
            var progress = new MetaProgress();
            Assert.AreEqual(2, progress.MaxLines);
            Assert.AreEqual(1f, progress.WindowSeconds, 1e-6);
            Assert.AreEqual(1, progress.NextLineCost);
            Assert.AreEqual(1, progress.NextWindowCost);
        }

        [Test]
        public void 成长_兑换扣材料且花费翻倍()
        {
            var profile = new PlayerProfile();
            profile.Codebase.AddMaterial(MaterialKind.Ram, 7);   // 1 + 2 + 4 = 7 → 恰好三次
            profile.Codebase.AddMaterial(MaterialKind.TimeSlice, 3);   // 1 + 2 = 3 → 两次

            Assert.IsTrue(profile.Progress.TryUpgradeLines(profile.Codebase));
            Assert.AreEqual(3, profile.Progress.MaxLines);
            Assert.AreEqual(6, profile.Codebase.CountMaterial(MaterialKind.Ram));
            Assert.AreEqual(2, profile.Progress.NextLineCost);

            Assert.IsTrue(profile.Progress.TryUpgradeLines(profile.Codebase));
            Assert.IsTrue(profile.Progress.TryUpgradeLines(profile.Codebase));
            Assert.AreEqual(5, profile.Progress.MaxLines);
            Assert.AreEqual(0, profile.Codebase.CountMaterial(MaterialKind.Ram));
            Assert.AreEqual(8, profile.Progress.NextLineCost);
            Assert.IsFalse(profile.Progress.TryUpgradeLines(profile.Codebase), "材料不足");

            Assert.IsTrue(profile.Progress.TryUpgradeWindow(profile.Codebase));
            Assert.AreEqual(1.5f, profile.Progress.WindowSeconds, 1e-6);
            Assert.IsTrue(profile.Progress.TryUpgradeWindow(profile.Codebase));
            Assert.AreEqual(2f, profile.Progress.WindowSeconds, 1e-6);
            Assert.IsFalse(profile.Progress.TryUpgradeWindow(profile.Codebase));
        }

        // —— PlayerProfile：新档锚点 ——

        [Test]
        public void 新档_一个attack1_两行_一秒窗_零材料()
        {
            PlayerProfile profile = PlayerProfile.NewGame();
            Assert.AreEqual(1, profile.Codebase.Count("attack(1)"));
            Assert.AreEqual(0, profile.Codebase.CountMaterial(MaterialKind.TimeSlice));
            Assert.AreEqual(0, profile.Codebase.CountMaterial(MaterialKind.Ram));
            Assert.AreEqual(0, profile.Codebase.CountMaterial(MaterialKind.Driver));
            Assert.AreEqual(1, profile.Loadout.Count);
            Assert.AreEqual("attack(1)", LoadoutValidator.FragmentIdOf(profile.Loadout[0]));
            Assert.AreEqual(2, profile.Progress.MaxLines);
        }

        // —— SaveGame：往返 ——

        [Test]
        public void 存档_往返无损()
        {
            PlayerProfile profile = PlayerProfile.NewGame();
            profile.Codebase.Add("attack(2)", 3);
            profile.Codebase.Add("attack()", 1);
            profile.Codebase.AddMaterial(MaterialKind.TimeSlice, 12);
            profile.Codebase.AddMaterial(MaterialKind.Ram, 5);
            profile.Codebase.AddMaterial(MaterialKind.Driver, 2);
            profile.Progress.TryUpgradeLines(profile.Codebase);
            profile.Loadout.Add(Block.Call("attack"));   // attack() 自由形参

            string json = SaveGame.ToJson(profile);
            PlayerProfile parsed = SaveGame.Parse(json);

            Assert.AreEqual(3, parsed.Codebase.Count("attack(2)"));
            Assert.AreEqual(1, parsed.Codebase.Count("attack(1)"));
            Assert.AreEqual(1, parsed.Codebase.Count("attack()"));
            Assert.AreEqual(12, parsed.Codebase.CountMaterial(MaterialKind.TimeSlice));
            Assert.AreEqual(4, parsed.Codebase.CountMaterial(MaterialKind.Ram), "兑换扣掉的 1 不回来");
            Assert.AreEqual(2, parsed.Codebase.CountMaterial(MaterialKind.Driver));
            Assert.AreEqual(3, parsed.Progress.MaxLines);
            Assert.AreEqual(2, parsed.Loadout.Count);
            Assert.AreEqual("attack()", LoadoutValidator.FragmentIdOf(parsed.Loadout[1]));
        }

        [Test]
        public void 存档_缺字段按零值补()
        {
            PlayerProfile parsed = SaveGame.Parse("{\"version\":1}");
            Assert.AreEqual(0, parsed.Codebase.Blocks.Count);
            Assert.AreEqual(2, parsed.Progress.MaxLines);
            Assert.AreEqual(0, parsed.Loadout.Count);
        }

        [Test]
        public void 存档_更高版本拒绝读取()
        {
            Assert.Throws<FormatException>(() => SaveGame.Parse("{\"version\":99}"));
        }

        // —— LoadoutValidator ——

        [Test]
        public void 校验_行数超限与用量超持有()
        {
            PlayerProfile profile = PlayerProfile.NewGame();   // attack(1)×1，2 行
            string error;

            var twoAttacks = new[]
            {
                Block.Call("attack", Expr.Num(1)),
                Block.Call("attack", Expr.Num(1)),
            };
            Assert.IsFalse(LoadoutValidator.Validate(twoAttacks, profile.Codebase, profile.Progress.MaxLines, out error),
                "用了 2 个 attack(1)，仓库只有 1 个");
            Assert.That(error, Does.Contain("仓库"));

            profile.Codebase.Add("attack(1)", 1);
            Assert.IsTrue(LoadoutValidator.Validate(twoAttacks, profile.Codebase, profile.Progress.MaxLines, out error));

            var threeLines = new[]
            {
                Block.Call("attack", Expr.Num(1)),
                Block.Call("attack", Expr.Num(1)),
                Block.Call("heal", Expr.Num(1)),
            };
            Assert.IsFalse(LoadoutValidator.Validate(threeLines, profile.Codebase, 2, out error), "3 行超 2 行上限");
            Assert.That(error, Does.Contain("行数"));
        }

        [Test]
        public void 校验_自由形参与变量绑定()
        {
            var profile = new PlayerProfile();
            profile.Codebase.Add("attack()", 1);

            var bound = new[] { Block.Call("attack", Expr.Var("hp")) };
            string error;
            Assert.IsTrue(LoadoutValidator.Validate(bound, profile.Codebase, 2, out error),
                "attack(变量) 按 attack() 计，持有 1 个");
            Assert.AreEqual("attack()", LoadoutValidator.FragmentIdOf(bound[0]));
        }

        [Test]
        public void 校验_ToBlock构造()
        {
            var b1 = LoadoutValidator.ToBlock("attack(2)");
            Assert.AreEqual("attack", b1.CallName);
            Assert.AreEqual(1, b1.Args.Length);
            Assert.AreEqual(2d, b1.Args[0].Literal.AsNumber());

            var bFree = LoadoutValidator.ToBlock("attack()");
            Assert.AreEqual(0, bFree.Args.Length);

            Assert.IsNull(LoadoutValidator.ToBlock("goto()"));
        }
    }
}
