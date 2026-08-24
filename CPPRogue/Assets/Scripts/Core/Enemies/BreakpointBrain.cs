namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 断点（§1.3，原冲刺兵）：进入 x 内 → 停止瞬间锁定"自身→玩家此刻位置"矢量
    /// 并亮预兆线（矢量锁定后不更新，玩家侧移即可骗冲）→ 等 1s → spd4 冲 2x
    /// → 0.75s 恢复硬直 → 2s 冷却 → 回到追击。
    /// 名字即机制：命中断点（停）→ 查看变量（等）→ F5 继续（冲）。
    /// </summary>
    public sealed class BreakpointBrain : IEnemyBrain
    {
        private enum Phase { Approach, LockWait, Dash, Recover, Cooldown }

        private Phase _phase = Phase.Approach;
        private Vec2 _dashDir;
        private float _timer;
        private float _dashRemaining;

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            var cfg = sim.Config;
            switch (_phase)
            {
                case Phase.Approach:
                    self.StateLabel = "";
                    self.TelegraphActive = false;
                    if (Vec2.Distance(self.Position, sim.PlayerPosition) <= cfg.BreakpointStopRange)
                        EnterLock(self, sim);
                    else
                        EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
                    break;

                case Phase.LockWait:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _dashRemaining = cfg.BreakpointDashDistance;
                        _phase = Phase.Dash;
                        self.StateLabel = "continue";
                    }
                    break;

                case Phase.Dash:
                    self.TelegraphActive = false;
                    float step = StatTable.Speed(cfg.BreakpointDashSpeedLv) * dt;
                    if (step >= _dashRemaining)
                    {
                        self.Position += _dashDir * _dashRemaining;
                        _timer = cfg.BreakpointRecoverTime;
                        _phase = Phase.Recover;
                        self.StateLabel = "";
                    }
                    else
                    {
                        self.Position += _dashDir * step;
                        _dashRemaining -= step;
                    }
                    break;

                case Phase.Recover:
                    self.StateLabel = "recovering";
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _timer = cfg.BreakpointCooldown;
                        _phase = Phase.Cooldown;
                    }
                    break;

                case Phase.Cooldown:
                    self.StateLabel = "";
                    _timer -= dt;
                    if (_timer <= 0f)
                        _phase = Phase.Approach;
                    break;
            }
        }

        private void EnterLock(Enemy self, EnemySim sim)
        {
            var cfg = sim.Config;
            _dashDir = (sim.PlayerPosition - self.Position).Normalized;
            _timer = cfg.BreakpointLockTime;
            _phase = Phase.LockWait;
            self.StateLabel = "paused at breakpoint";

            // 预兆线随本体移动更新终点方向？不——矢量锁定是断点的核心反制点，线也锁定
            self.TelegraphActive = true;
            self.TelegraphFrom = self.Position;
            self.TelegraphTo = self.Position + _dashDir * cfg.BreakpointDashDistance;
        }
    }
}
