namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 怪物种类。属性数值与行为规格见 EnemyDesign.md；命名纪律：机制先行，梗名是糖。
    /// </summary>
    public enum EnemyKind
    {
        /// <summary>普通：直线追击碰撞（原杂兵）。</summary>
        Bug,

        /// <summary>普通：快而脆的追击（原快步兵）。</summary>
        NullPointer,

        /// <summary>普通：停下锁矢量 → 1s → 冲刺（原冲刺兵）。</summary>
        Breakpoint,

        /// <summary>普通：2x 外投掷异常（原远程兵）。</summary>
        Exception,

        /// <summary>精英：死亡分裂为两份拷贝。</summary>
        DeepCopy,

        /// <summary>精英：引信倒计时归零自爆。</summary>
        Watchdog,

        /// <summary>精英：读条期双倍易伤，读完变强。</summary>
        Compiling,

        /// <summary>精英：快 3s / 降频 2s 循环。</summary>
        Overheat,

        /// <summary>精英：3x 环绕 + 高频低压弹幕（原中断风暴）。</summary>
        Storm,

        /// <summary>精英：孵化僵尸进程，死后全场 reap。</summary>
        Parent,

        /// <summary>Parent 的附属怪，不单独出现在出怪表。</summary>
        Zombie,
    }
}
