using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;

namespace CPPRogue.Core.Code.Builtins
{
    /// <summary>
    /// attack() / attack(n)：攻击最近的敌人。
    /// 无参用默认伤害；带参则参数为伤害值——n 可以是字面量、变量或表达式（attack(a*3)）。
    /// </summary>
    public sealed class AttackBuiltin : IBuiltin
    {
        public const float DefaultDamage = 10f;
        public const float DefaultRadius = 1.5f;

        public string Name => "attack";
        public int CpuCost => 2;
        public int ParamCount => 1;

        public void Invoke(ExecContext ctx, Value[] args)
        {
            float damage = args.Length > 0 ? (float)args[0].AsNumber() : DefaultDamage;
            ctx.World.Attack(damage, DefaultRadius);
        }
    }
}
