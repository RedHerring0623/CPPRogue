# 战斗系统（Combat）

> 对应代码：`Assets/Scripts/Core/Combat/`
> 职责：给代码执行系统提供副作用出口（世界端口）和状态载体（变量黑板）。
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. ICombatWorld —— 唯一的副作用出口

builtin 只报"想做什么"（`Attack(dmg, radius)` / `Heal(amount)` / `Shield(amount, ticks)`），
不关心谁来实现。这一层端口换来三份复用：

| 实现 | 用途 |
|---|---|
| `FakeCombatWorld`（Tests/Editor/Fakes/） | 单元测试：记录调用序列，断言"代码执行 → 世界收到了什么" |
| Unity 实现（未实现） | 表现层：驱动真正的怪、特效、音效 |
| 模拟器实现（未实现） | 局内模拟：纯数值战斗结算，灰盒 demo 用 |

**扩展方式**：新动词（召唤、闪避、标记敌人）= 接口加一个方法 + Fake 和 Unity 各补一份实现。
方法按动词语义命名（`Summon`），不按实现命名（`SpawnPrefabAndPlayVFX`）。

---

## 2. Blackboard —— 变量黑板

玩家 Routine 里的变量（`a = 值;`）存在这里。

| 规则 | 说明 |
|---|---|
| 跨 tick 持久 | §3.1：变量在 tick 之间保存，死亡才由上层调用 `Clear()` |
| 槽位上限 | 构造时传 `maxSlots`（§4：初始 2 个）；满了**拒绝新变量**（`TrySet` 返回 false），已有变量可覆盖 |
| 循环变量同黑板 | `for` 的 i 也是黑板变量，出循环仍可读；块级作用域未实现（见 CODE_EXECUTION.md 规划） |

`maxSlots` 默认 0 = 不限，由上层按角色配置传入——逻辑层不写死数值。

---

## 3. IPropertySource —— 系统只读量

`if (hp < 30%)` 里的 `hp` 这类系统量从 `ExecContext.Properties` 读，
不占玩家变量槽。变量求值顺序：黑板 → 属性源 → 未定义按 0。

Unity 侧将来的实现会桥接真实角色状态（hp 百分比、护盾量、tick 数等）。

---

## 4. 测试

| 测试文件 | 覆盖 |
|---|---|
| `Tests/Editor/Combat/BlackboardTests.cs` | 读写、覆盖、槽位上限、Clear |
| `Fakes/FakeCombatWorld.cs` / `Fakes/FakePropertySource.cs` | 测试替身（被 InterpreterTests 使用） |
