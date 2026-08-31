namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 编译中（§2.7）：出场为编译态——hp1 atk0 spd0、头顶 5s 进度、受伤 ×2；
    /// 读完条转为运行态：hp3 atk2 spd3 变强追击。读条期抢杀，漏掉就变强。
    /// </summary>
    public sealed class CompilingBrain : IEnemyBrain
    {
        private readonly float _compileTime;
        private float _remaining;
        private bool _compiled;

        public CompilingBrain(float compileTime)
        {
            _compileTime = compileTime;
            _remaining = compileTime;
        }

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            if (_compiled)
            {
                EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
                return;
            }

            _remaining -= dt;
            self.StateLabel = $"compiling {_remaining:0.0}s";
            if (_remaining > 0f)
                return;

            var cfg = sim.Config;
            _compiled = true;
            self.StateLabel = "";
            self.MaxHp = cfg.CompileRuntimeHp;
            self.Hp = self.MaxHp;
            self.Atk = cfg.CompileRuntimeAtk;
            self.Speed = StatTable.Speed(cfg.CompileRuntimeSpdLv);
            self.DamageTakenMultiplier = 1f;
            sim.Publish(SimEvent.FromEnemy(SimEventType.Compiled, self));
        }
    }
}
