using System.Collections;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Enemies;
using CPPRogue.Core.Loot;
using UnityEngine;
using UnityEngine.UI;

namespace CPPRogue.Game
{
    /// <summary>
    /// 敌人世界端口（Core.Enemies.EnemySim ↔ 表现层）：
    /// 每帧喂玩家位置 → Step(dt) → 把 sim 状态同步成视图（怪物/子弹/预兆线/爆炸圈），
    /// 并消费事件（日志、彩蛋飘字）。本身不含任何战斗逻辑。
    ///
    /// DemoUI 验收台操控：数字键 1-0 手动刷每种怪，G 切自动出怪，C 清场。
    /// </summary>
    public sealed class EnemyDirector : MonoBehaviour
    {
        public PlayerController Player;
        public RoutineHud Hud;

        private EnemySim _sim;
        private Text _hpText;
        private bool _autoSpawn = true;
        private float _autoSpawnTimer = 3f;
        private bool _deathShown;
        private bool _showRanges = true;   // 技能判定圈（调平衡用，V 切换）
        private RoutineEditor _editor;     // 重开时跨局保留的拼装结果
        private Transform _viewsRoot;      // 怪物/子弹/特效视图都挂这下面，重开随 director 一起拆
        private int _kills;                // 本局回收进程数（死亡弹窗统计用）

        // —— 掉落与背包（LootDesign.md §4：撤离入库，死亡全清）——
        private readonly System.Random _dropRng = new System.Random(20260831);
        private readonly List<Pickup> _pickups = new List<Pickup>();
        private readonly Dictionary<MaterialKind, int> _bag = new Dictionary<MaterialKind, int>();

        // —— 玩家状态可视化 ——
        private SpriteRenderer _shieldRing;
        private float _healFlash;

        private readonly Dictionary<int, EnemyView> _views = new Dictionary<int, EnemyView>();
        private readonly Dictionary<int, GameObject> _bulletViews = new Dictionary<int, GameObject>();
        private readonly HashSet<int> _aliveIds = new HashSet<int>();

        private static readonly EnemyKind[] AutoPool =
            { EnemyKind.Bug, EnemyKind.Bug, EnemyKind.Bug, EnemyKind.NullPointer, EnemyKind.NullPointer,
              EnemyKind.Breakpoint, EnemyKind.Breakpoint, EnemyKind.Exception, EnemyKind.Exception, EnemyKind.Storm };

        private static readonly (EnemyKind kind, string title)[] SpawnMenu =
        {
            (EnemyKind.Bug, "Bug"), (EnemyKind.NullPointer, "空指针"), (EnemyKind.Breakpoint, "断点"),
            (EnemyKind.Exception, "异常"), (EnemyKind.DeepCopy, "深拷贝"), (EnemyKind.Watchdog, "看门狗"),
            (EnemyKind.Compiling, "编译中"), (EnemyKind.Overheat, "过热"), (EnemyKind.Storm, "风暴"),
            (EnemyKind.Parent, "父进程"),
        };

        public EnemySim Sim => _sim;
        public bool AutoSpawn => _autoSpawn;

        /// <summary>手动刷新一只怪（测试面板按钮与数字键共用）。</summary>
        public void SpawnManual(EnemyKind kind)
        {
            SpawnAtRing(kind);
            string title = kind.ToString();
            foreach (var entry in SpawnMenu)
            {
                if (entry.kind == kind)
                {
                    title = entry.title;
                    break;
                }
            }
            if (Hud != null)
                Hud.Log($"[手动] 刷新 {title}");
        }

        public void ToggleAutoSpawn()
        {
            _autoSpawn = !_autoSpawn;
            if (Hud != null)
                Hud.Log(_autoSpawn ? "自动出怪：开" : "自动出怪：关");
        }

        public void ClearEnemies()
        {
            _sim.Clear();
            if (Hud != null)
                Hud.Log("清场");
        }

        public void Setup(PlayerController player, RoutineHud hud, RoutineEditor editor)
        {
            Player = player;
            Hud = hud;
            _editor = editor;
            // 图鉴直值覆盖（2026-08-31 改版）：hp/atk 直接用 EnemyTable.json 的数字，改表即生效
            _sim = new EnemySim(new EnemySimConfig(), seed: 42, CodexData.BuildStatOverrides());
            Player.MaxHp = _sim.PlayerMaxHp;
            Player.Hp = _sim.PlayerHp;

            _viewsRoot = new GameObject("Views").transform;
            _viewsRoot.SetParent(transform, false);

            // 护盾可视化：主角外圈的半透明青环（有盾才显示）
            var ringGo = new GameObject("ShieldRing");
            ringGo.transform.SetParent(Player.transform, false);
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = SpriteFactory.CreateCircle(96, new Color(0.55f, 0.9f, 1f, 0.4f));
            ring.sortingOrder = 9;
            ringGo.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
            ringGo.SetActive(false);
            _shieldRing = ring;

            // 血量显示：挂在 HUD 同一 Canvas 左下角
            var hpRect = UiFactory.Rect("PlayerHp", hud.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 64f), new Vector2(240f, 30f));
            _hpText = UiFactory.Label(hpRect, "", UiFonts.Code, 18, new Color(0.9f, 0.95f, 1f));

