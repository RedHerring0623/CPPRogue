# 表现层（Game）

> 对应代码：`Assets/Scripts/Game/`（asmdef：`CPPRogue.Game`，引用 `CPPRogue.Core`）
> 职责：Unity 侧的一切——把 Core 的逻辑执行结果变成看得见的东西。
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)，调度规则蓝本见 [DEMO_UI.md](DEMO_UI.md)。

---

## 1. 当前实现（2D 俯视角演示）

| 组件 | 职责 |
|---|---|
| `GameBootstrap` | 演示入口：`RuntimeInitializeOnLoadMethod` 在进 Play Mode 时先搭主菜单，选图后才搭局内世界（不改场景文件）。EventSystem 是会话级设施，永不随局拆除。正式场景就绪后移除，组件挂进场景 |
| `MainMenu` | **主菜单**（启动后第一屏）：启动进程 / 构建 / 词法树（禁用占位）/ 怪物图鉴 / 退出进程；持有局外 CodebaseState（跨局、死亡不清） |
| `MapSelectPanel` | 选择目标主机：当前只有"测试地图"卡片，点击即开局（`GameBootstrap.StartRun`） |
| `BuildPanel` | **构建页**：仓库语块列表 + 合成台 + 材料余额（模型在 Core/Codebase，见 [CODEBASE.md](CODEBASE.md)） |
| `PlayerController` | 主角 WASD/方向键走位（双手只负责移动，§2 核心循环），持 HP |
| `CameraFollow` | 正交相机平滑跟随目标（帧率无关阻尼） |
| `TickDriver` | 调度器（DemoUI 蓝本的正式版）：每 TickInterval 一个 tick，语句间 StatementInterval，下一 tick = max(固定间隔, 跑完时刻)；支持热替换 Routine |
| `RoutineHud` | **左上角源码逐行视图**：TickDriver 每步驱动高亮（黄=执行中 灰=优化掉 红=卡死），状态行显示 tick/周期 |
| `CombatWorldBridge` | `ICombatWorld` 实现：`attack()` → 从主角发射随机方向子弹；heal/shield → 数值与 HUD |
| `Bullet` | 直线飞行、超时消失；大小随伤害（视觉反馈）。命中判定待接 `CircleOverlap` |
| `PauseController` + `PauseMenu` | **ESC 暂停**：弹出主菜单（继续游戏 / 编辑 Routine / 怪物图鉴 / 返回主菜单）；编辑器或图鉴打开时再按 ESC = 关闭并继续（热替换 Routine，变量保留） |
| `ConfirmBox` | 返回主菜单的确认弹窗：「确认退回到主菜单吗？游戏内掉落不会保存！」确认 = 拆局回主菜单，取消/ESC = 回暂停菜单 |
| `EditorPanel` | 拼装编辑面板（拖拽插入/移动、右键删除、双击改参数——DemoUI 的 Unity 版），从主菜单进入 |
| `CodexPanel` | **怪物图鉴**：左列分组名单，右侧属性等级/掉落/描述；数据走 Core/Loot（见 [CODEX.md](CODEX.md)） |
| `DeathPanel` + `GameRun` | **死亡弹窗**：死因 + 存活/回收统计 + 重新开始/继续围观。`GameRun.Over` 置位后 ESC 失效、TickDriver 不再调度新 tick |
| `GamePalette` / `ParamDialog` | 可拖入的语法块清单 / 数值参数编辑弹窗 |
| `UiFonts` / `UiFactory` / `CodeColors` | 系统字体（Consolas + 微软雅黑）/ 控件工厂 / 源码配色 |
| `PlayerPropertySource` | `IPropertySource` 实现：`hp` 只读属性桥接 |

**怎么跑**：团结引擎打开工程，任意场景按 Play。
先停在**主菜单**：启动进程 → 选择目标主机 → 点"测试地图"卡片进入战斗。
预期画面：深色背景、青色圆圈主角（WASD 移动、视角锁定）、每 3 秒一个 tick——
先连发 3 颗随机方向子弹（每颗间隔 0.2s）再补 1 颗；**左上角源码视图黄色高亮逐句跳动**。
**按 ESC**：主菜单（继续 / 编辑 Routine / 怪物图鉴 / 返回主菜单），编辑器/图鉴内再按 ESC 直接继续；
**返回主菜单**：确认弹窗提示"游戏内掉落不会保存！"，确认后拆局回主菜单（局外数据保留）。
**死亡**：弹窗显示死因与统计——重新开始（全拆重建，拼装结果保留）或继续围观（世界恢复运转）。

## 2. 分层规则（与 Core 的边界）

- Game 层**可以**自由用 UnityEngine（GameObject/协程/Input/Random…）
- Game 层**不写游戏规则**：拼装校验、周期、语句语义全在 Core，Game 只做翻译
- 数据流向：`TickDriver` 拉 `Interpreter.Execute` 的步骤 → `CombatWorldBridge` 收到世界调用 → 生成子弹/特效
- 表现层的随机（子弹方向等）可以用 `UnityEngine.Random`；影响逻辑结果的随机必须走 `ExecContext.Rng`

## 3. 已知债务 / 下一步

- `GameBootstrap` 全代码搭建是演示手段——正式版做成场景 + Prefab；死亡重开 = TearDown 全拆重建，正式版应换成场景重载
- UI 无撤销（编辑误删只能重新拼）
- 字体走系统字体（Consolas/微软雅黑）——跨平台发布前要换成内置字体资源
