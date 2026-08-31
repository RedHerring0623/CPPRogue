# 像素素材提示词（PixelLab 生成用）

> 对应设计稿：敌人 [EnemyDesign.md](EnemyDesign.md)，掉落 [LootDesign.md](LootDesign.md)。
> 生成通道：PixelLab 可经 MCP 直连 ZCode（含一键接入脚本），见 [PixelLabMCP.md](PixelLabMCP.md)。
> 参数依据：官方 MCP 文档 <https://api.pixellab.ai/mcp/docs>（网站文档 <https://www.pixellab.ai/docs> 里的 `create-sl-image-pro` 就是 MCP 的 `create_image_pro`）。
> 现状：全项目美术为 SpriteFactory 程序化圆形占位（1 sprite = 1 世界单位，PPU=尺寸）。
> 本文档只管"怎么生成"，不涉及接入代码；接入时按 §9 的导入设置。

## 0. 维护与生成纪律（先读这个）

**双语维护规则（本文档的核心约定）：**

1. **中文描述是唯一源头**：每条描述独立完整——形体、颜色、气质、关键元素全在中文里写清，不依赖上下文。
2. **英文 description = 中文描述的直译 + 术语统一**：英文里不得出现中文没有的元素。
3. **改形象 = 改中文描述**，然后对 ZCode 说"ArtPrompts.md 里 XX 的描述改了，更新英文提示词"。
4. **不要只改英文**：下次按中文重新生成英文时，单独改过的英文会被覆盖。
5. **术语对照表**（多次更新时保持措辞一致）：

| 中文 | 英文 |
|---|---|
| 发光 | glowing |
| 半透明 | translucent |
| 幽灵复制体 | ghost echo |
| 警告三角 | warning triangle |
| 电路/PCB 纹路 | circuitry |
| 黄铜时钟元素 | small brass clock detail |
| 橙色齿轮元素 | small orange gear detail |
| 像素假字（屏显文本） | tiny fake text glyphs |

**生成纪律（按 PixelLab 实际调用方式）：**

1. **description 只写主体**：尺寸、视角、透明底、描边全是工具参数（见 §1.2），**不要写进描述**。
2. **风格统一靠传图，不靠文字**：玩家定稿后，后续所有生成都把它传进对应参数——`create_1_direction_object` 的 `style_images`（必填）、`create_image_pro` 的 `reference_images`/`style_image_url`、`create_map_object` 的 `background_image`。
3. **先定基调再批量**：第一张只生成**玩家**，反复挑到满意为止，之后的调用全部挂它。
4. **小画布=海量候选，别一张张 roll**：≤42px 的画布一次出 **64 张候选**（20 代币）；43–85px 出 16 张；86–170px 出 4 张。我们的 16/24/32 档全在 64 候选区间——一次生成、批量挑选。
5. **视角**：全部单位（含玩家）一律单朝向正面，左右移动时代码水平翻转——吸血鬼幸存者式。个别单位以后真要多朝向再按 §1.1 的备注升级。
6. **验收标准**（每张放大 800% 检查）：是"真像素"（无半透明抗锯齿边、无渐变糊）；像素密度和参考图一致；调色板不超出参考图色系。
7. **阵营点缀**（可选，LootDesign §4 主题绑定）：掉时间片的怪（空指针/断点/看门狗/过热）带一点黄铜时钟元素；掉 RAM 的怪（深拷贝/父进程/僵尸）带绿色 PCB 纹路；掉驱动块的怪（Bug/异常/风暴/编译中）带橙色齿轮/警示元素。要用就把术语表对应英文加进 description 末尾。
8. **调色板锁定**（进阶）：把选定的调色板（Lospec 的 Endesga 32 / Nyx8 等）做成一张色条 PNG 存 `Assets/Art/palette.png`，用 `create_image_pixflux` 的 `color_image_url` 传它强制锁色（1 代币/张）。
9. **所有 create 都是异步**：立刻返回 job id，用 `get_image` / `get_character` 等轮询结果（一般 30s–5min）。

## 1. 工具选型与全局参数

### 1.1 本项目用到的工具

