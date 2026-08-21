# 碰撞判定（Physics）

> 对应代码：`Assets/Scripts/Core/Physics/`
> 职责：逻辑层自用的命中判定，不依赖 Unity 物理引擎。
> 全局约定见 [OVERVIEW.md](OVERVIEW.md)。

---

## 1. CircleOverlap

| 方法 | 语义 |
|---|---|
| `Overlaps(a, ra, b, rb)` | 两圆是否重叠；**贴边（圆心距 == 半径之和）算命中** |
| `Contains(center, r, point)` | 点是否在圆内（边界算在内），用于子弹命中判定 |

用 `sqrMagnitude` 比较，无开方，可每帧大量调用。

## 2. 为什么逻辑层自己做碰撞

Unity 物理引擎（Rigidbody/Collider/回调）被逻辑层边界规则禁止（见 OVERVIEW.md §5）：
它依赖场景对象和引擎运行时，进了单元测试就跑不了。幸存者类游戏只需要圆形/胶囊判定，
自己写几十行就够，还能在测试里直接断言。

`CircleOverlap` 是逻辑层使用 `UnityEngine.Vector2` 的示范：**只用纯数学类型，
不碰 GameObject / Time / 物理**。

## 3. 编译与测试的特殊性

- 用了 `UnityEngine.Vector2` → **只能在 Unity 里编译**，
  `StandaloneTests` 的 csproj 里通过 `Exclude` 排除了本目录
- 它的 6 个测试只在 Unity Test Runner 里跑（dotnet 快速验证跳过）

## 4. 测试

`Tests/Editor/Physics/CircleOverlapTests.cs`：相离/相交/贴边/零半径退化/点包含。
