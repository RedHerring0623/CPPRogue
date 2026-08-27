# 图鉴与掉落数据（Codex）

> 对应代码：`Assets/Scripts/Core/Loot/`（数据层，asmdef `CPPRogue.Core`）+ `Assets/Scripts/Game/CodexPanel.cs`（图鉴 UI）
> 设计稿：[LootDesign.md](../LootDesign.md)（材料/掉落规则）、[EnemyDesign.md](../EnemyDesign.md)（属性/行为的唯一事实来源）
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. 数据从哪来

```
EnemyTable.json（Assets/Resources/，运行时 Resources.Load）
        │ EnemyCodexJson.Parse（MiniJson 手写解析器）
        ▼
   CodexBook（条目列表） ──解析失败/文件缺失──▶ EnemyCodex.Default()（代码内置镜像）
        │
        ▼
CodexPanel（图鉴 UI，只读展示）
```

- **JSON 是运行时数据源**：改描述/掉落/等级不用重编译，Play 即生效。
- **内置表是回退**：JSON 缺失或解析失败时图鉴仍能打开（Debug.LogWarning 提示）。
- **同步测试锁死两边**：`EnemyCodexTests.JSON资产_与内置表逐字段同步` 逐字段比对，
  谁改了没同步另一边，测试立刻红。

## 2. Core/Loot 各文件

| 文件 | 职责 |
|---|---|
| `MaterialKind.cs` | 材料三件套枚举（TimeSlice/Ram/Driver）+ 稳定 ID、展示名、效果文案 |
| `CodexEntry.cs` | 图鉴条目（id/中英名/档位/属性等级/积分/主材料/描述）+ `CodexTier`（Normal/Elite/Minion） |
| `EnemyCodex.cs` | 内置镜像表 `Default()`；`KindForId`/`IdForKind`（图鉴 ↔ EnemyKind 的唯一连接点）；`TierRule` 档位掉落规则文案 |
| `MiniJson.cs` | 极简 JSON 解析器（对象/数组/字符串/数字/布尔/null）。为什么手写：Core 不引第三方库（netstandard2.1），JsonUtility 不支持字典 |
| `EnemyCodexJson.cs` | EnemyTable.json → `CodexBook`；格式错误抛 FormatException（带条目定位） |

图鉴**只报属性等级**（hp2/atk1/spd2）；等级 → 数值的换算归 StatTable，图鉴不参与战斗。

## 3. 图鉴 UI（Game 层）

入口：**ESC → 主菜单 → 怪物图鉴**（PauseController 管理，打开时 timeScale=0）。

- 左列：按 普通/精英/附属 分组的怪物名单，点击切换详情；
- 右侧：名称/档位、属性等级与出怪积分、主掉落与档位规则、描述引文、补充说明、附属关系；
- 底部：三种材料的效果一句话（材料三色：时间片青 / RAM 绿 / 驱动块橙）；
- 返回按钮或 ESC 关闭并继续游戏。

## 4. 调参流程（改表必须看）

1. 改 `Assets/Resources/EnemyTable.json`；
2. **同步改 `EnemyCodex.Default()`**（同步测试盯着）；
3. 属性等级 / 出怪积分的源头仍是 EnemyDesign.md，行为数值源头仍是 StatTable/EnemySimConfig；
4. `dotnet test` 跑绿。

## 5. 测试

| 文件 | 覆盖 |
|---|---|
| `Tests/Editor/Loot/MiniJsonTests.cs` | 解析器全部语法 + 非法输入（尾逗号/残缺/未闭合…） |
| `Tests/Editor/Loot/EnemyCodexTests.cs` | 内置表完整性（覆盖全部 EnemyKind、字段合法域）、解析器映射与校验、JSON ↔ 内置表逐字段同步 |

同步测试从程序集位置向上搜索数据表（dotnet 在 StandaloneTests/bin 下、Unity 在 Library/ScriptAssemblies 下都能命中），
仓库外运行时 `Assert.Ignore` 跳过。跑法见 [OVERVIEW.md §2](OVERVIEW.md)。
