using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;

namespace CPPRogue.Core.Code.Builtins
{
    /// <summary>shield(n)：获得持续到下个 tick 的护盾（§3.1）。</summary>
    public sealed class ShieldBuiltin : IBuiltin
    {
        public string Name => "shield";
        public int CpuCost => 2;
        public int ParamCount => 1;

        public void Invoke(ExecContext ctx, Value[] args)
        {
            float amount = args.Length > 0 ? (float)args[0].AsNumber() : 0f;
            ctx.World.Shield(amount, 1);
        }
    }
}
