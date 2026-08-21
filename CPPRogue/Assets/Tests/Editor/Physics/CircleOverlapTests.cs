using CPPRogue.Core.Physics;
using NUnit.Framework;
using UnityEngine;

namespace CPPRogue.Core.Tests.Physics
{
    /// <summary>
    /// CircleOverlap 的单元测试，同时演示：测试里可以直接用 UnityEngine 的数学类型。
    /// </summary>
    [TestFixture]
    public class CircleOverlapTests
    {
        [Test]
        public void Overlaps_CirclesApart_ReturnsFalse()
        {
            bool hit = CircleOverlap.Overlaps(
                new Vector2(0f, 0f), 1f,
                new Vector2(5f, 0f), 1f);

            Assert.IsFalse(hit);
        }

        [Test]
        public void Overlaps_CirclesIntersecting_ReturnsTrue()
        {
            bool hit = CircleOverlap.Overlaps(
                new Vector2(0f, 0f), 1.5f,
                new Vector2(2f, 0f), 1.5f);

            Assert.IsTrue(hit);
        }

        [Test]
        public void Overlaps_TouchingExactly_CountsAsHit()
        {
            // 圆心距恰好等于半径之和（2 = 1 + 1），贴边命中
            bool hit = CircleOverlap.Overlaps(
                new Vector2(0f, 0f), 1f,
                new Vector2(2f, 0f), 1f);

            Assert.IsTrue(hit);
        }

        [Test]
        public void Overlaps_ZeroRadiusPointCircle_StillUsable()
        {
            // 半径 0 退化为点 vs 圆：点在圆上算命中
            bool hit = CircleOverlap.Overlaps(
                new Vector2(3f, 0f), 0f,
                new Vector2(0f, 0f), 3f);

            Assert.IsTrue(hit);
        }

        [Test]
        public void Contains_PointInside_ReturnsTrue()
        {
            bool hit = CircleOverlap.Contains(
                new Vector2(0f, 0f), 2f,
                new Vector2(1.4f, 1.4f));

            Assert.IsTrue(hit);
        }

        [Test]
        public void Contains_PointOutside_ReturnsFalse()
        {
            bool hit = CircleOverlap.Contains(
                new Vector2(0f, 0f), 2f,
                new Vector2(3f, 0f));

            Assert.IsFalse(hit);
        }
    }
}
