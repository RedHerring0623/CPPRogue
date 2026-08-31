namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 看门狗（§2.6）：spd1 缓慢追击，出场自带引信倒计时（头顶显示），
    /// 归零自爆——以自身为中心 3x 半径 atk3；被击杀则解除。强制优先击杀目标。
    /// </summary>
    public sealed class WatchdogBrain : IEnemyBrain
    {
        private readonly float _fuse;
        private float _remaining;

        public WatchdogBrain(float fuse)
        {
            _fuse = fuse;
            _remaining = fuse;
        }

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            _remaining -= dt;
            if (_remaining <= 0f)
            {
                sim.Explode(self, sim.Config.WatchdogBlastRadius,
                    sim.Config.WatchdogBlastAtk);
                return;
            }
            self.StateLabel = $"watch {_remaining:0.0}s";
            EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
        }
    }
}
