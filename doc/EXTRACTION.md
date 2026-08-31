# 局内循环：掉落 · 撤离 · 存档（Extraction）

> 对应代码：`Core/Loot/DropRoller.cs`（掷骰）+ `Game/Enemies/EnemyDirector.cs`（掉落/拾取/背包）
> + `Game/ExtractionPoint.cs` / `Game/ExtractionPanel.cs`（撤离）+ `Game/SaveFile.cs`（存档 IO）
> 设计稿：[LootDesign.md](../LootDesign.md) §3/§4（撤离唯一入库通道、掉落表）
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 闭环全景

```
杀怪 → DropRoller 掷骰（35%/必掉2）→ 材料球（Pickup）→ 走近拾取 → 局内背包（死亡全清）
                                                                        │
                              撤离点读条 2s（g++ compiling）───────────┤→ 入库 CodebaseState + 落盘 save.json
                                                                        │
                                                    死亡 → 弹窗重开 → 背包清空，档案不动
```

- **撤离是唯一入库通道**（LootDesign §3）：背包材料写进 `MainMenu.Profile.Codebase` 并 `SaveFile.Save`。
- 死亡/返回主菜单：局内世界整体拆除（GameBootstrap.TearDown），背包随之消失——单条规则无特例。
- 撤离结算时 `GameRun.Over = true`：ESC 与代码调度冻结，由 ExtractionPanel 接管。

## 2. 掉落与拾取

| 环节 | 实现 |
|---|---|
| 掷骰 | `Core/Loot/DropRoller.Roll(tier, mainDrop, rng)`——纯逻辑可测；普通/附属 35%×主材料，精英必掉 2（主+随机） |
| 档位/主材料来源 | `Game/CodexData`：EnemyKind → 图鉴条目（与图鉴同源，EnemyTable.json） |
| 表现 | `Pickup`：材料色小球（三色与图鉴一致），上下浮动；挂在 EnemyDirector 的 Views 下 |
| 拾取 | Director 每帧查距（1.0 世界单位）→ 入背包 → 日志；HUD 血条行显示「背包 片×n RAM×n 驱×n」 |

语块掉落（attack(1) 之类）**未接入**——掉落率待设计（LootDesign §6），当前只有材料。

## 3. 撤离点（测试地图）

- **常态存在**：绿环底盘 + 内芯读条填充；正式地图的出现条件后续加（`AlwaysActive` 语义已预留）。
- **指引箭头**：撤离点存在期间，主角外环（1.5 单位）上常驻绿色三角箭头指向它（`SpriteFactory.CreateTriangle`）。
- **读条**：进入 2.2 半径开始 2s 读条，离开快速回退；期间怪照常攻击（风险窗口）。
- 完成 → `BankBag()` 入库落盘 → 撤离结算弹窗（入库明细 + 返回主菜单）。

## 4. 存档

| 层 | 职责 |
|---|---|
| `Core/Codebase/SaveGame` | PlayerProfile ↔ JSON 文本（MiniJson 往返）；版本号只读不迁档，未知语块跳过 |
| `Core/Code/BlockJson` | Block/Expr ↔ JSON（战备 BD 落盘用，即 CODE_EXECUTION §7 规划的 Routine 序列化） |
| `Game/SaveFile` | 文件 IO：`persistentDataPath/save.json`；损坏/缺失返回 null → 新档 |

保存时机：**撤离入库、构建页任何改动（合成/装备/兑换）——改完立即落盘**；局内热编辑不落盘（随局消失）。

## 5. 测试

| 文件 | 覆盖 |
|---|---|
| `Tests/Editor/Loot/DropRollerTests.cs` | 精英必掉 2、普通/附属 35% 统计、随机材料覆盖三种 |
| `Tests/Editor/Code/BlockJsonTests.cs` | 全部语句/表达式无损往返、非法输入 |
| `Tests/Editor/Codebase/ProfileTests.cs` | 等比兑换、新档锚点、存档往返、战备校验 |

## 6. 待定

- [ ] 语块掉落率（材料之外的 Fragment 怎么进仓库）
- [ ] 正式地图撤离点出现条件
- [ ] 眩晕期间无敌帧规则的实测校准
