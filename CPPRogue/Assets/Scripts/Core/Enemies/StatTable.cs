namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 属性等级（lv1-5）→ 具体数值的唯一映射（EnemyDesign.md §0）。
    /// 平衡调整只改这里的表；Brain / Sim 代码里禁止出现裸数值。
    /// 玩家基础速度 = Spd(3)（§0 玩家速度参照）。
    /// </summary>
    public static class StatTable
    {
        public static readonly float[] Health = { 10f, 20f, 40f, 80f, 160f };
        public static readonly float[] Attack = { 4f, 8f, 15f, 30f, 60f };
        public static readonly float[] Spd = { 1.2f, 2.4f, 3.6f, 4.8f, 6.0f }; // 世界单位/秒

        public static float Hp(int lv) => Health[lv - 1];
        public static float Atk(int lv) => Attack[lv - 1];
        public static float Speed(int lv) => Spd[lv - 1];
    }
}
