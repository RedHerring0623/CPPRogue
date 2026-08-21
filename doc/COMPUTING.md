# 计算资源（Computing）

> 对应代码：`Assets/Scripts/Core/Computing/`
> 职责：CPU 周期（Cycle）的记账。这是 IDEA.md §4 的核心平衡阀门：
> **任何超模组合最终都卡在"周期不够"**，数值可控全靠它。
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. CpuBudget

| 成员/方法 | 语义 |
|---|---|
| `Capacity` | 本 tick 的周期总量（角色/装备决定） |
| `Remaining` / `IsExhausted` | 剩余量 / 是否耗尽 |
| `TrySpend(cycles)` | 尝试消耗；**不足时返回 false 且不部分扣除**——对应"语句被优化掉，周期留给后面的语句" |
| `Reset()` | 恢复满额；`Interpreter.RunTick` 开头自动调用（预算按 tick 结算） |
| 负数参数 | 构造负容量 / 负消耗都抛 `ArgumentOutOfRangeException` |
| 零消耗 | `TrySpend(0)` 在零预算下也成功（零消耗语句永不被优化掉） |

## 2. 周期定价原则

| 语句 | 周期 | 定价逻辑 |
|---|---|---|
| `attack()` | 2 | 基准输出 |
| `heal(n)` | 3 | 保命比输出贵 |
| `shield(n)` | 2 | 防御与输出同价 |
| Assign / If / Return / Break / Continue | 每条 1 | 结构语句便宜但不是免费 |
| For / While | 每次迭代额外 1 | 循环越猛开销越大，天然抑制无脑嵌套 |

`ExecContext` 默认预算 32/tick，实际由上层（角色成长/装备词缀）配置。

调平衡的顺序：先动定价表（builtin 的 `CpuCost`），再动预算——
定价是"这张牌多贵"，预算是"你多有钱"，混着调会乱。

## 3. 测试

`Tests/Editor/Computing/CpuBudgetTests.cs`：记账、不足不扣、清零边界、Reset、负数异常。
