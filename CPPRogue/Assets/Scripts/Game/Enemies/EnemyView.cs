using System.Collections.Generic;
using CPPRogue.Core.Enemies;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// 怪物视图：圆贴图 + 球上方名字/状态标签 + 技能判定圈（调平衡用）+ 断点冲刺预兆线。
    /// 只读 Enemy 的公开状态做渲染，不含任何逻辑。
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const int RingSegments = 48;

        private SpriteRenderer _body;
        private TextMesh _name;
        private TextMesh _label;
        private LineRenderer _range;
        private LineRenderer _telegraph;
        private Color _baseColor;

        /// <param name="skillRange">技能判定半径（调平衡可视化；0 = 该怪无范围技能，不画圈）。</param>
        public void Setup(Enemy e, float skillRange)
        {
            _baseColor = EnemyVisuals.KindColor(e.Kind);
            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = EnemyVisuals.KindSprite(e.Kind);
            _body.sortingOrder = 5;
            float scale = e.Radius * 2f;   // CreateCircle 的 Sprite 在 scale=1 时直径恰为 1
            transform.localScale = new Vector3(scale, scale, 1f);

            // 名字在球正上方、状态在名字上方；都做反缩放，字号/位置与世界尺寸解耦
            _name = CreateWorldText(e.DisplayName, e.Radius + 0.1f, scale,
                new Color(0.96f, 0.98f, 1f), fontSize: 26, charSize: 0.1f, sortingOrder: 11);
            _label = CreateWorldText(string.Empty, e.Radius + 0.44f, scale,
                new Color(0.82f, 0.88f, 1f), fontSize: 22, charSize: 0.09f, sortingOrder: 12);

            if (skillRange > 0f)
                CreateRangeRing(skillRange, scale);

            _telegraph = gameObject.AddComponent<LineRenderer>();
            _telegraph.material = EnemyVisuals.LineMaterial;
            _telegraph.startColor = _telegraph.endColor = new Color(1f, 0.35f, 0.25f, 0.5f);
            _telegraph.widthMultiplier = 0.06f;
            _telegraph.sortingOrder = 4;
            _telegraph.positionCount = 2;
            _telegraph.useWorldSpace = true;
            _telegraph.enabled = false;
        }

        /// <summary>技能判定圈开关（EnemyDirector 的 V 键驱动）。</summary>
        public void SetRangeVisible(bool visible)
        {
            if (_range != null)
                _range.enabled = visible;
        }

        public void Sync(Enemy e)
        {
            transform.position = new Vector3(e.Position.X, e.Position.Y, 0f);

            // 血量越低越暗，轻量替代血条
            float hpK = Mathf.Clamp01(e.HpFraction);
            _body.color = Color.Lerp(_baseColor * 0.35f, _baseColor, 0.4f + 0.6f * hpK);

            bool showLabel = !string.IsNullOrEmpty(e.StateLabel);
            _label.gameObject.SetActive(showLabel);
            if (showLabel && _label.text != e.StateLabel)
                _label.text = e.StateLabel;

            _telegraph.enabled = e.TelegraphActive;
            if (e.TelegraphActive)
            {
                _telegraph.SetPosition(0, new Vector3(e.TelegraphFrom.X, e.TelegraphFrom.Y, 0f));
                _telegraph.SetPosition(1, new Vector3(e.TelegraphTo.X, e.TelegraphTo.Y, 0f));
            }
        }

        /// <summary>创建一个不随本体缩放的子 TextMesh（运行时创建必须手动挂字体材质）。</summary>
        private TextMesh CreateWorldText(string text, float worldY, float parentScale,
            Color color, int fontSize, float charSize, int sortingOrder)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, worldY / parentScale, 0f);
            go.transform.localScale = Vector3.one / Mathf.Max(parentScale, 0.1f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = UiFonts.Text;
            mesh.fontSize = fontSize;
            mesh.characterSize = charSize;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var meshRenderer = mesh.GetComponent<MeshRenderer>();
            meshRenderer.material = mesh.font.material;
            meshRenderer.sortingOrder = sortingOrder;
            return mesh;
        }

        /// <summary>技能判定圈：以怪为圆心的细线圈，半径来自 EnemySimConfig（V 键开关）。</summary>
        private void CreateRangeRing(float radius, float parentScale)
        {
            var go = new GameObject("RangeRing");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one / Mathf.Max(parentScale, 0.1f);
            _range = go.AddComponent<LineRenderer>();
            _range.material = EnemyVisuals.LineMaterial;
            _range.useWorldSpace = false;
            _range.loop = true;
            _range.widthMultiplier = 0.035f;
            _range.sortingOrder = 3;
            _range.startColor = _range.endColor = new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.45f);
            _range.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                _range.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
        }
    }

    /// <summary>敌人表现层的共享贴图/材质缓存（运行时代码生成，无美术资源阶段）。</summary>
    public static class EnemyVisuals
    {
        private static readonly Dictionary<EnemyKind, Sprite> KindSprites =
            new Dictionary<EnemyKind, Sprite>();

        private static readonly Dictionary<Color, Sprite> CircleSprites =
            new Dictionary<Color, Sprite>();

        private static Material _lineMaterial;

        public static Material LineMaterial
        {
            get
            {
                if (_lineMaterial == null)
                    _lineMaterial = new Material(Shader.Find("Sprites/Default"));
                return _lineMaterial;
            }
        }

        public static Color KindColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Bug: return new Color(0.55f, 0.65f, 0.45f);
                case EnemyKind.NullPointer: return new Color(0.95f, 0.32f, 0.32f);
                case EnemyKind.Breakpoint: return new Color(1f, 0.6f, 0.2f);
                case EnemyKind.Exception: return new Color(0.72f, 0.45f, 0.92f);
                case EnemyKind.DeepCopy: return new Color(0.3f, 0.78f, 0.72f);
                case EnemyKind.Watchdog: return new Color(0.96f, 0.85f, 0.3f);
                case EnemyKind.Compiling: return new Color(0.5f, 0.8f, 0.55f);
                case EnemyKind.Overheat: return new Color(0.92f, 0.42f, 0.25f);
                case EnemyKind.Storm: return new Color(0.42f, 0.8f, 0.96f);
                case EnemyKind.Parent: return new Color(0.78f, 0.35f, 0.72f);
                case EnemyKind.Zombie: return new Color(0.62f, 0.62f, 0.65f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// 各怪技能判定半径（EnemyDesign.md 的 x 倍数换算结果），调平衡可视化用。
        /// 无范围技能的怪返回 0。
        /// </summary>
        public static float SkillRange(EnemyKind kind, EnemySimConfig cfg)
        {
            switch (kind)
            {
                case EnemyKind.Breakpoint: return cfg.BreakpointStopRange;   // 停止锁矢量的 x
                case EnemyKind.Exception: return cfg.ExceptionStopRange;     // 停止投掷的 2x
                case EnemyKind.Storm: return cfg.StormRingRange;             // 环绕的 3x
                case EnemyKind.Watchdog: return cfg.WatchdogBlastRadius;     // 自爆的 3x
                default: return 0f;
            }
        }

        /// <summary>异常子弹按异常类型着色（表现层彩蛋 §3），其它敌弹灰白。</summary>
        public static Color BulletColor(SimBullet bullet)
        {
            if (bullet.FromPlayer)
                return new Color(1f, 0.84f, 0.28f);
            switch (bullet.Label)
            {
                case "bad_alloc": return new Color(0.95f, 0.35f, 0.35f);
                case "out_of_range": return new Color(1f, 0.65f, 0.3f);
                case "poison_exception": return new Color(0.45f, 0.9f, 0.4f);
                case "bad_cast": return new Color(0.8f, 0.5f, 0.95f);
                default: return new Color(0.85f, 0.85f, 0.9f);
            }
        }

        public static Sprite KindSprite(EnemyKind kind)
        {
            if (!KindSprites.TryGetValue(kind, out Sprite sprite))
            {
                sprite = SpriteFactory.CreateCircle(48, KindColor(kind));
                KindSprites[kind] = sprite;
            }
            return sprite;
        }

        public static Sprite CircleSprite(Color color)
        {
            if (!CircleSprites.TryGetValue(color, out Sprite sprite))
            {
                sprite = SpriteFactory.CreateCircle(32, color);
                CircleSprites[color] = sprite;
            }
            return sprite;
        }
    }
}
