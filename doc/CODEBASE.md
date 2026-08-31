# 局外代码库（Codebase）

> 对应代码：`Assets/Scripts/Core/Codebase/`（模型，asmdef `CPPRogue.Core`）+ `Assets/Scripts/Game/BuildPanel.cs`（构建页 UI）
> 设计稿：[LootDesign.md](../LootDesign.md) §3（搜打撤局内/局外管理与块合成规则）
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 模型（Core/Codebase/）

| 文件 | 职责 |
|---|---|
| `FragmentCatalog.cs` | 参数化语块阶梯：家族（attack/heal/shield）、档位上限（MaxTier=5）、ID 生成与解析（`attack(2)`/`attack()` → `FragmentDef`，阶梯外返回 null） |
| `CodebaseState.cs` | 局外状态：仓库语块计数 + 三种材料余额（`Add`/`Spend`）+ 合成规则（`CanMerge`/`Merge`：2×tier → tier+1，2×顶档 → 自由形参） |
| `MetaProgress.cs` | 局外成长：行数（基础 2，+1 行花 RAM 1,2,4,8…）与时间窗（基础 1s，+0.5s 花时间片 1,2,4,8…）的等比兑换（2026-08-31 定稿锚点） |
| `PlayerProfile.cs` | 玩家档案：仓库 + 成长 + 战备 BD（`Loadout`）；新档 = 一条 attack(1) |
| `SaveGame.cs` | 档案 ↔ JSON 文本（存档格式 v1，详见 §3）；版本只读不迁档，未知语块跳过 |
| `LoadoutValidator.cs` | 战备 BD 校验：行数 ≤ 成长上限、每种语块用量 ≤ 仓库持有（attack(变量) 按 attack() 计）；`ToBlock(id)` 把语块 ID 变成可拼装语句 |

关键语义：

- **ID 即档位**：`attack(1)` 的数字就是档位（同时也是参数字面量，烧死在块里）；`attack()` 形参留空，
  拼装时可绑定任意变量——阶梯顶点的严格上位（LootDesign.md §3）。
- **自由形参不可再合成**（Tier=0 不参与 Merge）。
- **档案跨局保留**：Game 层 `MainMenu.Profile` 静态持有，局内死亡/重开不清，改动即时落盘（`SaveFile`）。
- `Add` 对阶梯外 ID 抛 `ArgumentException`——仓库里只可能有目录内的块。

## 2. 构建页（Game 层 BuildPanel）——两个分页

主菜单 → 「构 建」进入，右上分页签切换：

- **战备 BD 页**：拖拽拼装（复用 EditorPanel 的拖放 UI）。左侧面板 = 仓库持有的语块
  （`FragmentPalette.Build`）；插入校验 = 每种语块用量 ≤ 仓库持有（`FragmentPalette.BuildCanInsert`）；
  改动即时落盘（`FragmentPalette.Persist`：校验通过 → 克隆进档案 → SaveFile）。
- **升级 / 合成页**：材料余额 + 兑换按钮（「行数 +1（RAM ×n）」「时间窗 +0.5s（时间片 ×n）」，花费不足置灰）
  + 仓库列表（选家族）+ 合成台（2×同级 → 升一档）。

**局内 ESC 的双 BD（测试地图）**：`编辑 BD · 正常` = 同样的仓库受限编辑 + 即时存档（热更新）；
`编辑 BD · 测试` = 全语句自由拼、不存档、随局消失（正式地图没有这个入口）。
正常 BD 的三件套（面板/校验/落盘）与构建页共用 `FragmentPalette`。

## 3. 存档格式（v1）

`persistentDataPath/save.json`：

```json
{ "version": 1,
  "blocks": {"attack(1)": 2},
  "materials": {"timeSlice": 8, "ram": 0, "driver": 2},
  "lineBonus": 1, "windowSteps": 0,
  "loadout": [ {"kind":"call","name":"attack","args":[{"k":"lit","n":1}]} ] }
```

loadout 的语句结构由 `Core/Code/BlockJson` 定义（全部 BlockKind/ExprKind 无损往返，测试锁定）。

## 3. 测试

`Tests/Editor/Codebase/CodebaseStateTests.cs`：目录解析往返与非法 ID、仓库/材料记账、
合成升降档、顶档→自由形参、非法家族/档位拒绝、材料 Spend、档案/成长/存档/战备校验（ProfileTests）。跑法见 [OVERVIEW.md §2](OVERVIEW.md)。

## 4. 待定 / 下一步

- [x] 存档持久化（save.json，2026-08-31 实现，格式见 §3）
- [x] 局内材料掉落接入（怪死掉材料 → 背包 → 撤离入库，见 [EXTRACTION.md](EXTRACTION.md)）
- [ ] 局内**语块**掉落（Fragment 掉落率待设计，当前只掉材料）
- [ ] heal/shield 阶梯是推广规则，实测后确认（LootDesign.md §6）
- [ ] 档位数值曲线与 MaxTier=5 的合理性（当前为锚点）
- [ ] 合成的行数/周期收益提示（省 1 行省 1 次调用）在 UI 上的呈现
- [ ] 战备 BD 的完整拖拽编辑（当前是追加+删除，后续复用 EditorPanel 的拖拽）
