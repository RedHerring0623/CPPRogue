# Tests — 单元测试

测试脚本是**同一份源码、两个运行入口**：

| 入口 | 命令 | 说明 |
|---|---|---|
| Unity Test Runner（正式） | 编辑器里 Window → General → Test Runner → EditMode → Run All | 全部测试都跑，包括依赖 UnityEngine 的 |
| dotnet 快速验证（无需 Unity） | 仓库根目录 `dotnet test StandandaloneTests/CPPRogue.Core.StandaloneTests.csproj` | 秒级反馈；排除了 Physics 下用 Vector2 的两个文件 |

`StandaloneTests/` 里的 csproj 不复制源码，直接链接本项目的文件（单一事实来源），
在没装 Unity 编辑器的机器上也能验证逻辑层。

## 目录约定

| 路径 | 放什么 |
|---|---|
| `Assets/Scripts/Core/Code/Ast/` | 语法块与表达式的数据结构（Block / Expr / Value）——封闭集 |
| `Assets/Scripts/Core/Code/Runtime/` | 解释器、执行上下文、安全阀——唯一的执行者 |
| `Assets/Scripts/Core/Code/Builtins/` | 技能动词（attack/heal/shield…）——开放集，一个技能一个文件 |
| `Assets/Scripts/Core/Combat/` | 战斗端口（ICombatWorld）和变量黑板 |
| `Assets/Tests/Editor/` | 测试脚本，目录结构与上面镜像 |

命名规则：逻辑类 `Foo` 对应测试类 `FooTests`；`Tests/Editor/Fakes/` 放测试替身（FakeCombatWorld 等）。

## 逻辑层的边界规则（保住可测性）

逻辑层**可以**用 UnityEngine 里的纯数据/数学类型：`Vector2` / `Mathf` / `AnimationCurve` 等
（这类文件需要 Unity 编译，dotnet 快速验证会跳过它们，见 csproj 里的 Exclude）。

逻辑层**不要**用：

- `GameObject` / `MonoBehaviour`（表现层胶水，放 `Assets/Scripts/Game/`）
- `Time` / 帧驱动逻辑（逻辑用自己的 tick 模拟）
- 物理引擎（命中判定用 `CircleOverlap` 这类纯逻辑）
- `UnityEngine.Random`（逻辑内随机用 `System.Random`，从 ExecContext 注入，测试可复现）

## 新技能 / 新语法怎么加

- 新技能（如 `fireball()`）：`Builtins/` 加一个实现 `IBuiltin` 的类，`BuiltinTable.CreateDefault()` 里 Register 一行。参考 `BuiltinTableTests.Register_CustomFunction_IsCallableFromProgram`。
- 新语法（如 `switch`）：`BlockKind` 加一个值 + `Interpreter.ExecuteStatement` 一个分支。
- 新的世界动词（如召唤）：`ICombatWorld` 加方法，Fake 和 Unity 实现各补一份。
