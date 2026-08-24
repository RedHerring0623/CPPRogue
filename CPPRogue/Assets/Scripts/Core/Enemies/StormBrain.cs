namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 风暴（§2.9，原中断风暴）：移动到 3x 环上，横向绕玩家游走，
    /// 每 0.5s 一发低压高速弹——高频低压弹幕，与异常（低频高压）互补。
    /// </summary>
    public sealed class StormBrain : IEnemyBrain
    {
        private readonly int _orbitSign;    // +1 / -1：顺/逆时针
        private float _fireTimer;
        private bool _loaded;               // 上环后先装填一发间隔，避免瞬发

        public StormBrain(int orbitSign)
        {
            _orbitSign = orbitSign >= 0 ? 1 : -1;
        }

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            var cfg = sim.Config;
            float dist = Vec2.Distance(self.Position, sim.PlayerPosition);

            if (dist > cfg.StormRingRange * 1.1f)
            {
                EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
                return;
            }

            // 环上游走：以玩家为圆心，沿切线推进角度，再向环点修正位置
            Vec2 toSelf = self.Position - sim.PlayerPosition;
            float angle = (float)System.Math.Atan2(toSelf.Y, toSelf.X);
            float angularSpeed = self.Speed / cfg.StormRingRange;
            angle += _orbitSign * angularSpeed * dt;
            Vec2 ringPoint = sim.PlayerPosition + Vec2.FromAngle(angle) * cfg.StormRingRange;
            EnemyMovement.MoveTowards(self, ringPoint, self.Speed, dt);

            if (!_loaded)
            {
                _loaded = true;
                _fireTimer = cfg.StormFireInterval;
                return;
            }
            _fireTimer -= dt;
            if (_fireTimer <= 0f)
            {
                _fireTimer = cfg.StormFireInterval;
                sim.SpawnEnemyBullet(self, sim.PlayerPosition,
                    StatTable.Speed(cfg.StormBulletSpeedLv), StatTable.Atk(cfg.StormBulletAtkLv));
            }
        }
    }
}
