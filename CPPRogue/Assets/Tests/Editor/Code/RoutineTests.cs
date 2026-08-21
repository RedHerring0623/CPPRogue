using NUnit.Framework;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;

namespace CPPRogue.Core.Tests.Code
{
    [TestFixture]
    public class RoutineTests
    {
        [Test]
        public void Constructor_StoresLines_WithDefaultLimit()
        {
            var routine = new Routine(new[] { Block.Call("attack"), Block.Call("heal", Expr.Num(5)) });

            Assert.AreEqual(2, routine.LineCount);
            Assert.AreEqual(Routine.DefaultMaxLines, routine.MaxLines);
        }

        [Test]
        public void Constructor_NullLines_BecomesEmpty()
        {
            var routine = new Routine(null);

            Assert.AreEqual(0, routine.LineCount);
        }

        [Test]
        public void Constructor_ExceedsMaxLines_Throws()
        {
            // §4：初始 8 行——第 9 行拼不进去
            var nine = new Block[9];
            for (int i = 0; i < nine.Length; i++)
                nine[i] = Block.Call("attack");

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Routine(nine));
        }

        [Test]
        public void Constructor_ExtendedMaxLines_AllowsMore()
        {
            // 装备/成长扩展行数上限
            var nine = new Block[9];
            for (int i = 0; i < nine.Length; i++)
                nine[i] = Block.Call("attack");

            var routine = new Routine(nine, maxLines: 12);

            Assert.AreEqual(9, routine.LineCount);
            Assert.AreEqual(12, routine.MaxLines);
        }
    }
}