            if (Hud != null)
                Hud.Log("[1]Bug [2]空指针 [3]断点 [4]异常 [5]深拷贝 [6]看门狗 [7]编译中 [8]过热 [9]风暴 [0]父进程  G=自动出怪 C=清场 V=判定圈");
        }

        /// <summary>attack() 的落地：朝鼠标方向发射（CombatWorldBridge 委托进来）。</summary>
        public void PlayerAttack(float damage, float radius)
        {
            if (_sim == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Vector3 mouse = Input.mousePosition;
            mouse.z = -cam.transform.position.z;   // 正交相机：投到 z=0 游戏平面
            Vector3 world = cam.ScreenToWorldPoint(mouse);
            Vec2 origin = ToVec2(Player.transform.position);
            Vec2 dir = new Vec2(world.x, world.y) - origin;
            if (dir.SqrMagnitude < 1e-4f)
                dir = new Vec2(0f, 1f);   // 鼠标恰好在玩家圆心：给个默认向上
            _sim.SpawnPlayerBullet(origin, dir, damage, radius);
        }

        /// <summary>heal() 的落地：血量以 sim 为准（表现层只同步）。</summary>
        public void HealPlayer(float amount)
        {
            if (_sim != null)
                _sim.HealPlayer(amount);
            _healFlash = 0.6f;   // HP 数字短暂变绿
        }

        /// <summary>shield() 的落地：护盾由 sim 结算（先于血量吸收）。</summary>
        public void ShieldPlayer(float amount)
        {
            if (_sim != null)
                _sim.ShieldPlayer(amount);
        }

        /// <summary>tick 开始：清空"持续 1 tick"的护盾（TickDriver 经 CombatWorldBridge 调用）。</summary>
        public void BeginTick()
        {
            if (_sim != null)
                _sim.ClearPlayerShield();
        }

        /// <summary>执行超窗的惩罚伤害：穿盾穿无敌帧（LootDesign.md §1）。</summary>
        public void TimeoutPunish(float maxHpFraction)
        {
            if (_sim != null)
                _sim.TimeoutPunishDamage(maxHpFraction);
        }

        /// <summary>局内背包（撤离结算入库用；死亡即清——本对象随局一起拆）。</summary>
        public IReadOnlyDictionary<MaterialKind, int> Bag => _bag;

        /// <summary>撤离入库：背包材料写进局外档案并落盘。</summary>
        public void BankBag()
        {
            foreach (KeyValuePair<MaterialKind, int> pair in _bag)
                MainMenu.Profile.Codebase.AddMaterial(pair.Key, pair.Value);
            SaveFile.Save(MainMenu.Profile);
        }

        private void Update()
        {
            if (_sim == null || Player == null)
                return;
            HandleDebugKeys();
            TickAutoSpawn(Time.deltaTime);

            // 帧率尖峰时限制步长，避免子弹穿隧
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _sim.SetPlayerPosition(ToVec2(Player.transform.position));
            _sim.Step(dt);

            // 碰撞击退：sim 结算方向与衰减，位移应用在表现层（玩家位置归表现层所有）
            Vec2 knockback = _sim.PlayerKnockback;
            if (knockback.SqrMagnitude > 0f)
                Player.transform.position += new Vector3(knockback.X, knockback.Y, 0f) * dt;

            SyncEnemies();
            SyncBullets();
            UpdatePickups(dt);
            SyncPlayerHud();
            HandleEvents();
        }

        // —— 输入（验收台） ——

        private void HandleDebugKeys()
        {
            for (int i = 0; i < SpawnMenu.Length; i++)
            {
                if (Input.GetKeyDown((KeyCode)(KeyCode.Alpha1 + i))
                    || Input.GetKeyDown((KeyCode)(KeyCode.Keypad1 + i)))
                {
                    SpawnManual(SpawnMenu[i].kind);
                }
            }
            if (Input.GetKeyDown(KeyCode.G))
                ToggleAutoSpawn();
            if (Input.GetKeyDown(KeyCode.C))
                ClearEnemies();
            if (Input.GetKeyDown(KeyCode.V))
            {
                _showRanges = !_showRanges;
                foreach (EnemyView view in _views.Values)
                    view.SetRangeVisible(_showRanges);
                if (Hud != null)
                    Hud.Log(_showRanges ? "技能判定圈：开" : "技能判定圈：关");
            }
        }

        private void TickAutoSpawn(float dt)
        {
            if (!_autoSpawn)
                return;
            _autoSpawnTimer -= dt;
            if (_autoSpawnTimer > 0f)
                return;
            _autoSpawnTimer = 6f;

            int alive = 0;
            foreach (Enemy e in _sim.Enemies)
                if (!e.Dead)
                    alive++;
            if (alive < 10)
                SpawnAtRing(AutoPool[Random.Range(0, AutoPool.Length)]);
        }

        private void SpawnAtRing(EnemyKind kind)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vec2 player = ToVec2(Player.transform.position);
            _sim.Spawn(kind, player + Vec2.FromAngle(angle) * 10f);
        }

