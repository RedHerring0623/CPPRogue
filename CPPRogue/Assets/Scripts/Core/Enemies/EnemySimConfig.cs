namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// EnemyDesign.md 的全局规则 + 各怪时序参数的唯一来源。
    /// 距离参数按"x 的倍数"书写（x = 玩家碰撞半径 × 3，§0），构造时换算成绝对值；
    /// Brain 只读换算结果，调平衡不碰行为代码。
    /// </summary>
    public sealed class EnemySimConfig
    {
        // —— §0 全局规则 ——
        public float PlayerRadius = 0.5f;
        public float PlayerMaxHp = 10f;   // 2026-08-31 数值改版：与图鉴直值同量级（bug atk1 = 10% /次）

        /// <summary>碰撞结算：玩家受击后的全局无敌帧——唯一的受击间隔（§0）。</summary>
        public float PlayerInvulnTime = 0.1f;

        /// <summary>碰撞击退：接触结算后玩家获得的速度（单位/秒），方向为远离该敌人。</summary>
        public float ContactKnockbackSpeed = 8f;

        /// <summary>击退速度的线性衰减时长（归零即恢复完全操控）。</summary>
        public float KnockbackTime = 0.25f;

        // —— 断点（§1.3）——
        public float BreakpointStopRangeX = 2f;    // 进入 2x 内停止并锁矢量
        public float BreakpointLockTime = 1f;      // 锁定后的等待（预兆线展示窗口）
        public int BreakpointDashSpeedLv = 4;
        public float BreakpointDashDistanceX = 4f; // 冲刺 4x 距离
        public float BreakpointRecoverTime = 0.75f;
        public float BreakpointCooldown = 2f;

        // —— 异常（§1.4）——
        public float ExceptionStopRangeX = 3f;     // 进入 3x 内停下投掷
        public float ExceptionRetreatRangeX = 1f;  // 玩家进入 x 内则边退边射
        public float ExceptionFireInterval = 2f;
        public int ExceptionBulletSpeedLv = 2;
        public int ExceptionBulletAtkLv = 2;

        // —— 风暴（§2.9）——
        public float StormRingRangeX = 3f;
        public float StormFireInterval = 0.5f;
        public int StormBulletSpeedLv = 3;
        public int StormBulletAtkLv = 1;

        // —— 看门狗（§2.6）——
        public float WatchdogFuse = 10f;
        public float WatchdogBlastRadiusX = 3f;
        public float WatchdogBlastAtk = 3f;         // 图鉴直值（原等级制 2026-08-31 改版）

        // —— 编译中（§2.7）——
        public float CompileTime = 5f;
        public float CompileDamageTakenMultiplier = 2f;
        public float CompileRuntimeHp = 3f;         // 图鉴直值
        public float CompileRuntimeAtk = 2f;        // 图鉴直值
        public int CompileRuntimeSpdLv = 3;         // spd 仍是等级

        // —— 过热（§2.8）——
        public float OverheatFastTime = 3f;
        public float OverheatSlowTime = 2f;
        public int OverheatFastSpdLv = 4;
        public int OverheatSlowSpdLv = 1;

        // —— 父进程（§2.10）——
        public float ParentSpawnInterval = 3f;
        public int ParentMaxChildren = 8;

        // —— 子弹通用 ——
        public float BulletHitRadius = 0.2f;
        public float BulletLifetime = 6f;
        public float PlayerBulletSpeed = 7f;

        // —— 换算后的绝对值（Recalc 生成，Brain 直接读）——
        public float X;
        public float BreakpointStopRange;
        public float BreakpointDashDistance;
        public float ExceptionStopRange;
        public float ExceptionRetreatRange;
        public float StormRingRange;
        public float WatchdogBlastRadius;

        public EnemySimConfig()
        {
            Recalc();
        }

        /// <summary>改完 PlayerRadius 或任一倍率后重算派生值。</summary>
        public void Recalc()
        {
            X = PlayerRadius * 3f;
            BreakpointStopRange = BreakpointStopRangeX * X;
            BreakpointDashDistance = BreakpointDashDistanceX * X;
            ExceptionStopRange = ExceptionStopRangeX * X;
            ExceptionRetreatRange = ExceptionRetreatRangeX * X;
            StormRingRange = StormRingRangeX * X;
            WatchdogBlastRadius = WatchdogBlastRadiusX * X;
        }
    }
}
