using System;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Combat;

namespace CPPRogue.DemoUI
{
    /// <summary>
    /// Demo 的战斗世界：一个木桩敌人 + 玩家血条，把 ICombatWorld 的调用变成可见的数值变化和日志。
    /// 相当于 Unity 表现层的迷你替身。
    /// </summary>
    public sealed class DemoCombatWorld : ICombatWorld
    {
        public const float PlayerMaxHp = 100f;
        public const float EnemyMaxHp = 120f;

        public float PlayerHp = PlayerMaxHp;
        public float EnemyHp = EnemyMaxHp;
        public int Kills;
        public int Attacks;
        public int Heals;
        public int Shields;

        private readonly Action<string> _log;

        public DemoCombatWorld(Action<string> log)
        {
            _log = log;
        }

        public void Attack(float damage, float radius)
        {
            Attacks++;
            EnemyHp -= damage;
            if (EnemyHp <= 0f)
            {
                Kills++;
                EnemyHp = EnemyMaxHp;
                _log($"attack({Fmt(damage)}) → 击杀！敌人重生");
            }
            else
            {
                _log($"attack({Fmt(damage)}) → 敌人剩 {Fmt(EnemyHp)} HP");
            }
        }

        public void Heal(float amount)
        {
            Heals++;
            PlayerHp = Math.Min(PlayerMaxHp, PlayerHp + amount);
            _log($"heal({Fmt(amount)}) → 玩家回血，剩 {Fmt(PlayerHp)}");
        }

        public void Shield(float amount, int durationTicks)
        {
            Shields++;
            _log($"shield({Fmt(amount)}) → 护盾，持续 {durationTicks} tick");
        }

        private static string Fmt(float v)
        {
            return v.ToString("0.#");
        }
    }

    /// <summary>hp 等系统属性：实时桥接 Demo 战斗状态。</summary>
    public sealed class DemoPropertySource : IPropertySource
    {
        private readonly DemoCombatWorld _world;

        public DemoPropertySource(DemoCombatWorld world)
        {
            _world = world;
        }

        public bool TryRead(string name, out Value value)
        {
            if (name == "hp")
            {
                value = Value.Of(_world.PlayerHp / DemoCombatWorld.PlayerMaxHp);
                return true;
            }
            value = Value.Zero;
            return false;
        }
    }
}
