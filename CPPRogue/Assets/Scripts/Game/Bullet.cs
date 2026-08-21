using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>演示子弹：直线飞行，超时消失。以后在这里接命中判定（CircleOverlap）。</summary>
    public sealed class Bullet : MonoBehaviour
    {
        public float Speed = 12f;
        public float Lifetime = 2f;

        private static Sprite _circle;

        private Vector3 _direction;
        private float _age;

        public static void Spawn(Vector3 origin, float angleRad, float damage)
        {
            if (_circle == null)
                _circle = SpriteFactory.CreateCircle(48, new Color(1f, 0.84f, 0.28f));

            var go = new GameObject("Bullet");
            go.transform.position = origin;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _circle;
            renderer.sortingOrder = 5;
            float size = Mathf.Clamp(0.28f + damage * 0.01f, 0.28f, 0.7f); // 伤害越大子弹越大（视觉反馈）
            go.transform.localScale = new Vector3(size, size, 1f);

            var bullet = go.AddComponent<Bullet>();
            bullet._direction = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
        }

        private void Update()
        {
            transform.position += _direction * (Speed * Time.deltaTime);
            _age += Time.deltaTime;
            if (_age >= Lifetime)
                Destroy(gameObject);
        }
    }
}