        // —— 视图同步 ——

        private void SyncEnemies()
        {
            _aliveIds.Clear();
            foreach (Enemy e in _sim.Enemies)
            {
                _aliveIds.Add(e.Id);
                if (!_views.TryGetValue(e.Id, out EnemyView view))
                {
                    var go = new GameObject($"Enemy_{e.DisplayName}_{e.Id}");
                    go.transform.SetParent(_viewsRoot, false);
                    view = go.AddComponent<EnemyView>();
                    view.Setup(e, EnemyVisuals.SkillRange(e.Kind, _sim.Config));
                    view.SetRangeVisible(_showRanges);
                    _views[e.Id] = view;
                }
                view.Sync(e);
            }
            RemoveStale(_views, _aliveIds);
        }

        private void SyncBullets()
        {
            _aliveIds.Clear();
            foreach (SimBullet b in _sim.Bullets)
            {
                _aliveIds.Add(b.Id);
                GameObject go;
                if (!_bulletViews.TryGetValue(b.Id, out go))
                {
                    Color color = EnemyVisuals.BulletColor(b);
                    go = new GameObject("Bullet");
                    go.transform.SetParent(_viewsRoot, false);
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = EnemyVisuals.CircleSprite(color);
                    renderer.sortingOrder = 6;
                    go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                    _bulletViews[b.Id] = go;
                }
                go.transform.position = new Vector3(b.Position.X, b.Position.Y, 0f);
            }
            RemoveStale(_bulletViews, _aliveIds);
        }

