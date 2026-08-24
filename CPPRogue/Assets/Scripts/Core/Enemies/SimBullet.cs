namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 模拟层子弹（敌我通用）。FromPlayer 区分阵营；
    /// Label 携带表现层提示（异常子弹的异常类型名，用于着色/拖尾）。
    /// </summary>
    public sealed class SimBullet
    {
        public int Id;
        public Vec2 Position;
        public Vec2 Direction;
        public float Speed;
        public float Damage;
        public float HitRadius;
        public bool FromPlayer;
        public float Age;
        public bool Dead;
        public string Label;

        /// <summary>敌弹在玩家无敌帧中穿过时作废：这颗弹永远不再对玩家结算，
        /// 否则慢速弹会在帧结束后从背后再打一次。</summary>
        public bool PassedThroughPlayer;
    }
}