| 工具 | 用在 | 关键参数 | 备注 |
|---|---|---|---|
| `create_character` | 升级备选（默认不用）：要多方向走路动画的角色、四足怪 | `description`, `n_directions=4`, `size`, `view`, `proportions` | 四足怪用 `body_type=quadruped, template=dog/cat/…`；模板走路动画 1 代币/方向 |
| `create_1_direction_object` | **全部敌人**（单朝向+代码翻转） | `description`, `size`, `view=top-down`, `style_images`（必填=玩家图） | size≤42 时 64 候选 |
| `create_image_pro` | **玩家（定基调第一张）**、材料图标/语法块/子弹/Boss 概念 | `description`, `width`/`height`, `no_background=true`, `reference_images` | ≤42px 出 64 候选；`reference_images` 每项带 `usage` 标签；无 view 参数，朝向写进 description |
| `create_topdown_tileset` | 地板↔墙 Wang 瓦片集 | `lower_description`, `upper_description`, `tile_size=16`, `transition_size` | 双地形过渡模型，不是"一套地板+一套墙"；`lower_base_tile_id` 可链式扩地形 |
| `create_map_object` | 地图摆件/撤离点 | `description`, `width`/`height`, `view=top-down`, `background_image`（=地块截图，风格匹配） | **生成结果 8 小时自动删除，立刻保存** |
| `animate_character` / `animate_image` | 行走/攻击/特效动画 | `character_id` / `first_frame_url`, `frame_count=4` | 模板动画 1 代币/方向 |
| `edit_image` / `inpaint_image` | 修不满意的图 | `image_urls` + `description` / 遮罩 | inpaint 遮罩**白=重绘，黑=保留** |
| `create_image_pixflux` | 锁调色板出图 | `color_image_url`（=palette.png） | 1 代币 |

### 1.2 全局参数约定（每次调用都带上，条目里不再重复）

| 参数 | 取值 | 说明 |
|---|---|---|
| 尺寸 | 小兵/材料/子弹 **16**；精英 **24**；Boss **32** | 都是 8 的倍数，像素密度一致 |
| `view` | 1-dir object 一律 `top-down` | `create_image_pro` 没有 view 参数，朝向写进 description（"front-facing"） |
| `outline` | `single color black outline` | 全项目统一描边 |
| `no_background` | `true` | 透明底（image_pro 默认就是 true） |
| 风格参考 | 1-dir object 的 `style_images`；image_pro 的 `reference_images`（usage 写 `style reference`）；map_object 的 `background_image` | 一律挂玩家/已定稿图 |
| `text_guidance_scale` | 默认 8.0；出图不听描述就调高 | |

## 2. 玩家（最先做，定基调）

主角是一个正在运行的 Routine/小进程——不是人类角色。单朝向正面，移动只做水平翻转（吸血鬼幸存者式）；以后真要四方向走路动画，再按 §1.1 升级 `create_character`。

**玩家主角** ｜ `create_image_pro` · width 16 · height 16 · `no_background=true`（64 候选里挑定基调的那张）

- 描述：正面朝向的一个正在运行的小程序生物：小小的发光人形，身体由绿色终端代码构成，身形圆润紧凑，胸口有一颗亮青色核心，体表有淡淡的扫描线纹理，气质机灵可爱。

```text
a front-facing tiny living program, small glowing humanoid shape made of green terminal code, short round body with a bright cyan core in its chest, faint scanline texture, cute and agile
```

备选方向（不满意时换方向 roll）——**光标骑士**，参数同上：

- 描述：正面朝向的一个微型"光标骑士"：发亮的白色鼠标箭头当脑袋，安在绿色程序小身子上，手里举着一面小括号盾牌。

```text
a front-facing tiny cursor-knight, a glowing white mouse arrow as its head on a small green program body, holding a tiny bracket shield
```

要走路动感：拿定稿用 `animate_image` 补 4 帧原地小弹跳（action：`gentle walking bob`）。

## 3. 普通敌人（基础四件套）

工具统一：`create_1_direction_object` · size 16 · view `top-down` · `style_images`=[玩家定稿]（下面不再重复）。

