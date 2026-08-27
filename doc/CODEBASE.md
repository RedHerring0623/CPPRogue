# 局外代码库（Codebase）

> 对应代码：`Assets/Scripts/Core/Codebase/`（模型，asmdef `CPPRogue.Core`）+ `Assets/Scripts/Game/BuildPanel.cs`（构建页 UI）
> 设计稿：[LootDesign.md](../LootDesign.md) §3（搜打撤局内/局外管理与块合成规则）
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 模型（Core/Codebase/）

| 文件 | 职责 |
|---|---|
| `FragmentCatalog.cs` | 参数化语块阶梯：家族（attack/heal/shield）、档位上限（MaxTier=5）、ID 生成与解析（`attack(2)`/`attack()` → `FragmentDef`，阶梯外返回 null） |
| `CodebaseState.cs` | 局外状态：仓库语块计数 + 三种材料余额 + 合成规则（`CanMerge`/`Merge`：2×tier → tier+1，2×顶档 → 自由形参）+ `SeedDemo()` 演示数据 |

关键语义：

- **ID 即档位**：`attack(1)` 的数字就是档位（同时也是参数字面量，烧死在块里）；`attack()` 形参留空，
  拼装时可绑定任意变量——阶梯顶点的严格上位（LootDesign.md §3）。
- **自由形参不可再合成**（Tier=0 不参与 Merge）。
- `CodebaseState` 是会话级内存数据：由 Game 层 `MainMenu` 持有为静态，**局内死亡/重开不清**——
  "局外"的字面含义。存档系统未实现（§4 待定）。
- `Add` 对阶梯外 ID 抛 `ArgumentException`——仓库里只可能有目录内的块。

## 2. 构建页（Game 层 BuildPanel）

主菜单 → 「构 建」进入：

- 顶栏：三种材料余额（时间片/RAM/驱动块，三色与图鉴一致）；
- 左列：仓库语块列表（`id ×数量`，点击选中家族）；
- 右侧：合成台——所选家族的完整阶梯（档位 1..5 + 自由形参行），
  每档一个「合成 ×2 → 下一档」按钮，数量不足置灰；
- 演示数据（SeedDemo）：attack(1)×4、heal(1)×2、shield(1)×2；时间片×8、RAM×4、驱动块×2。

## 3. 测试

`Tests/Editor/Codebase/CodebaseStateTests.cs`：目录解析往返与非法 ID、仓库/材料记账、
合成升降档、顶档→自由形参、非法家族/档位拒绝、演示数据锚点。跑法见 [OVERVIEW.md §2](OVERVIEW.md)。

## 4. 待定 / 下一步

- [ ] 存档持久化（JSON）——当前只在会话内存活
- [ ] 局内掉落接入：怪死掉块 → 入背包 → 撤离入库（LootDesign.md §4 掉落表）
- [ ] heal/shield 阶梯是推广规则，实测后确认（LootDesign.md §6）
- [ ] 档位数值曲线与 MaxTier=5 的合理性（当前为锚点）
- [ ] 合成的行数/周期收益提示（省 1 行省 1 次调用）在 UI 上的呈现
