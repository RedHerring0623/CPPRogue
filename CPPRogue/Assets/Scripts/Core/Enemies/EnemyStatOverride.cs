using System.Collections.Generic;

namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 图鉴直值覆盖（2026-08-31 数值改版）：hp/atk 不再走等级换算，
    /// 直接使用 EnemyTable.json 里的数字（bug 的 hp2 就是 2 点生命）。
    /// spd 仍是等级（1-5 → StatTable 换算）。表现层从图鉴数据构建这张表注入 EnemySim；
    /// 传 null 时用 EnemyArchetypes 里的内置同款数值。
    /// </summary>
    public sealed class EnemyStatOverride
    {
        public float Hp;
        public float Atk;
    }
}
