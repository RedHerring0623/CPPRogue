using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;

namespace CPPRogue.Core.Code.Builtins
{
    /// <summary>heal(n)：回复 n 点生命（§3.1）。周期比 attack 贵——保命要有代价。</summary>
    public sealed class HealBuiltin : IBuiltin
    {
        public string Name => "heal";
        public int CpuCost => 3;
        public int ParamCount => 1;

        public void Invoke(ExecContext ctx, Value[] args)
        {
            float amount = args.Length > 0 ? (float)args[0].AsNumber() : 0f;
            ctx.World.Heal(amount);
        }
    }
}
