namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 异常（§1.4，原远程兵）：移动到 2x 内停止，每 2s 向玩家**当前位置** throw 一颗子弹
    /// （不预判，走位可躲）；玩家进入 x 内则边后退边射击，退到 2x 外重新逼近。
    /// IDEA.md"敌人可以对你 throw 异常"的落地。
    /// </summary>
    public sealed class RangedBrain : IEnemyBrain
    {
        private float _fireTimer;

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            var cfg = sim.Config;
            float dist = Vec2.Distance(self.Position, sim.PlayerPosition);

            if (dist < cfg.ExceptionRetreatRange)
            {
                // 贴脸：背向玩家后退
                Vec2 away = (self.Position - sim.PlayerPosition).Normalized;
                EnemyMovement.MoveTowards(self, self.Position + away, self.Speed, dt);
            }
            else if (dist > cfg.ExceptionStopRange)
            {
                EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
            }

            // 装填只在射程内走：追击路上不倒计时，到位即首发
            if (dist <= cfg.ExceptionStopRange)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0f)
                {
                    _fireTimer = cfg.ExceptionFireInterval;
                    sim.SpawnEnemyBullet(self, sim.PlayerPosition,
                        StatTable.Speed(cfg.ExceptionBulletSpeedLv), StatTable.Atk(cfg.ExceptionBulletAtkLv));
                    self.StateLabel = "throw";
                }
            }
        }
    }
}
