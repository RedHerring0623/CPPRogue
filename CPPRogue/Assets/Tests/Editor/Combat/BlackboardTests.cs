using NUnit.Framework;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Combat;

namespace CPPRogue.Core.Tests.Combat
{
    [TestFixture]
    public class BlackboardTests
    {
        [Test]
        public void TrySet_ThenTryGet_RoundTrip()
        {
            var board = new Blackboard();

            board.TrySet("rage", Value.Of(3));

            Assert.IsTrue(board.TryGet("rage", out Value v));
            Assert.AreEqual(3, v.AsNumber(), 0.0001);
        }

        [Test]
        public void TrySet_OverwritesExistingValue()
        {
            var board = new Blackboard();
            board.TrySet("a", Value.Of(1));

            board.TrySet("a", Value.Of(7));

            Assert.IsTrue(board.TryGet("a", out Value v));
            Assert.AreEqual(7, v.AsNumber(), 0.0001);
            Assert.AreEqual(1, board.Count);
        }

        [Test]
        public void SlotLimit_RejectsNewVariable()
        {
            // §4：变量槽位初始 2 个——第三个新变量被拒绝
            var board = new Blackboard(maxSlots: 2);

            Assert.IsTrue(board.TrySet("a", Value.Zero));
            Assert.IsTrue(board.TrySet("b", Value.Zero));
            Assert.IsFalse(board.TrySet("c", Value.Zero));
            Assert.IsFalse(board.Contains("c"));
        }

        [Test]
        public void SlotLimit_AllowsOverwriteWithinLimit()
        {
            var board = new Blackboard(maxSlots: 2);
            board.TrySet("a", Value.Of(1));
            board.TrySet("b", Value.Of(2));

            Assert.IsTrue(board.TrySet("a", Value.Of(9)));

            Assert.IsTrue(board.TryGet("a", out Value v));
            Assert.AreEqual(9, v.AsNumber(), 0.0001);
        }

        [Test]
        public void Clear_RemovesAllVariables()
        {
            var board = new Blackboard();
            board.TrySet("a", Value.Zero);
            board.TrySet("b", Value.Zero);

            board.Clear();

            Assert.AreEqual(0, board.Count);
            Assert.IsFalse(board.Contains("a"));
        }
    }
}
