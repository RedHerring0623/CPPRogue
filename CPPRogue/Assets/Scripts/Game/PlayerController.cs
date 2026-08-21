using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>主角：双手只负责走位（WASD/方向键），战斗全部交给 Routine。</summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public float Speed = 6f;
        public float MaxHp = 100f;
        public float Hp = 100f;

        private void Update()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            var move = new Vector3(h, v, 0f);
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            transform.position += move * (Speed * Time.deltaTime);
        }
    }
}
