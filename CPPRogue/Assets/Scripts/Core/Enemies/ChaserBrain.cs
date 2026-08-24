namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 直线追击碰撞（§1.1 / §1.2）：Bug、空指针、僵尸进程、编译后的运行态共用。
    /// 无内部状态，全局单例。
    /// </summary>
    public sealed class ChaserBrain : IEnemyBrain
    {
        public static readonly ChaserBrain Instance = new ChaserBrain();

        public void Tick(Enemy self, EnemySim sim, float dt)
        {
            EnemyMovement.MoveTowards(self, sim.PlayerPosition, self.Speed, dt);
        }
    }
}
