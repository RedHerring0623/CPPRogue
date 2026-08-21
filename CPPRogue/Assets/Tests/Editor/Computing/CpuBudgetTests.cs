using CPPRogue.Core.Computing;
using NUnit.Framework;

namespace CPPRogue.Core.Tests.Computing
{
    /// <summary>
    /// CpuBudget 的单元测试，同时作为测试工程的示例模板：
    /// 一个逻辑类对应一个测试类，测试类与被测类同名 + Tests 后缀，命名空间同路径。
    /// </summary>
    [TestFixture]
    public class CpuBudgetTests
    {
        [Test]
        public void Constructor_NonNegativeCapacity_Succeeds()
        {
            var budget = new CpuBudget(100);

            Assert.AreEqual(100, budget.Capacity);
            Assert.AreEqual(100, budget.Remaining);
            Assert.IsFalse(budget.IsExhausted);
        }

        [Test]
        public void Constructor_NegativeCapacity_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new CpuBudget(-1));
        }

        [Test]
        public void TrySpend_WithinBudget_DeductsAndReturnsTrue()
        {
            var budget = new CpuBudget(100);

            bool spent = budget.TrySpend(30);

            Assert.IsTrue(spent);
            Assert.AreEqual(70, budget.Remaining);
        }

        [Test]
        public void TrySpend_InsufficientBudget_ReturnsFalseAndKeepsRemaining()
        {
            var budget = new CpuBudget(50);

            // 周期不够 = 语句被"优化掉"，但不允许扣成负数或部分扣除
            bool spent = budget.TrySpend(80);

            Assert.IsFalse(spent);
            Assert.AreEqual(50, budget.Remaining);
        }

        [Test]
        public void TrySpend_ExactlyToZero_ExhaustsBudget()
        {
            var budget = new CpuBudget(40);

            bool spent = budget.TrySpend(40);

            Assert.IsTrue(spent);
            Assert.AreEqual(0, budget.Remaining);
            Assert.IsTrue(budget.IsExhausted);
        }

        [Test]
        public void TrySpend_NegativeCycles_Throws()
        {
            var budget = new CpuBudget(100);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => budget.TrySpend(-5));
        }

        [Test]
        public void Reset_RestoresFullBudget()
        {
            var budget = new CpuBudget(100);
            budget.TrySpend(90);

            budget.Reset();

            Assert.AreEqual(100, budget.Remaining);
            Assert.IsFalse(budget.IsExhausted);
        }

        [Test]
        public void TrySpend_ZeroCostOnEmptyBudget_AlwaysSucceeds()
        {
            var budget = new CpuBudget(0);

            // 零消耗语句（注释/空行为）即使在零预算 tick 也应能执行
            bool spent = budget.TrySpend(0);

            Assert.IsTrue(spent);
            Assert.IsTrue(budget.IsExhausted);
        }
    }
}
