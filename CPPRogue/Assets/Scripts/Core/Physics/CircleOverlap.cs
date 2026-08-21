using UnityEngine;

namespace CPPRogue.Core.Physics
{
    /// <summary>
    /// 圆形碰撞的纯逻辑判定。
    /// 逻辑层可以直接用 UnityEngine 的数学类型（Vector2/Mathf/AnimationCurve），
    /// 但仍然不碰 GameObject/物理引擎/Time——那些属于表现层，不进单元测试。
    /// </summary>
    public static class CircleOverlap
    {
        /// <summary>两个圆是否重叠（圆心距 &lt;= 半径之和，贴边算命中）。</summary>
        public static bool Overlaps(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
        {
            float radiusSum = radiusA + radiusB;
            return (centerA - centerB).sqrMagnitude <= radiusSum * radiusSum;
        }

        /// <summary>点是否落在圆内（边界算在内）。用于子弹命中判定。</summary>
        public static bool Contains(Vector2 center, float radius, Vector2 point)
        {
            return (point - center).sqrMagnitude <= radius * radius;
        }
    }
}