| # | 敌人 | 机制要点（视觉要呼应） | 阵营点缀 |
|---|---|---|---|
| 1 | Bug 杂兵虫 | 数量压制 | 橙齿轮 |
| 2 | 空指针 | 极快极脆、半透明感 | 黄铜时钟 |
| 3 | 断点 | 停下读条→冲刺 | 黄铜时钟 |
| 4 | 异常 | 远程丢"异常对象" | 橙齿轮 |

**Bug（杂兵虫）**

- 描述：一只油亮的绿色小甲虫（软件 bug 生物），背壳带细小的六边形花纹，一对愤怒的圆点眼睛，短短的小腿，一脸淘气。

```text
a small glossy green beetle, software bug creature, shiny carapace with a tiny hexagon pattern, two angry dot eyes, stubby little legs, mischievous look
```

**空指针**

- 描述：一个半透明白色的箭头光标幽灵生物——空心的光标外壳成了怪物，中心是一个空洞，轮廓忽明忽暗地闪烁，剪影细瘦、看上去就很快。

```text
a ghostly translucent white arrow-pointer creature, an empty cursor shell turned monster, hollow core like a hole, faint flickering outline, thin and fast silhouette
```

**断点**

- 描述：一个实心的红色八角停止标记生物——IDE 断点标记成了怪物，核心是一颗发亮的红点，站姿沉重扎地，眼神倔强地眯着眼。

```text
a solid red octagon stop-marker creature, IDE breakpoint marker with a glowing red dot core, braced heavy stance, stubborn squinting eyes
```

**异常**

- 描述：一个漂浮的红色错误弹窗小怪：愤怒的像素信封脸，正面一个醒目的感叹号，周身窜着红色电弧，摆出正要被抛出去的姿势。

```text
a small floating red error-box creature, an angry pixel envelope with a bright exclamation mark, crackling red energy around it, mid-throw pose as if being thrown
```

## 4. 精英敌人

工具统一：`create_1_direction_object` · size 24 · view `top-down` · `style_images`=[玩家定稿]（僵尸进程 size 16）。

| # | 敌人 | 机制要点（视觉要呼应） | 阵营点缀 |
|---|---|---|---|
| 5 | 深拷贝 | 死亡分裂 | 绿 PCB |
| 6 | 看门狗 | 倒计时自爆 | 黄铜时钟 |
| 7 | 编译中 | 读条态→运行态（**两张**） | 橙齿轮 |
| 8 | 过热 | 快段/冒烟慢段（**两张**） | 黄铜时钟 |
| 9 | 风暴 | 高频绕行弹幕 | 橙齿轮 |
| 10 | 父进程 | 召唤僵尸 | 绿 PCB |
| — | 僵尸进程 | 被召唤的附属怪（size 16） | 绿 PCB |

**深拷贝**

- 描述：一只凝胶质感的青色全息生物，同时有两具重叠的身体：前面一具实体、后面拖着一具略微错位的幽灵复制体，整体是"复制粘贴"出来的剪影，边缘半透明。

```text
a gel-like cyan hologram creature with two overlapping identical bodies, one solid body and one ghost echo copy slightly offset behind it, copy-paste silhouette, translucent edges
```

**看门狗**（若要四足行走动画：改用 `create_character` · `body_type=quadruped` · `template=dog`）

- 描述：一只金属板和电路拼成的矮壮护卫犬，双眼发红光，脖子上挂着一枚小圆倒计时器，头顶一根短天线，全身绷紧的看守姿态。

```text
a stocky guard dog built from metal plating and circuitry, glowing red eyes, a small round countdown timer on its collar, short antenna on its head, tense watchdog stance
```

**编译中——编译态**（第 1 张）

- 描述：一只灰暗低功耗的芯片技工生物：弯着腰对着面前一根悬浮的横向进度条，身体半透明，胸腔里的齿轮缓缓转动，一副昏昏欲睡、毫无防备的样子。

```text
a dim gray chip-mechanic creature hunched over a floating horizontal progress bar, half transparent and low power, slowly spinning gears inside its chest, sleepy vulnerable look
```

**编译中——运行态**（第 2 张；两张对不上时，用运行态满意稿作 `reference_images` 重 roll 编译态）

