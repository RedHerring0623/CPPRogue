using CPPRogue.Core.Enemies;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// 撤离点（编译出口，LootDesign.md §3"撤离是唯一入库通道"）：
    /// 测试地图常态存在、随时可撤；正式地图的出现条件后续再加。
    /// 玩家走近 → 2s 读条（g++ compiling...，离开会回退）→ 撤离结算入库、回主菜单。
    /// 撤离点存在期间，主角外环上常驻一个绿色箭头指向它。
    /// </summary>
    public sealed class ExtractionPoint : MonoBehaviour
    {
        private const float TriggerRadius = 2.2f;
        private const float ChannelSeconds = 2f;

        /// <summary>撤离点位置（世界坐标）。测试地图放在出生点右侧远处，逼玩家跑图。</summary>
        public Vec2 Location = new Vec2(15f, 2f);

        private PlayerController _player;
        private EnemyDirector _director;
        private Transform _arrow;
        private Transform _fill;
        private float _progress;
        private bool _done;

        public void Setup(PlayerController player, EnemyDirector director)
        {
            _player = player;
            _director = director;
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            var pad = new GameObject("ExtractionPad");
            pad.transform.SetParent(transform, false);
            pad.transform.position = new Vector3(Location.X, Location.Y, 0f);
            var padRenderer = pad.AddComponent<SpriteRenderer>();
            padRenderer.sprite = SpriteFactory.CreateCircle(128, new Color(0.3f, 0.9f, 0.5f, 0.3f));
            padRenderer.sortingOrder = 0;
            pad.transform.localScale = new Vector3(TriggerRadius * 2f, TriggerRadius * 2f, 1f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(pad.transform, false);
            var fillRenderer = fillGo.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = SpriteFactory.CreateCircle(64, new Color(0.45f, 1f, 0.65f, 0.6f));
            fillRenderer.sortingOrder = 1;
            fillGo.transform.localScale = Vector3.zero;
            _fill = fillGo.transform;

            var arrowGo = new GameObject("ExtractionArrow");
            arrowGo.transform.SetParent(transform, false);
            var arrowRenderer = arrowGo.AddComponent<SpriteRenderer>();
            arrowRenderer.sprite = SpriteFactory.CreateTriangle(64, new Color(0.45f, 1f, 0.65f, 0.9f));
            arrowRenderer.sortingOrder = 8;
            _arrow = arrowGo.transform;
        }

        private void Update()
        {
            if (_done || _player == null || GameRun.Over)
                return;

            Vector3 playerPos = _player.transform.position;
            Vector3 target = new Vector3(Location.X, Location.Y, 0f);
            Vector3 toTarget = target - playerPos;
            float dist = toTarget.magnitude;

            // 指引箭头：主角外环（1.5 个世界单位）上指向撤离点
            Vector3 dir = dist > 0.001f ? toTarget / dist : Vector3.right;
            _arrow.position = playerPos + dir * 1.5f;
            _arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

            if (dist <= TriggerRadius)
            {
                _progress += Time.deltaTime / ChannelSeconds;
                if (_progress >= 1f)
                {
                    Extract();
                    return;
                }
            }
            else
            {
                _progress = Mathf.Max(0f, _progress - Time.deltaTime * 2f);   // 离开快速回退
            }

            float fillScale = Mathf.Clamp01(_progress) * 1.6f;
            _fill.localScale = new Vector3(fillScale, fillScale, 1f);
        }

        private void Extract()
        {
            _done = true;
            _arrow.gameObject.SetActive(false);

            // 入库 + 落盘，再弹结算
            _director.BankBag();
            Time.timeScale = 0f;
            GameRun.Over = true;   // 冻结 ESC 与代码调度，撤离结算接管
            if (_director.Hud != null)
                _director.Hud.Log("g++ compiling... 撤离成功，本局所得已入库");
            ExtractionPanel.Create(_director.Hud != null ? _director.Hud.transform : transform,
                _director.Bag,
                () =>
                {
                    Time.timeScale = 1f;
                    GameBootstrap.QuitToMenu();
                });
        }
    }
}
