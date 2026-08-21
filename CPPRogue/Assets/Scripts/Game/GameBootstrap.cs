using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Ast;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 演示入口：不改场景文件，进入 Play Mode 时自动搭建 2D 俯视角视图 + HUD + 暂停编辑。
    /// 正式场景搭建好后可以移除这个类，把组件挂进场景。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            // 相机：复用场景里的 Main Camera，改成 2D 正交
            Camera cam = Camera.main != null ? Camera.main : new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 主角：一个圆圈（WASD 走位）
            var playerGo = new GameObject("Player");
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = SpriteFactory.CreateCircle(128, new Color(0.35f, 0.85f, 1f));
            playerRenderer.sortingOrder = 10;
            PlayerController player = playerGo.AddComponent<PlayerController>();

            // 视角锁定主角
            cam.gameObject.AddComponent<CameraFollow>().Target = playerGo.transform;

            // UI 根 + 事件系统（uGUI 拖拽/点击/输入框都靠它）
            var canvasGo = new GameObject("UICanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // RoutineHud：左上角源码逐行显示 + 高亮
            RoutineHud hud = canvasGo.AddComponent<RoutineHud>();

            // 拼装编辑器是唯一事实来源：演示程序先放进去
            var editor = new RoutineEditor();
            foreach (Block b in DemoProgram())
                editor.Insert(new Slot(null, 0, editor.Root.Length), b);
            Routine routine = editor.BuildRoutine();
            hud.Rebuild(routine);

            // 战斗世界：attack() → 发射随机方向子弹
            CombatWorldBridge world = playerGo.AddComponent<CombatWorldBridge>();
            world.Player = player;
            world.Hud = hud;

            // TickDriver + ESC 暂停编辑
            TickDriver driver = playerGo.AddComponent<TickDriver>();
            driver.TickInterval = 3f;
            driver.StatementInterval = 0.2f;
            driver.Setup(routine, world, hud, player);

            PauseController pause = canvasGo.AddComponent<PauseController>();
            pause.Setup(editor, driver, hud);
        }

        /// <summary>演示程序：for (i = 0; i &lt; 3; i += 1) attack(); attack();</summary>
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
