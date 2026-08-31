using CPPRogue.Core.Combat;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// ICombatWorld 的表现层实现（端口对接处）：
    /// attack() → 瞄准最近敌人发射子弹（EnemySim 结算）；heal/shield → 数值与 HUD 反馈。
    /// Director 缺席时退回旧演示路径（随机方向子弹 + 直接改血量）。
    /// </summary>
    public sealed class CombatWorldBridge : MonoBehaviour, ICombatWorld
    {
        public PlayerController Player;
        public RoutineHud Hud;
        public EnemyDirector Director;

        public void Attack(float damage, float radius)
        {
            if (Director != null)
            {
                Director.PlayerAttack(damage, radius);
                if (Hud != null)
                    Hud.Log($"attack({damage:0.#}) → 射向鼠标方向");
                return;
            }

            // 旧演示路径：无敌人世界时的兜底
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Bullet.Spawn(transform.position, angle, damage);
            if (Hud != null)
                Hud.Log($"attack({damage:0.#}) → 发射子弹");
        }

        public void Heal(float amount)
        {
            if (Director != null)
            {
                Director.HealPlayer(amount);   // 血量以 sim 为准，PlayerController.Hp 由 Director 同步
            }
            else if (Player != null)
            {
                Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + amount);
            }
            if (Hud != null)
                Hud.Log($"heal({amount:0.#}) → 回血");
        }

        public void Shield(float amount, int durationTicks)
        {
            if (Director != null)
                Director.ShieldPlayer(amount);
            if (Hud != null)
                Hud.Log($"shield({amount:0.#}) → 护盾 +{amount:0.#}（持续到下个 tick）");
        }

        public void BeginTick()
        {
            Director?.BeginTick();   // 清空"持续 1 tick"的护盾
        }

        public void TimeoutPunish(float maxHpFraction)
        {
            Director?.TimeoutPunish(maxHpFraction);
        }
    }
}
