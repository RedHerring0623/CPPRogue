using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>正交相机平滑跟随目标（2D 俯视角，视角锁定主角）。</summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smoothing = 6f;

        private readonly Vector3 _offset = new Vector3(0f, 0f, -10f);

        private void LateUpdate()
        {
            if (Target == null)
                return;
            Vector3 desired = Target.position + _offset;
            float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime); // 帧率无关的阻尼
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }
    }
}
