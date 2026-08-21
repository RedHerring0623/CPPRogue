# CRogue 开发文档 · 总览

> 更新日期：2026-08-21
> 策划设计稿见仓库根目录 `IDEA.md`，本目录只讲实现。
> **文档按功能块拆分**，本文件是索引 + 全局约定（技术栈、运行方式、命名、边界规则、路线图）。

---

## 文档索引

| 文档 | 功能块 | 对应代码 |
|---|---|---|
| [CODE_EXECUTION.md](CODE_EXECUTION.md) | 代码执行系统：AST / 解释器 / 词汇表 / Routine | `Assets/Scripts/Core/Code/` |
| [COMBAT.md](COMBAT.md) | 战斗端口与变量黑板 | `Assets/Scripts/Core/Combat/` |
| [COMPUTING.md](COMPUTING.md) | CPU 周期资源 | `Assets/Scripts/Core/Computing/` |
| [PHYSICS.md](PHYSICS.md) | 碰撞判定 | `Assets/Scripts/Core/Physics/` |
| [DEMO_UI.md](DEMO_UI.md) | 执行可视化 Demo（开发期演示，非 Unity） | `DemoUI/` |
| [PRESENTATION.md](PRESENTATION.md) | Unity 表现层（2D 俯视角视图/TickDriver） | `Assets/Scripts/Game/` |

**规则**：新功能块（Fragment 掉落、Affix 词缀、怪物三档、表现层 TickDriver…）落地时，
在 `doc/` 新建对应文档并登记到这张表；各模块的语义决策写在自己文档里，全局约定只写在这份总览。

---

## 1. 项目与技术栈

**一句话概念**：战利品是 C++ 语法块（`if`/`for`/`while`…），玩家把它拼成一个函数（Routine），
副本里每隔 TickInterval 秒自动执行一次——代码就是 Build。

| 项 | 值 |
|---|---|
| 引擎 | Unity 2022.3（**团结引擎** `2022.3.62t13`，Tuanjie 1.10.1） |
| 语言 | C# 9（Unity 2022.3 的语法上限，逻辑层严格遵守） |
| 测试框架 | NUnit 3（Unity Test Framework 1.1.33 内置） |
| 快速验证 | .NET SDK 10 + `dotnet test`（不需要 Unity 编辑器） |

⚠️ **编辑器版本**：本项目由团结引擎创建（`ProjectVersion.txt` 为 `2022.3.62t13`，且依赖
`cn.tuanjie.*` 专有包），必须安装团结引擎对应版本，国际版 Unity 无法打开。

---

## 2. 仓库结构与运行方式

```
CPPRogue/（仓库根）
├─ IDEA.md                       策划设计稿
├─ doc/                          开发文档（按功能块拆分）
├─ StandaloneTests/              dotnet 快速验证工程（链接源码，不复制）
├─ DemoUI/                       执行可视化 Demo（WinForms，dotnet run 直接跑）
└─ CPPRogue/                     Unity 工程根
   ├─ Assets/
   │  ├─ Scripts/Core/           逻辑层（asmdef: CPPRogue.Core）
   │  └─ Tests/                  测试 + 说明（asmdef: CPPRogue.Core.Tests）
   ├─ Packages/manifest.json     已含 com.unity.test-framework 1.1.33
   └─ ProjectSettings/
```

**测试有两个入口，跑的是同一份脚本**：

| 入口 | 命令 | 用途 |
|---|---|---|
| Unity Test Runner | 编辑器 Window → General → Test Runner → EditMode → Run All | 正式入口，全部测试 |
| dotnet 快速验证 | 仓库根目录 `dotnet test StandaloneTests/CPPRogue.Core.StandaloneTests.csproj` | 无 Unity 机器上的秒级反馈 |

`StandaloneTests/` 的 csproj 通过 `<Compile Include>` 直接链接 Unity 项目里的源文件（单一事实来源）。
它排除了 `Physics/`（用了 `UnityEngine.Vector2`，本机无引擎 DLL），所以 dotnet 跑 69 个，
Unity Test Runner 会多跑 6 个（CircleOverlap 的测试）。

**执行可视化 Demo**：`dotnet run --project DemoUI`——真解释器驱动，语句高亮/优化掉灰显/
Hung 红显/单步/快进/间隔可调，用来看"代码执行"的观感。详见 [DEMO_UI.md](DEMO_UI.md)。

---

## 3. 全局架构

```
Routine（玩家拼的函数）
   │  每 TickInterval 执行一次
   ▼
Interpreter（唯一的执行者 + 全部安全阀）
   │  遇到 Call 语句 → 查 BuiltinTable
   ▼
IBuiltin（attack / heal / shield …，开放集）
   │  副作用只走端口
   ▼
ICombatWorld ←── FakeCombatWorld（单测）/ Unity 实现（表现层）
```

核心设计决策：**语法形状、技能词汇、词缀修饰三条轴正交**——加新技能不碰解释器，
加新语法不碰技能。各层细节见 [CODE_EXECUTION.md](CODE_EXECUTION.md)。

---

## 4. 命名约定（一词一义）