- 描述：同一只芯片技工生物，但已经完全通电暴走：体型轮廓不变，通体亮橙色，双眼喷光，电火花四溅，摆好冲锋的架势。

```text
the same chip-mechanic creature now fully powered and enraged, same body shape but bright orange glow, blazing eyes, electric sparks, ready to charge
```

**过热——过热帧**（第 1 张）

- 描述：一只矮壮的硅芯片生物：身体是散热器造型，背着一枚小风扇，外壳裂缝里透出发亮的橙红色裂纹，正处于危险过热的状态。

```text
a stocky silicon-chip creature with a heatsink body and a tiny fan on its back, glowing orange cracks spreading across its shell, running dangerously hot
```

**过热——降频帧**（第 2 张，同体冷却态）

- 描述：同一只硅芯片生物冷却之后的样子：身体转为淡蓝色，风扇停转，头顶飘起细细的白色蒸汽，疲惫而迟钝。

```text
the same silicon-chip creature cooled down, pale blue body, fan idle, thin white steam rising, tired and sluggish
```

**风暴**

- 描述：一小团静电构成的风暴云生物：云团内部劈着黄色闪电，身边漂浮着小小的黄色警告三角，整个身体躁动不安。

```text
a small storm cloud creature made of electric static, yellow lightning bolts crackling inside it, tiny warning triangles floating around it, jittery and restless
```

**父进程**

- 描述：一台大方块处理器芯片怪：踩着粗短的小腿，背上长着叉子形状的金属插脚，姿态沉稳威严，腹部有一个发光的孵化口。

```text
a big square processor-chip monster on stubby legs, fork-shaped metal pins on its back, calm authoritative posture, a glowing spawn port on its belly
```

**僵尸进程**（size 16）

- 描述：一个灰色的僵尸进程小怪：破破烂烂的小程序剪影，眼窝空洞地发着光，身体像垂死的进程一样忽明忽暗地闪烁，拖着行尸走肉般的步态。

```text
a small gray zombie process, shabby little program sprite with hollow glowing eyes, flickering body like a dying process, undead shamble
```

## 5. Boss 概念（⚠️ 仅探索用）

> Boss 设计稿未定（EnemyDesign §0 预留名，设计由用户后续提供）。以下只出**概念图**探索方向，定稿前不进游戏。
> 工具统一：`create_image_pro` · width 32 · height 32 · `no_background=true`（32px 一次出 64 张候选，适合概念海选）。

**死循环 for(;;)**（预留 Boss 位）

- 描述：一条由发光绿色代码字符构成的衔尾蛇：追着自己的尾巴咬，盘成催眠般的螺旋，鳞片是一粒粒分号，周身散发"无限循环"的能量。

```text
an ouroboros serpent made of glowing green code characters chasing its own tail, coiled into a hypnotic spiral, semicolon scales, infinite loop energy
```

**GC 垃圾回收器**

- 描述：一台笨重的垃圾回收机器人：一条巨大的磁铁手臂，肚子是垃圾压实机，身后拖着一串发光的内存方块，像个快乐但无情的清洁工。

```text
a hulking garbage-collector robot with a huge magnet arm and a trash compactor belly, dragging glowing memory cubes behind it, cheerful but merciless janitor
```

**祖传屎山**

- 描述：一座由层层叠叠的祖传代码石板堆成的高山：石板布满裂痕、摇摇欲坠，层与层之间戳出石化了的"注释骨头"。

```text
a towering unstable mountain of stacked legacy code slabs, cracked and crumbling, fossilized comment bones sticking out of the layers
```

**Kernel Panic**（最终 Boss 候选）

- 描述：一个碎裂的 CRT 显示屏魔像：脸是一整块蓝屏，屏上排满白色恐慌报错文本的像素假字，电缆当四肢，散发冷冷的蓝光，最终 Boss 的气场。

```text
a cracked CRT monitor golem with a blue screen face full of white panic text glyphs, cables as limbs, cold blue glow, final boss aura
```

## 6. 掉落物

工具统一：`create_image_pro` · width 16 · height 16 · `no_background=true` · `reference_images=[{url: 玩家定稿, usage: "style reference"}]`（64 候选挑图）。

**材料三件套**（颜色即身份，当前游戏内是三色球，接入后球换成这些图标）：

