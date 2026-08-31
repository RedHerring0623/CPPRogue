using CPPRogue.Core.Enemies;
using CPPRogue.Core.Loot;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>掉落物（表现层）：材料小球，上下浮动；拾取判定由 EnemyDirector 每帧检查。</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public MaterialKind Kind { get; private set; }
        public Vec2 Position { get; private set; }

        private float _bob;

        public void Setup(Vec2 position, MaterialKind kind)
        {
            Position = position;
            Kind = kind;
            transform.position = new Vector3(position.X, position.Y, 0f);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.CreateCircle(64, CodexPanel.MaterialColor(kind));
            renderer.sortingOrder = 5;
            transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        }

        /// <summary>浮动动画（Director 每帧驱动，timeScale=0 时自然停住）。</summary>
        public void TickAnimation(float dt)
        {
            _bob += dt;
            transform.position = new Vector3(Position.X, Position.Y + Mathf.Sin(_bob * 3f) * 0.12f, 0f);
        }
    }
}
