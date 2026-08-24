namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 父进程（§2.10）：缓慢随机游走，每 3s 孵化一只僵尸进程（场上限 4）；
    /// 僵尸被杀后父进程会重新孵化补满（"杀不干净"），父进程死亡时全场僵尸被 reap。
    /// 孵化上限与 reap 结算在 EnemySim 里，这里只管游走和按节拍要孩子。
    /// </summary>
    public sealed class SpawnerBrain : IEnemyBrain
    {
        private readonly System.Random _rng;
        private float _spawnTimer;
        private float _wanderTimer;
        private Vec2 _wanderDir = Vec2.Zero;

        public SpawnerBrain(System.Random rng, EnemySimConfig cfg)
        {
            _rng = rng;
            _spawnTimer = cfg.ParentSpawnInterval;   // 出生后第一拍按间隔来，不秒孵
        }

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            var cfg = sim.Config;

            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = 2f + (float)_rng.NextDouble() * 2f;
                _wanderDir = Vec2.FromAngle((float)(_rng.NextDouble() * System.Math.PI * 2.0));
            }
            self.Position += _wanderDir * (self.Speed * dt);

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = cfg.ParentSpawnInterval;
                sim.TrySpawnChild(self);
            }
            self.StateLabel = $"children {sim.CountChildren(self)}/{cfg.ParentMaxChildren}";
        }
    }
}
