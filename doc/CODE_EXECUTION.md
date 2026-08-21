# 代码执行系统（Code）

> 对应代码：`Assets/Scripts/Core/Code/`（asmdef：`CPPRogue.Core`）
> 职责：把玩家拼装的 Routine 每 tick 安全地解释执行，副作用只通过 ICombatWorld 出去。
> 全局约定（命名/边界/运行方式）见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 设计：三条正交的轴

| 轴 | 位置 | 集合性质 | 增长方式 |
|---|---|---|---|
| 语法形状 | `Ast/`（Block / Expr / Value） | 封闭集，很少长 | 加 `BlockKind` + 解释器分支 |
| 技能词汇 | `Builtins/` + `Library.cs` | 开放集，天天加 | 加一个 `IBuiltin` 类 + 注册一行 |
| 词缀修饰 | （未实现，规划 `Affixes/`） | 开放集 | 装饰器 + 编译期烘焙 |

**加新技能不碰解释器，加新语法不碰技能**——两条成长轴解耦是本系统的核心原则。

---

## 2. 语法层（Ast/）

三个纯数据类型，**自己不执行任何逻辑**：

### Block（语句）

`BlockKind`：`Call` / `Assign` / `If` / `For` / `While` / `Return` / `Break` / `Continue`。

- `For` 存为 `for (LoopVar = Init; Condition; LoopVar += Step) { Body }`，三个位置全是 `Expr`
- `While` 的 `Condition` 为 null 时视为 `while(true)`

关键决策：**`Call` 存函数名字符串，不存对象引用**。AST 因此可以直接 JSON 序列化
（仓库存档、Boss 屏显源码都靠它），函数在执行期由 `BuiltinTable` 按名解析。

### Expr（表达式）

`Literal` / `Var` / `Binary` / `Unary` 四种节点。

**所有参数位都是 `Expr`**——这是参数化扩展点：`attack(n)` 的 n、`for(i=0; i<x; i++)` 的
初值/条件/步长，字面量、变量、运算式通吃。

变量求值顺序（`Interpreter.Eval`）：玩家黑板 → 系统属性（`IPropertySource`，如 hp）→ 未定义按 0。

### Value（值）

数字与布尔共用一个 double 载体（`ValueKind` 区分），`AsBool()` 按"非零为真"。
为将来的扩展（目标引用、字符串）预留 Kind 枚举。

---

## 3. 执行层（Runtime/）

### Interpreter —— 唯一的执行者

所有安全阀集中在 `Interpreter.cs` 一个文件，不会散落到语句类里：

| 安全阀 | 规则 |
|---|---|
| CPU 周期 | 每条语句执行前扣费；不够 = 整条跳过（"被优化掉"），计入 `ctx.SkippedByBudget`，不部分扣费 |
| 循环上限 | 单循环迭代超过 `MaxLoopIterations`（默认 1000）→ 本 tick 返回 `ExecResult.Hung` |
| 语句数上限 | 单 tick 语句总数超过 `MaxStatementsPerTick`（默认 10000）→ `Hung` |
| 未知函数 | 抛 `UndefinedReferenceException`（`undefined reference to ...`） |

`RunTick(Routine, ExecContext)` 开头自动重置周期预算和统计计数。

**调用语句的执行顺序**：按名解析 builtin → 扣周期 → 求值参数 → `Invoke`。
顺序有意义：解析失败先于扣费（未定义引用不花钱），周期不足时参数不会被求值。

**控制流传播**：`return` / `break` / `continue` 作为 `Flow` 信号向上冒泡，
由对应的层级（函数体 / 最近循环）消费；`break` 被 For/While 捕获，`return` 穿透一切。

### ExecContext —— 执行环境

| 成员 | 说明 |
|---|---|
| `World`（ICombatWorld） | 唯一副作用出口，必填 |
| `Functions`（BuiltinTable） | 词汇表，默认 `CreateDefault()` |
| `Budget`（CpuBudget） | 周期预算，默认 32/tick |
| `Vars`（Blackboard） | 玩家变量黑板 |
| `Rng`（System.Random） | 注入随机源，种子可控 |
| `Properties`（IPropertySource） | hp/tick 等系统只读量 |
| `Limits`（InterpreterLimits） | 安全阀参数 |
| `Tick` | 当前 tick 序号，由游戏循环驱动（解释器不自增） |
| `SkippedByBudget` / `StatementsExecuted` | 本 tick 统计（UI 反馈/安全阀） |

### Routine —— 拼装的函数

`Lines`（Block[]）+ `MaxLines`（默认 8，§4 行数上限；超出抛异常，装备可扩展）。
玩家、怪物、Boss 的程序统一是 Routine。

---

## 4. 词汇层（Builtins/ + Library.cs）

### IBuiltin 接口

```
Name        函数名 = 存档/掉落表的引用 ID，发布后保持稳定
CpuCost     每次调用消耗的周期
ParamCount  最大参数个数（拼装 UI 防呆用）
Invoke(ctx, args)  执行；不持有状态，副作用只走 ctx.World
```

### BuiltinTable（词汇表）

名字 → IBuiltin 的注册表，`CreateDefault()` 注册开局词汇。
**重名注册直接抛异常**——词汇表里一个名字只能有一个意思。

### 已实现

| 函数 | 周期 | 语义 |
|---|---|---|
| `attack()` | 2 | 默认 10 伤害、半径 1.5，攻击最近敌人 |
| `attack(n)` | 2 | n 为伤害值（n 可为变量/表达式） |
| `heal(n)` | 3 | 回复 n 点生命（比 attack 贵——保命要有代价） |
| `shield(n)` | 2 | 获得 n 点护盾，持续 1 tick |

---

## 5. 已定语义决策（改之前想清楚）

| 决策 | 内容 | 理由 |
|---|---|---|
| 未定义变量 | 求值为 0，不报错 | C++"未初始化"的老传统，做梗 |
| 除零 / 模零 | 结果按 0 | UB 梗，且避免解释器炸 |
| `&&` / `\|\|` | 短路求值 | 语义正确 |
| `while` 条件为 null | 视为 `while(true)` | 便于构造死循环玩法 |
| 循环变量 | 与玩家变量同黑板，出循环仍可读，**暂无作用域** | 已知简化，作用域在路线图 |
| 优化掉 | 整条跳过、不部分扣费，tick 仍算 Completed | 被优化的是语句不是 tick |

---

## 6. 测试

| 测试文件 | 覆盖 |
|---|---|
| `Tests/Editor/Code/Runtime/InterpreterTests.cs` | 全语句类型、参数化（变量/表达式参数、变量上限/步长的循环）、Hung、优化掉、return |
| `Tests/Editor/Code/BuiltinTableTests.cs` | 词汇表注册、重名、自定义技能可调用 |
| `Tests/Editor/Code/RoutineTests.cs` | 行数上限 |
| `Tests/Editor/Fakes/FakeCombatWorld.cs` | 测试替身（记录世界调用） |

跑法见 [OVERVIEW.md §2](OVERVIEW.md)。

---

## 7. 未实现 / 规划

- 变量作用域（块级作用域；`static` / `const` 语义的地基）
- 词缀系统（`Affixes/` 装饰器 + 编译期烘焙，见 IDEA.md §5）
- `try/catch`、`new`/`delete`（内存泄漏 debuff）、递归与栈深（IDEA.md §3.2）
- `switch` 语句
- Routine 的 JSON 序列化（存档 / 仓库 / Boss 屏显）
- 函数返回值（目前 builtin 无返回值，表达式层无函数调用）
