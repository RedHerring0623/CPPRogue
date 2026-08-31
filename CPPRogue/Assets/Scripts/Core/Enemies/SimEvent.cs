namespace CPPRogue.Core.Enemies
{
    public enum SimEventType
    {
        EnemySpawned,
        EnemyDied,

        /// <summary>看门狗自爆（视图画爆炸圈）。</summary>
        Exploded,

        /// <summary>编译中读完条进入运行态。</summary>
        Compiled,

        /// <summary>父进程死亡，全场僵尸进程被 reap。</summary>
        ChildrenReaped,

        PlayerHit,

        /// <summary>护盾吸收了伤害（UI 飘字/变盾条用）。</summary>
        ShieldAbsorbed,
    }

    /// <summary>
    /// 模拟事件：Step 期间产生的"表现层该知道的事"。
    /// 视图层每帧 DrainEvents 消费（飘字/音效/日志），逻辑层自己不留恋。
    /// </summary>
    public sealed class SimEvent
    {
        public SimEventType Type;
        public EnemyKind Kind;
        public int EnemyId;
        public Vec2 Position;

        /// <summary>Exploded 的爆炸半径。</summary>
        public float Radius;

        /// <summary>PlayerHit 的伤害量。</summary>
        public float Amount;

        public static SimEvent FromEnemy(SimEventType type, Enemy enemy)
        {
            return new SimEvent { Type = type, Kind = enemy.Kind, EnemyId = enemy.Id, Position = enemy.Position };
        }
    }
}
