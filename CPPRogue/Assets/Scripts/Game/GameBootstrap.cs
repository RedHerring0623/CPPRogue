using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using CPPRogue.Core.Enemies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 演示入口：不改场景文件。进 Play 先停在主菜单（MainMenu），
    /// 「启动进程 → 选择地图」之后才搭建局内世界（BuildRun）。
    /// 死亡重开（DeathPanel → Restart）与主菜单开局（StartRun）都走 TearDown → BuildRun；
    /// Restart 保留拼装结果（RoutineEditor），StartRun 用全新演示程序。
    /// </summary>
    public static class GameBootstrap
    {
        // 动态创建的根对象（主菜单世界或局内世界）：切换时全拆。
        // 怪物/子弹/特效视图挂在 EnemyDirector 的 Views 子节点下，随 director 一起拆。
        private static readonly List<GameObject> Tracked = new List<GameObject>();
        private static RoutineEditor _carriedEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // 打包版启动即"无边框窗口填满屏幕"：窗口化不独占显示，Alt+Tab 友好
            if (!Application.isEditor)
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            _carriedEditor = null;

            // EventSystem 是会话级设施，不进 Tracked、永不随局拆除：
            // 曾把拆掉的 EventSystem 留在 FindObjectOfType 眼皮底下（当帧还能找到→跳过创建→帧末销毁），
            // 之后全场 uGUI 点击永久失效——"菜单能开（键盘）、按钮点了没反应"就是这个坑
            EnsureEventSystem();
            StyleCamera();

            MainMenu.Show();
        }

        /// <summary>主菜单"开始游戏"：拆掉菜单世界，搭建全新一局（演示程序）。</summary>
        public static void StartRun()
        {
            _carriedEditor = null;
            TearDown();
            BuildRun();
        }

        /// <summary>局内 ESC 菜单的"返回主菜单"：拆掉本局回主菜单（局外数据保留，本局所得不保存）。</summary>
        public static void QuitToMenu()
        {
            _carriedEditor = null;
            GameRun.Over = false;
            TearDown();
            MainMenu.Show();
        }

        /// <summary>死亡弹窗的"重新开始"：保留拼装结果，拆掉本局重建。</summary>
        public static void Restart(RoutineEditor carry)
        {
            _carriedEditor = carry;
            TearDown();
            BuildRun();
        }

        /// <summary>主菜单等世界把自己的根对象登记进来，与世界切换共用一套拆除。</summary>
        internal static void Track(GameObject go)
        {
            Tracked.Add(go);
        }

        private static void TearDown()
        {
            Time.timeScale = 1f;
            foreach (GameObject go in Tracked)
            {
                if (go != null)
                    Object.Destroy(go);
            }
            Tracked.Clear();

            // 复用的场景相机不拆，但镜头跟随是本局装的，要去掉
            Camera cam = Camera.main;
            if (cam != null)
            {
                CameraFollow follow = cam.GetComponent<CameraFollow>();
                if (follow != null)
                    Object.Destroy(follow);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private static void StyleCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void BuildRun()
        {
            GameRun.Over = false;
            EnsureEventSystem();

            // 相机：复用场景里的 Main Camera，改成 2D 正交；场景没有才新建（新建的要进拆除清单）
            Camera cam;
            if (Camera.main != null)
            {
                cam = Camera.main;
            }
            else
            {
                var camGo = new GameObject("Main Camera");
                Tracked.Add(camGo);
                cam = camGo.AddComponent<Camera>();
            }
            StyleCamera();

            // 主角：一个圆圈（WASD 走位）
            var playerGo = new GameObject("Player");
            Tracked.Add(playerGo);
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = SpriteFactory.CreateCircle(128, new Color(0.35f, 0.85f, 1f));
            playerRenderer.sortingOrder = 10;
            PlayerController player = playerGo.AddComponent<PlayerController>();

            // 视角锁定主角
            cam.gameObject.AddComponent<CameraFollow>().Target = playerGo.transform;

            // UI 根 + 事件系统（uGUI 拖拽/点击/输入框都靠它）
            var canvasGo = new GameObject("UICanvas");
            Tracked.Add(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // RoutineHud：左上角源码逐行显示 + 高亮
            RoutineHud hud = canvasGo.AddComponent<RoutineHud>();

            // 拼装编辑器是唯一事实来源：首局放演示程序，重开沿用玩家拼过的版本
            RoutineEditor editor = _carriedEditor ?? NewDemoEditor();
            Routine routine = editor.BuildRoutine();
            hud.Rebuild(routine);

            // 战斗世界：attack() → 瞄准最近敌人；heal() → EnemySim 血量
            CombatWorldBridge world = playerGo.AddComponent<CombatWorldBridge>();
            world.Player = player;
            world.Hud = hud;

            // 敌人世界：EnemySim 驱动 + 怪物测试面板（按钮与数字键 1-0 / G / C 等价）
            player.Speed = StatTable.Speed(3);   // EnemyDesign.md §0：玩家速度参照 spd3
            var directorGo = new GameObject("EnemyDirector");
            Tracked.Add(directorGo);
            EnemyDirector director = directorGo.AddComponent<EnemyDirector>();
            director.Setup(player, hud, editor);
            world.Director = director;
            canvasGo.AddComponent<EnemyTestPanel>().Setup(director);

            // TickDriver + ESC 暂停菜单（编辑 Routine / 怪物图鉴从这里进）
            TickDriver driver = playerGo.AddComponent<TickDriver>();
            driver.TickInterval = 3f;
            driver.StatementInterval = 0.2f;
            driver.Setup(routine, world, hud, player);

            PauseController pause = canvasGo.AddComponent<PauseController>();
            pause.Setup(editor, driver, hud);
        }

        /// <summary>首局的演示程序：for (i = 0; i &lt; 3; i += 1) attack(); attack();</summary>
        private static RoutineEditor NewDemoEditor()
        {
            var editor = new RoutineEditor();
            foreach (Block b in DemoProgram())
                editor.Insert(new Slot(null, 0, editor.Root.Length), b);
            return editor;
        }

        private static Block[] DemoProgram()
        {
            return new[]
            {
                Block.For("i", Expr.Num(0),
                    Expr.Bin(BinaryOp.Lt, Expr.Var("i"), Expr.Num(3)), Expr.Num(1),
                    Block.Call("attack")),
                Block.Call("attack"),
            };
        }
    }
}
