# PixelLab MCP 接入指南（ZCode）

> 配套文档：素材提示词 [ArtPrompts.md](ArtPrompts.md)（生成什么、提示词怎么写）。
> 本文档解决：怎么让 ZCode 直连 PixelLab，在任何一台新机器上 30 秒接入。

## 1. 这是什么

PixelLab 提供官方 MCP server（HTTP 型）。接入后，ZCode 会话里可以直接调用它的生成工具，不需要开网页手动操作——对 ZCode 说"用 ArtPrompts.md §3 的 Bug 提示词生成 sprite"即可。

主要能力（来自官网 <https://www.pixellab.ai/mcp>，以实际 tools/list 为准）：

| 工具 | 用途 | 对应 ArtPrompts.md |
|---|---|---|
| `create_character` | 像素角色，4/8 方向、四足模板 | 升级备选（默认全员单朝向+代码翻转） |
| `create_1_direction_object` | 单朝向物件（`style_images` 必填，小尺寸海量候选） | §3/§4 全部敌人 |
| `create_image_pro` | 高质量出图，可挂参考图（带 usage 标签）；≤42px 画布一次 64 候选 | §2 玩家、§6 材料/语法块、§7 子弹、§5 Boss 概念 |
| `create_topdown_tileset` | 俯视角 Wang 双地形过渡图块集 | §8 地板↔墙 |
| `create_map_object` | 透明背景地图物件（可传 `background_image` 匹配风格；**结果 8 小时删除**） | §8 摆件/撤离点 |
| `animate_character` / `animate_image` | 给角色/任意图加动画帧 | §7 特效、行走动画 |
| `edit_image` / `inpaint_image` | 整图编辑 / 局部重绘 | 修不满意的生成结果 |
| `create_image_pixflux` | 快速出图，`color_image` 可**强制锁调色板** | §0 第 8 条锁色工作流 |
| `get_image` / `get_character` | 轮询任务结果（create 全是异步） | 所有任务的第二步 |

> 命名对照：网站文档 <https://www.pixellab.ai/docs> 的 `create-sl-image-pro`（S-XL Image）就是 MCP 的 `create_image_pro`；参数以 <https://api.pixellab.ai/mcp/docs> 为准。

## 2. 前置准备：拿 API Key

1. 打开 <https://www.pixellab.ai>，注册并登录（登录入口 /signin）。
2. 在账户的 API Key 页面创建一个 key（形如长随机串），复制备用。

## 3. 一键接入（推荐，本仓库自带脚本）

仓库根目录的 `setup-pixellab-mcp.ps1` 会把 server 写进**用户级**配置 `~/.zcode/cli/config.json`（key 只留在本机，**不会进 git**；重复运行即覆盖更新，改 key 也用它）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File setup-pixellab-mcp.ps1 -ApiKey 你的key
```

写完**重启 ZCode（开新会话）**，到 Settings → MCP 确认 pixellab 已连接。

## 4. 手动接入（不想跑脚本时）

**方式 A：用户级（推荐）**——编辑 `C:\Users\<你>\.zcode\cli\config.json`，在 `mcp.servers` 下加入（文件里已有其他字段时只动 `mcp` 段）：

```json
{
  "mcp": {
    "servers": {
      "pixellab": {
        "type": "http",
        "url": "https://api.pixellab.ai/mcp",
        "headers": {
          "Authorization": "Bearer 你的key"
        }
      }
    }
  }
}
```

**方式 B：工作区级（团队共享）**——在仓库根建 `.zcode/config.json`，格式同上。打开项目即自动连接。
⚠️ **key 会随 git 提交**：仅私有仓库、且你接受泄露风险时用；默认建议 key 走用户级（方式 A / 脚本），仓库里只放本文档。

## 5. 生效与验证

1. 配置后必须**重启 ZCode / 新开会话**（MCP 在会话启动时连接）。
2. Settings → MCP：pixellab 显示已连接即成功。
3. 会话内直接下需求测试：

   > 用 pixellab 的 create_image_pro，参考玩家 sprite，生成 ArtPrompts.md §3 的 Bug：16x16 ……

## 6. 产物落地纪律

- 生成结果是**服务器暂存链接，有时效**（官网对 map object 标注 8 小时自动删除）——生成满意后**立刻**让 ZCode 下载保存到 `Assets/Art/`，别囤。
- 命名与目录照抄 ArtPrompts.md §9；导入设置照抄 §9 末尾（Point / None / PPU 16）。

## 7. 故障排查

| 症状 | 原因 / 处理 |
|---|---|
| Settings → MCP 里 pixellab 报 401 | key 没换掉占位符或填错——重跑脚本换真 key，再重启 |
| 连接超时 / 失败 | 先测网络：`curl -s -o NUL -w "%{http_code}" -X POST https://api.pixellab.ai/mcp -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"probe\",\"version\":\"0\"}}}"` 返回 200 即通 |
| 工具没出现在会话里 | 确认重启过会话；Settings → MCP 里选中 pixellab 修复/重连 |
| 生成任务一直 pending | 正常现象，MCP 工具是异步的——用 `get_image` 轮询 job id |
