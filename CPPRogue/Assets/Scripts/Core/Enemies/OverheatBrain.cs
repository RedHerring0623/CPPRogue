namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 过热（§2.8）：spd4 全速追 3s → 过热降频到 spd1 持续 2s（头顶冒烟）→ 恢复，循环。
    /// 节奏怪：快段躲，慢段打。
    /// </summary>
    public sealed class OverheatBrain : IEnemyBrain
    {
        private readonly EnemySimConfig _cfg;
        private bool _hot = true;
        private float _timer = -1f;    // -1 = 首帧初始化

        public OverheatBrain(EnemySimConfig cfg)
        {
            _cfg = cfg;
        }

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            if (_timer < 0f)
            {
                _timer = _cfg.OverheatFastTime;
            }
            else
            {
                _timer -= dt;
                if (_timer <= 0f)
                {
                    _hot = !_hot;
                    _timer = _hot ? _cfg.OverheatFastTime : _cfg.OverheatSlowTime;
                }
            }

            self.Speed = StatTable.Speed(_hot ? _cfg.OverheatFastSpdLv : _cfg.OverheatSlowSpdLv);
            self.StateLabel = _hot ? "" : "throttling";
            EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
        }
    }
}
