using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Code.Runtime;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>系统只读属性源：if (hp &lt; 0.5) 里的 hp 从这里读。</summary>
    public sealed class PlayerPropertySource : IPropertySource
    {
        private readonly PlayerController _player;

        public PlayerPropertySource(PlayerController player)
        {
            _player = player;
        }

        public bool TryRead(string name, out Value value)
        {
            if (_player != null && name == "hp")
            {
                value = Value.Of(Mathf.Clamp01(_player.Hp / _player.MaxHp));
                return true;
            }
            value = Value.Zero;
            return false;
        }
    }
}
