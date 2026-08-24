# 敌人系统（Enemies）

> 对应代码：`Assets/Scripts/Core/Enemies/`（逻辑层）+ `Assets/Scripts/Game/Enemies/`（表现层）
> 设计文档：[EnemyDesign.md](../EnemyDesign.md)（属性等级、行为规格、命名的唯一事实来源）
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 分层

| 层 | 目录 | 职责 | 依赖 |
|---|---|---|---|
| Core | `Core/Enemies/` | 确定性模拟：怪物状态机、子弹、伤害结算、事件 | 纯 C#，不碰 UnityEngine（自带 `Vec2`），`dotnet test` 可跑 |
| Game | `Game/Enemies/` | 视图同步、输入验收台、特效/日志 | MonoBehaviour，读 sim 状态渲染 |

原则与 Code 执行系统一致：**逻辑层不知道 Unity 存在**。
表现层每帧 `SetPlayerPosition → Step(dt)`，然后把 `Enemies/Bullets/Events` 同步成 GameObject；
反过来 `attack()/heal()` 从 CombatWorldBridge 委托进 sim。双向只有一个针眼（EnemyDirector）。

## 2. Core：EnemySim

`EnemySim` 持有全部可变状态（怪物、敌我子弹、玩家血量），随机只走注入的 `System.Random`（种子可复现）。

`Step(dt)` 顺序：计时 → Brain.Tick → 子弹飞行与命中 → 接触结算 → 清尸。

伤害模型（EnemyDesign.md §0，测试锁定）：

| 规则 | 实现 |
|---|---|
| 接触单次结算 | 碰撞瞬间扣一次 atk |
| 玩家全局无敌帧 0.1s | `PlayerInvuln`，**唯一**的受击间隔，挡住所有来源（含敌弹）；防重合磨血交给击退 |
| 无敌帧中穿过的敌弹 | 标记 `PassedThroughPlayer` 作废，不许帧结束补刀 |
| 碰撞击退 | 结算后玩家获得远离该敌人的初速（线性衰减）；sim 管方向与衰减，位移由表现层应用 |

死亡处理集中在一个 `Kill()`：深拷贝分裂（世代 0→1→2）、父进程死亡 reap 全场僵尸、
看门狗自爆（`Explode` 走无敌帧规则）都在这里，Brain 不自己碰生死。

## 3. Brain：每怪一状态机

`IEnemyBrain.Tick(self, sim, dt)`，Brain 实例自身字段就是状态（断点的相位、过热的快慢段）。
无状态的可共享单例（`ChaserBrain.Instance`）。

| 怪 | Brain | 状态机要点 |
|---|---|---|
| Bug / 空指针 / 僵尸 | `ChaserBrain` | 无状态直线追击 |
| 断点 | `BreakpointBrain` | 追击→2x 锁矢量(预兆线,1s)→冲 4x→恢复→冷却 |
| 异常 | `RangedBrain` | 3x 停 / x 内退，2s 一发不预判 |
| 风暴 | `StormBrain` | 3x 环切向游走，0.5s 一发 |
| 过热 | `OverheatBrain` | spd4×3s → spd1×2s 循环 |
| 看门狗 | `WatchdogBrain` | 引信倒计时，归零调 `sim.Explode` |
| 编译中 | `CompilingBrain` | 编译态(易伤×2)→读完条换成运行态数值并转追击 |
| 父进程 | `SpawnerBrain` | 随机游走 + 按节拍调 `sim.TrySpawnChild`（上限/补位/reap 在 sim） |

## 4. 数值：两张表

- `StatTable`：lv1-5 → hp/atk/spd 具体数值的唯一映射。玩家速度 = `Speed(3)`（§0 锚点）。
- `EnemySimConfig`：全部时序/距离参数（锁矢量时长、引信、读条时间、x 的倍率……），
  构造时把"x 的倍数"换算成绝对值。**调平衡只动这两个文件**，Brain 里没有裸数字。

`EnemyArchetypes.Create()` 是"设计文档 → 实例"的唯一入口：等级属性查 StatTable、
装配 Brain、深拷贝世代表也在里面。

## 5. 事件

`SimEvent`（Spawned/Died/Exploded/Compiled/ChildrenReaped/PlayerHit），`DrainEvents()` 消费。
表现层拿它做：空指针死亡飘 `Segmentation fault (core dumped)`、看门狗爆炸圈、编译完成提示、
父进程 reap 日志。逻辑层发完不留恋，测试也能断言。

## 6. 测试与验收

| 入口 | 覆盖 |
|---|---|
| `dotnet test`（StandaloneTests/，无引擎依赖） | `Tests/Editor/Enemies/`：碰撞模型、分裂世代、自爆、孵化/reap、子弹无敌帧、断点锁矢量、远程定距、风暴环、过热循环、编译易伤，共 89 例（含既有用例） |
| Unity Test Runner | 同一份脚本（Editor 平台） |

**Demo 验收台**（Play Mode 或打包 exe）：数字键 `1-0` 逐只刷新十种怪，
`G` 切自动出怪，`C` 清场；`attack()` 已改为射向最近敌人。验收断点时盯着红色预兆线侧移即可骗冲。
