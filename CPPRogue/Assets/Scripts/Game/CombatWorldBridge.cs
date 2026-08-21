using CPPRogue.Core.Combat;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// ICombatWorld 的表现层实现（端口对接处）：
    /// attack() → 从主角位置向随机方向发射子弹；heal/shield → 数值与 HUD 反馈。
    /// </summary>
    public sealed class CombatWorldBridge : MonoBehaviour, ICombatWorld
    {
        public PlayerController Player;
        public RoutineHud Hud;

        public void Attack(float damage, float radius)
        {
            // 演示：随机方向。正式版改成"最近的敌人"（命中判定用 Core 的 CircleOverlap）
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Bullet.Spawn(transform.position, angle, damage);
            if (Hud != null)
                Hud.Log($"attack({damage:0.#}) → 发射子弹");
        }

        public void Heal(float amount)
        {
            if (Player != null)
                Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + amount);
            if (Hud != null)
                Hud.Log($"heal({amount:0.#}) → 回血");
        }

        public void Shield(float amount, int durationTicks)
        {
            if (Hud != null)
                Hud.Log($"shield({amount:0.#}) → 护盾，持续 {durationTicks} tick");
        }
    }
}