        private void RemoveStale(Dictionary<int, EnemyView> views, HashSet<int> alive)
        {
            List<int> stale = null;
            foreach (var pair in views)
            {
                if (alive.Contains(pair.Key))
                    continue;
                if (stale == null)
                    stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale == null)
                return;
            foreach (int id in stale)
            {
                if (views[id] != null)
                    Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }

        private void RemoveStale(Dictionary<int, GameObject> views, HashSet<int> alive)
        {
            List<int> stale = null;
            foreach (var pair in views)
            {
                if (alive.Contains(pair.Key))
                    continue;
                if (stale == null)
                    stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale == null)
                return;
            foreach (int id in stale)
            {
                if (views[id] != null)
                    Destroy(views[id]);
                views.Remove(id);
            }
        }

        private void SyncPlayerHud()
        {
            // sim 是血量唯一事实，同步给 PlayerController 供 hp 传感器 / HUD 读取
            Player.Hp = _sim.PlayerHp;
            if (_hpText != null)
            {
                string shield = _sim.PlayerShield > 0f ? $" +盾{_sim.PlayerShield:0.#}" : "";
                _hpText.text = $"HP {_sim.PlayerHp:0}/{_sim.PlayerMaxHp:0}{shield}    敌人 {AliveCount()}    背包 {BagText()}";
                _hpText.color = _healFlash > 0f
                    ? new Color(0.5f, 1f, 0.6f)
                    : _sim.PlayerHp <= _sim.PlayerMaxHp * 0.3f
                        ? new Color(1f, 0.4f, 0.4f)
                        : new Color(0.9f, 0.95f, 1f);
            }
            if (_healFlash > 0f)
                _healFlash -= Time.deltaTime;

            // 护盾环：有盾显示，盾越厚环越大
            if (_shieldRing != null)
            {
                bool hasShield = _sim.PlayerShield > 0f;
                _shieldRing.gameObject.SetActive(hasShield);
                if (hasShield)
                {
                    float scale = 1.7f + Mathf.Min(_sim.PlayerShield, 60f) * 0.008f;
                    _shieldRing.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
            if (_sim.PlayerDead && !_deathShown)
            {
                _deathShown = true;
                GameRun.Over = true;
                Player.Speed = 0f;
                Time.timeScale = 0f;
                if (Hud != null)
                    Hud.Log("进程终止：未捕获的异常导致崩溃 —— 你死了");
                DeathPanel.Create(Hud != null ? Hud.transform : transform, _sim.Time, _kills,
                    onRestart: () => GameBootstrap.Restart(_editor),
                    onSpectate: () => Time.timeScale = 1f);
            }
        }

        private int AliveCount()
        {
            int n = 0;
            foreach (Enemy e in _sim.Enemies)
                if (!e.Dead)
                    n++;
            return n;
        }

        // —— 掉落与背包 ——

        /// <summary>按图鉴档位掷骰掉落（LootDesign.md §4），在尸体位置生成材料球。</summary>
        private void RollDrops(EnemyKind kind, Vec2 position)
        {
            if (!CodexData.TryGet(kind, out CodexTier tier, out MaterialKind main))
                return;
            foreach (MaterialKind material in DropRoller.Roll(tier, main, _dropRng))
            {
                var go = new GameObject($"Pickup_{material}");
                go.transform.SetParent(_viewsRoot, false);
                var pickup = go.AddComponent<Pickup>();
                pickup.Setup(position, material);
                _pickups.Add(pickup);
            }
        }

        private void UpdatePickups(float dt)
        {
            Vec2 player = ToVec2(Player.transform.position);
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = _pickups[i];
                if (pickup == null)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }
                pickup.TickAnimation(dt);
                if (GameRun.Over || Vec2.Distance(pickup.Position, player) <= 1f)
                {
                    if (!GameRun.Over)
                    {
                        _bag.TryGetValue(pickup.Kind, out int n);
                        _bag[pickup.Kind] = n + 1;
                        if (Hud != null)
                            Hud.Log($"拾取 {Materials.Name(pickup.Kind)}（背包 {BagText()}）");
                    }
                    Destroy(pickup.gameObject);
                    _pickups.RemoveAt(i);
                }
            }
        }

        private string BagText()
        {
            var parts = new List<string>();
            if (_bag.TryGetValue(MaterialKind.TimeSlice, out int ts) && ts > 0) parts.Add($"片×{ts}");
            if (_bag.TryGetValue(MaterialKind.Ram, out int ram) && ram > 0) parts.Add($"RAM×{ram}");
            if (_bag.TryGetValue(MaterialKind.Driver, out int dr) && dr > 0) parts.Add($"驱×{dr}");
            return parts.Count > 0 ? string.Join(" ", parts) : "空";
        }

        // —— 事件消费 ——

        private void HandleEvents()
        {
            foreach (SimEvent ev in _sim.DrainEvents())
            {
                switch (ev.Type)
                {
                    case SimEventType.EnemyDied:
                        _kills++;
                        RollDrops(ev.Kind, ev.Position);
                        if (ev.Kind == EnemyKind.NullPointer && Hud != null)
                            Hud.Log("Segmentation fault (core dumped)");   // 表现层彩蛋 §3
                        break;

                    case SimEventType.ShieldAbsorbed:
                        if (Hud != null)
                            Hud.Log($"护盾吸收 {ev.Amount:0.#}");
                        break;

                    case SimEventType.Exploded:
                        StartCoroutine(ExplosionFx(ToVector3(ev.Position), _sim.Config.WatchdogBlastRadius));
                        if (Hud != null)
                            Hud.Log("看门狗超时复位！");
                        break;

                    case SimEventType.Compiled:
                        if (Hud != null)
                            Hud.Log("编译完成 → 运行态，变强了");
                        break;

                    case SimEventType.ChildrenReaped:
                        if (Hud != null)
                            Hud.Log("父进程被杀，僵尸进程全部回收（reap）");
                        break;
                }
            }
        }

        private IEnumerator ExplosionFx(Vector3 pos, float radius)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(_viewsRoot, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = EnemyVisuals.CircleSprite(new Color(1f, 0.75f, 0.3f));
            renderer.sortingOrder = 7;
            const float duration = 0.35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, k);
                renderer.color = new Color(1f, 0.75f, 0.3f, 1f - k);
                yield return null;
            }
            Destroy(go);
        }

        private static Vec2 ToVec2(Vector3 v) => new Vec2(v.x, v.y);
        private static Vector3 ToVector3(Vec2 v) => new Vector3(v.X, v.Y, 0f);
    }
}