**时间片**

- 描述：一个金色的钟表盘被切成扇形，其中一块楔形比其他更亮——"时间片"材料。

```text
a golden clock face split into pie slices, one wedge glowing brighter than the rest, time slice material
```

**RAM**

- 描述：一根绿色内存条：小块电路板，板上是芯片，底边一排金色金手指引脚。

```text
a green RAM memory stick, small circuit board with golden connector pins along the bottom edge
```

**驱动块**

- 描述：一个齿轮和芯片的融合体：蓝色方形芯片身上长出一圈齿轮外齿——"驱动"模块。

```text
a blue gear fused with a microchip, cog-wheel teeth on a square chip body, driver module
```

**语法块**（掉落/拾取形态；底板统一，正面符号区分；稀有度靠外圈光色区分：白→绿→蓝→紫→金）：

**语法块底板**

- 描述：一块深色的圆角代码芯片，正面有一个发光符号，整体微微悬浮，下方一点淡淡的投影。

```text
a small dark rounded code chip with a glowing glyph on the front, floating slightly, subtle drop shadow beneath
```

正面符号变体（在底板描述后追加一句；中文描述同样只描述符号；懒得逐个生成就用 `edit_image` 在底板上改符号）：

| 符号 | 中文描述 | 追加英文 |
|---|---|---|
| attack | 发光的红色剑形符号 | `with a glowing red sword glyph` |
| heal | 发光的绿色十字符号 | `with a glowing green cross glyph` |
| shield | 发光的蓝色盾牌符号 | `with a glowing blue shield glyph` |
| for/while | 发光的循环箭头符号 | `with a glowing circular arrow glyph` |
| if/else | 发光的分叉箭头符号 | `with a glowing forked arrow glyph` |
| return/break | 发光的出口箭头符号 | `with a glowing exit arrow glyph` |

## 7. 子弹与特效

**子弹**（16×16 画布，实际主体可只占 6–10px）：工具 `create_image_pro` · width 16 · height 16。

**玩家子弹**

- 描述：一枚发光的绿色飞镖，形如">"光标箭头，亮核带一条短尾迹。

```text
a glowing green dart shaped like a ">" cursor arrow, bright core with a short trail
```

**异常子弹**

- 描述：一颗旋转的红色错误小方块，正面带感叹号，愤怒地噼啪冒电。

```text
a small red spinning error cube with an exclamation mark, angry and crackling
```

**风暴子弹**

- 描述：一枚锐利的黄色警告三角火花，小而快。

```text
a small yellow warning triangle spark, sharp and fast
```

**特效**：先出首帧，再 `animate_image`（`frame_count=4`，`first_frame_url`=首帧）。每条附**动作描述**（`action` 参数只写运动，不写环境）。

**命中火花**

- 描述：一簇绿色像素火花爆开，末帧化作熄灭的余烬。
- 动作：`green pixel spark burst, quick flash then fading embers`

**自爆冲击环**（看门狗自爆用）

- 描述：一个橙色冲击波圆环向外扩散，环的边缘带着电花。
- 动作：`orange shockwave ring expanding outward with electric edges`

**蒸汽烟**（过热降频用）

- 描述：一小团灰白色蒸汽上升、散开、消散。
- 动作：`gray-white steam puff rising and dissipating`

**拾取闪光**

- 描述：一瞬间的亮光闪，带小小的十字光芒。
- 动作：`brief bright glint with tiny plus-shaped rays`

## 8. 地块

> PixelLab 的地块是 **Wang 双地形过渡模型**：一次调用生成"下层地形 ↔ 上层地形"的完整过渡瓦片集，不是"一套地板+一套墙"。
> 我们的用法：下层=主板地板，上层=抬高的芯片台地（当墙用），`transition_size=1.0` 生成 25 块悬崖砖（带立面）。

**地板 ↔ 芯片墙** ｜ `create_topdown_tileset` · `tile_size=16` · `transition_size=1.0`

- 描述（下层·地板）：深绿黑色的 PCB 主板地板，板上有暗铜色的走线和零星焊点。
- lower_description：

```text
dark green-black PCB motherboard floor with faint copper traces and solder dots
```

