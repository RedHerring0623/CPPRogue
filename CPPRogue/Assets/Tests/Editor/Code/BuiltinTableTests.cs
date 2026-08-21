using NUnit.Framework;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Builtins;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Tests.Fakes;

namespace CPPRogue.Core.Tests.Code
{
    [TestFixture]
    public class BuiltinTableTests
    {
        [Test]
        public void CreateDefault_ContainsCoreVocabulary()
        {
            var table = BuiltinTable.CreateDefault();

            Assert.IsTrue(table.TryGet("attack", out _));
            Assert.IsTrue(table.TryGet("heal", out _));
            Assert.IsTrue(table.TryGet("shield", out _));
            Assert.GreaterOrEqual(table.Count, 3);
        }

        [Test]
        public void Register_DuplicateName_Throws()
        {
            var table = new BuiltinTable();
            table.Register(new AttackBuiltin());

            Assert.Throws<System.ArgumentException>(() => table.Register(new AttackBuiltin()));
        }

        [Test]
        public void Register_CustomFunction_IsCallableFromProgram()
        {
            // 新技能 = 一个类 + 一行注册，解释器和其他技能零改动
            var table = new BuiltinTable();
            table.Register(new PingBuiltin());
            var world = new FakeCombatWorld();
            var ctx = new ExecContext(world, table);

            new Interpreter().RunTick(new Routine(new[] { Block.Call("ping") }), ctx);

            Assert.AreEqual(1, world.Heals.Count);
        }

        private sealed class PingBuiltin : IBuiltin
        {
            public string Name => "ping";
            public int CpuCost => 1;
            public int ParamCount => 0;

            public void Invoke(ExecContext ctx, Value[] args)
            {
                ctx.World.Heal(1f);
            }
        }
    }
}
