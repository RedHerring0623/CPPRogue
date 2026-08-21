namespace CPPRogue.Core.Combat
{
    /// <summary>
    /// 逻辑层的唯一副作用出口：builtin 只报"想做什么"，不关心谁来实现。
    /// 单元测试用 Fake 记录调用做断言；Unity 表现层实现它来驱动真正的怪/特效/音效。
    /// 以后加新动词（召唤、闪避、标记）在这里加方法，接口按动词语义命名，不按实现命名。
    /// </summary>
    public interface ICombatWorld
    {
        void Attack(float damage, float radius);

        void Heal(float amount);

        void Shield(float amount, int durationTicks);
    }
}