代码、UI 文案、文档、提交信息统一遵守：

| 中文概念 | 英文 | 备注 |
|---|---|---|
| tick（一次执行） | `Tick` | `RunTick()`、`ctx.Tick` |
| tick 间隔 | `TickInterval` | 秒数；不叫 `TickRate`（业界指每秒次数，语义相反） |
| 超频 | `Overclock` | 缩短 TickInterval 的词缀/机制 |
| 语法块（代码形态） | `Block` | AST 语句节点 |
| 语法块（物品形态） | `Fragment` | 掉落/背包条目，呼应"回收源代码片段"；掉落系统落地时建类 |
| 拼装的函数 | `Routine` | 玩家/怪物/Boss 的程序统一叫 Routine |
| 局外仓库 | `Codebase` | 仓库系统落地时建类 |
| CPU 周期 | `Cycle` | `CpuBudget`、`CpuCost` |

**禁用词**：`Cycle` 表示时间（专指 CPU 周期）、`Function` 表示玩家程序（已被 `IBuiltin` 占用）、
`Script`（Unity 里指 MonoBehaviour）、`Token`（词法/代币歧义）、`Statement`（与 Block 重复）。
口语可用 `Build`，不进类名。

---

## 5. 逻辑层边界规则（保住可测性）

`Assets/Scripts/Core/` 下**可以**用 UnityEngine 的纯数据/数学类型：`Vector2` / `Mathf` /
`AnimationCurve` / `Color`（这类文件只能在 Unity 编译，dotnet 验证会跳过）。

**禁止**进逻辑层：

- `GameObject` / `MonoBehaviour` —— 表现层胶水，将来放 `Assets/Scripts/Game/`
- `Time` / 帧驱动逻辑 —— 逻辑用自己的 tick 模拟
- 物理引擎 / Collider 回调 —— 命中判定用 `CircleOverlap` 这类纯逻辑
- `UnityEngine.Random` —— 用注入的 `System.Random`

语言限制：C# 9、不引 `System.*` 以外的命名空间（netstandard2.1 兼容），保证拷进 Unity 零修改。

---

## 6. 扩展指南（想要 X 改哪里）

| 需求 | 改动范围 | 详细文档 |
|---|---|---|
| 新技能（`fireball()`） | `Builtins/` 加一个类 + 注册一行 | CODE_EXECUTION |
| 新语法（`switch`） | `BlockKind` 加值 + 解释器一个分支 | CODE_EXECUTION |
| 新世界动词（召唤等） | `ICombatWorld` 加方法 + 两份实现 | COMBAT |
| 调整周期定价 | 各 builtin 的 `CpuCost` / 解释器结构语句定价 | COMPUTING |
| 新词缀（将来） | `Affixes/` 装饰器，编译期烘焙 | （落地时建文档） |
| 怪物 AI | 拼 Routine + 注册 `mob.*` builtin | （落地时建文档） |

---

## 7. 当前状态与路线图

**已实现**：AST 三件套、解释器全部基础语句、表达式求值、变量黑板、CPU 周期系统、
安全阀（Hung / 优化掉 / 未定义引用）、三个 builtin、Routine 行数上限、圆形碰撞、
**步骤机**（Execute 迭代器，UI 逐句驱动 + 懒执行）、**SourcePrinter** 源码排版、
**RoutineEditor 拼装编辑器**（插入/移动/删除 + 防呆）、
**执行可视化 Demo**（DemoUI，含拖拽拼装）、
**Unity 表现层**（2D 俯视角 + TickDriver + attack 发射子弹，团结引擎批处理编译验证通过）。
测试：69 个全绿（dotnet）+ 6 个仅 Unity 侧（Physics，待编辑器 Test Runner 首跑）。

**未实现**（按建议顺序）：

1. 变量作用域（块级作用域，`static`/`const` 语义的地基）→ CODE_EXECUTION
2. 词缀系统（装饰器 + 编译期烘焙）→ 新文档 AFFIX.md
3. `Fragment` + 掉落表 → 新文档 FRAGMENT.md
4. 局内模拟器（固定 tick 循环 + 战斗状态机，灰盒 demo 核心）
5. Unity 表现层（`TickDriver` + `ICombatWorld` 真实现）→ 新文档 PRESENTATION.md
6. Routine 的 JSON 序列化（存档 / 仓库 / Boss 屏显）→ CODE_EXECUTION
7. 危险结构：`try/catch`、`new`/`delete`、递归栈深 → CODE_EXECUTION
8. 怪物三档体系（普通/精英词缀/Boss 可读源码）→ 新文档 MONSTER.md

---

## 8. 已知注意事项

- 当前开发机未装 Unity 编辑器：**Core 代码从未在 Unity 里编译过**，
  dotnet 全绿但最终以团结引擎 Test Runner 为准。
- `dotnet new sln` 在 .NET 10 下生成 `.slnx` 新格式；本仓库不需要解决方案文件，
  直接 `dotnet test <csproj>`。
- Unity 侧首次打开会为所有新文件生成 `.meta`，属正常现象，一并提交。
