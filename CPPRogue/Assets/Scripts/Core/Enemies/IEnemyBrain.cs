namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 敌人行为状态机（EnemyDesign.md §0：怪物和玩家一样用 tick 状态机更新）。
    /// 每帧由 EnemySim 调 Tick：Brain 直接写 self.Position/StateLabel，
    /// 开火/爆炸/孵化走 sim 接口（保持副作用可测试）。
    /// Brain 是每怪一实例，无状态的行为可共享单例（如 ChaserBrain）。
    /// </summary>
    public interface IEnemyBrain
    {
        void Tick(Enemy self, EnemySim sim, float dt);
    }

    /// <summary>Brain 共用的运动小工具。</summary>
    public static class EnemyMovement
    {
        /// <summary>以 speed 朝目标移动一个 dt 步长；返回 true 表示已到达目标点。</summary>
        public static bool MoveTowards(Enemy self, Vec2 target, float speed, float dt)
        {
            Vec2 delta = target - self.Position;
            float dist = delta.Magnitude;
            float step = speed * dt;
            if (dist <= step || dist < 1e-5f)
            {
                self.Position = target;
                return true;
            }
            self.Position += delta / dist * step;
            return false;
        }
    }
}
