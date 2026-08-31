using System.Collections.Generic;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Combat;

namespace CPPRogue.Core.Tests.Fakes
{
    /// <summary>测试替身：只记录 builtin 报上来的意图，用于断言"代码执行 → 世界收到了什么"。</summary>
    public sealed class FakeCombatWorld : ICombatWorld
    {
        public readonly List<(float Damage, float Radius)> Attacks = new List<(float, float)>();
        public readonly List<float> Heals = new List<float>();
        public readonly List<(float Amount, int Ticks)> Shields = new List<(float, int)>();
        public int BeginTicks;
        public readonly List<float> TimeoutPunishes = new List<float>();

        public void Attack(float damage, float radius)
        {
            Attacks.Add((damage, radius));
        }

        public void Heal(float amount)
        {
            Heals.Add(amount);
        }

        public void Shield(float amount, int durationTicks)
        {
            Shields.Add((amount, durationTicks));
        }

        public void BeginTick()
        {
            BeginTicks++;
        }

        public void TimeoutPunish(float maxHpFraction)
        {
            TimeoutPunishes.Add(maxHpFraction);
        }
    }

    /// <summary>测试替身：提供 hp 之类的只读系统属性。</summary>
    public sealed class FakePropertySource : IPropertySource
    {
        private readonly Dictionary<string, Value> _props;

        public FakePropertySource(Dictionary<string, Value> props)
        {
            _props = props ?? new Dictionary<string, Value>();
        }

        public bool TryRead(string name, out Value value)
        {
            return _props.TryGetValue(name, out value);
        }
    }
}