- 描述（上层·墙体）：抬高的集成电路台地，表面是芯片顶盖，边缘是焊死的金属亮边，立面能看到 PCB 侧板。
- upper_description：

```text
raised integrated-circuit chip platform with soldered bright metal edges, PCB side visible on the cliff face
```

后续地图扩地形用链式（`lower_base_tile_id` 接上一次的 base id），比如 "主板地板 ↔ 数据总线金属地面"。

**地图摆件** ｜ `create_map_object` · 16×16 · view `top-down` · `background_image`=地块集截图（⚠️ 结果 8 小时删除，立刻保存）。每件一调：

- 电容柱——描述：一颗直立的大电解电容，顶上两根短引脚，当柱子用。

```text
a standing electrolytic capacitor with two short pins on top, pillar prop
```

- 电阻路障——描述：一排轴向色环电阻横放，当路障用。

```text
a row of axial color-band resistors lying flat, barrier prop
```

- 跳线碎片——描述：几根散落的跳线针脚和金属碎屑，当地面杂物用。

```text
scattered jumper pins and metal debris on the ground
```

**撤离点** ｜ `create_map_object` · 32×32 · view `top-down`（占 2×2 格）

- 描述：一块嵌入地面的 USB 风格接口地台：端口凹槽外有一圈发光的绿色光环。

```text
a USB-like port docking pad inset in the floor with a glowing green light ring, extraction point
```

## 9. 文件命名与导入

命名（建议直接照抄，接入代码时省心）：

```text
Assets/Art/Enemies/   enemy_bug / enemy_null_pointer / enemy_breakpoint / enemy_exception
                       enemy_deep_copy / enemy_watchdog / enemy_compiling / enemy_compiling_on
                       enemy_overheat / enemy_overheat_cool / enemy_storm / enemy_parent / enemy_zombie
                       boss_for_loop / boss_gc / boss_legacy_mountain / boss_kernel_panic   (概念)
Assets/Art/Materials/ material_time_slice / material_ram / material_driver
Assets/Art/Fragments/ fragment_base / fragment_attack / fragment_heal / fragment_shield
                       fragment_loop / fragment_branch / fragment_return
Assets/Art/Bullets/   bullet_player / bullet_exception / bullet_storm
Assets/Art/Effects/   fx_hit / fx_explode / fx_smoke / fx_sparkle
Assets/Art/Tiles/     tileset_floor_wall / prop_capacitor / prop_resistors / prop_debris / tile_extraction
Assets/Art/           palette.png（锁色板色条图，可选）
```

玩家和敌人都是单朝向单张（`player.png`、`enemy_bug.png`…），不编方向号——水平翻转交给代码。

导入团结引擎（每张 PNG）：

- Texture Type：Sprite (2D and UI)
- Filter Mode：**Point (no filter)**
- Compression：**None**
- Pixels Per Unit：**16**（保持现状"1 sprite = 1 世界单位"，直接替换现在的圆形）
- 多帧/多方向合图：Sprite Mode = Multiple，手切或按网格切

## 10. 生成顺序清单（照此逐项打勾）

| 顺序 | 内容 | 数量 | 工具 |
|---|---|---|---|
| 1 | 玩家 | 1（64 候选定基调） | `create_image_pro` 16×16 |
| 2 | 普通敌人 | 4 | `create_1_direction_object` size 16，style_images=玩家 |
| 3 | 精英 | 8（编译中/过热各两态） | `create_1_direction_object` size 24 |
| 4 | 僵尸进程 | 1 | 同上 size 16 |
| 5 | 材料三件套 | 3 | `create_image_pro` 16×16 |
| 6 | 语法块 | 1 底板 + 6 符号 | `create_image_pro` 16×16 / `edit_image` |
| 7 | 子弹 | 3 | `create_image_pro` 16×16 |
| 8 | 特效 | 4 组×4 帧 | 首帧 `create_image_pro` + `animate_image` |
| 9 | 地块 | 1 套 + 3 摆件 + 撤离点 | `create_topdown_tileset` + `create_map_object` |
| 10 | Boss 概念（可选） | 4 | `create_image_pro` 32×32 |
