namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 一只怪物的全部可变状态。结算类字段（Position/Hp/Dead）只有 Sim 写；
    /// 行为字段（Speed/StateLabel/Telegraph*）由 Brain 写；视图只读。
    /// 项目纪律：Core 不做防御性封装，靠"谁该写谁"的注释约束。
    /// </summary>
    public sealed class Enemy
    {
        public int Id;
        public EnemyKind Kind;
        public string DisplayName;          // 中文梗名，日志/飘字用

        /// <summary>行为状态机实例；Brain 自身字段就是它的私有状态。</summary>
        public IEnemyBrain Brain;

        // —— 结算状态（Sim 写）——
        public Vec2 Position;
        public float Radius;
        public float Hp;
        public float MaxHp;
        public float Atk;
        public bool Dead;

        /// <summary>僵尸进程的父进程 Id；其它怪为 0。</summary>
        public int ParentId;

        /// <summary>深拷贝世代：0 = hp4 本体，1 = hp2 副本，2 = hp1 副本（不再分裂）。</summary>
        public int Generation;

        // —— 行为状态（Brain 写，Sim 初始化）——
        public float Speed;                 // 当前移速：Brain 可按状态改（过热降频、编译态为 0）

        /// <summary>受伤倍率：编译态 = 2，常态 = 1（§2.7）。</summary>
        public float DamageTakenMultiplier = 1f;

        /// <summary>视图状态标签：断点 paused/continue、看门狗倒计时、编译进度。</summary>
        public string StateLabel = "";

        /// <summary>冲刺预兆线：Lock 阶段由 Brain 写，视图画线、玩家据此骗冲。</summary>
        public bool TelegraphActive;
        public Vec2 TelegraphFrom;
        public Vec2 TelegraphTo;

        public float HpFraction => MaxHp > 0f ? Hp / MaxHp : 0f;
        public bool Alive => !Dead;
    }
}
