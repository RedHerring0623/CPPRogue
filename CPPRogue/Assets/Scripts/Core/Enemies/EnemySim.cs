using System.Collections.Generic;

namespace CPPRogue.Core.Enemies
{
    /// <summary>
    /// 敌人世界的确定性模拟器：怪物、敌我子弹、玩家血量与全部伤害结算都在这层完成。
    /// 不碰 GameObject / Time / 全局随机——表现层每帧喂玩家位置和 dt，再把状态同步到视图。
    /// 伤害模型按 EnemyDesign.md §0：接触单次结算 + 每敌 0.8s 碰撞冷却 + 玩家 0.3s 全局无敌帧。
    /// 随机只走注入的 System.Random（种子可复现），单元测试不飘。
    /// </summary>
    public sealed class EnemySim
    {
        private static readonly string[] ExceptionNames = { "bad_alloc", "out_of_range", "poison_exception", "bad_cast" };

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<SimBullet> _bullets = new List<SimBullet>();
        private List<SimEvent> _events = new List<SimEvent>();
        private readonly System.Random _rng;
        private int _nextId = 1;

        public EnemySim(EnemySimConfig config, int seed = 0,
            System.Collections.Generic.IReadOnlyDictionary<EnemyKind, EnemyStatOverride> statOverrides = null)
        {
            Config = config;
            _rng = new System.Random(seed);
            PlayerHp = config.PlayerMaxHp;
            StatOverrides = statOverrides;
        }

        public EnemySimConfig Config { get; }
        public System.Collections.Generic.IReadOnlyDictionary<EnemyKind, EnemyStatOverride> StatOverrides { get; }
        public float Time { get; private set; }
        public IReadOnlyList<Enemy> Enemies => _enemies;
        public IReadOnlyList<SimBullet> Bullets => _bullets;

        // —— 玩家状态 ——
        public Vec2 PlayerPosition { get; private set; }
        public float PlayerHp { get; private set; }
        public float PlayerMaxHp => Config.PlayerMaxHp;
        public bool PlayerDead => PlayerHp <= 0f;
        public float PlayerInvuln { get; private set; }

        /// <summary>当前护盾值（shield(n) 叠加；由驱动层在每个 tick 开始时清空——"持续 1 tick"）。</summary>
        public float PlayerShield { get; private set; }

        /// <summary>
        /// 玩家当前击退速度（方向 + 大小，单位/秒）。碰撞结算时产生，随后线性衰减到零；
        /// 位移由表现层应用（玩家位置归表现层所有），sim 只管方向与衰减——保证可单测。
        /// </summary>
        public Vec2 PlayerKnockback { get; private set; }

        private Vec2 _knockbackVelocity;
        private float _knockbackRemaining;

        /// <summary>表现层在每次 Step 前喂入主角最新位置（移动属于表现层职责）。</summary>
        public void SetPlayerPosition(Vec2 position) => PlayerPosition = position;

        public void HealPlayer(float amount)
        {
            if (PlayerDead)
                return;
            PlayerHp = System.Math.Min(PlayerMaxHp, PlayerHp + amount);
        }

        /// <summary>shield(n)：叠加护盾。先于血量吸收伤害，被完全吸收的攻击不触发无敌帧。</summary>
        public void ShieldPlayer(float amount)
        {
            if (PlayerDead || amount <= 0f)
                return;
            PlayerShield += amount;
        }

        /// <summary>tick 开始时清空护盾（持续 1 tick 的语义，由驱动层调用）。</summary>
        public void ClearPlayerShield()
        {
            PlayerShield = 0f;
        }

        /// <summary>
        /// 执行超窗的惩罚伤害（LootDesign.md §1）：按最大生命百分比直接扣血，
        /// 穿透护盾与无敌帧——堆护盾躲不掉超时惩罚。
        /// </summary>
        public void TimeoutPunishDamage(float maxHpFraction)
        {
            if (PlayerDead)
                return;
            float amount = PlayerMaxHp * maxHpFraction;
            PlayerHp = System.Math.Max(0f, PlayerHp - amount);
            _events.Add(new SimEvent { Type = SimEventType.PlayerHit, Position = PlayerPosition, Amount = amount });
        }

        // —— 出怪 ——

        public Enemy Spawn(EnemyKind kind, Vec2 position, int generation = 0)
        {
            Enemy e = EnemyArchetypes.Create(kind, position, generation, _rng, Config, StatOverrides);
            e.Id = _nextId++;
            _enemies.Add(e);
            Publish(SimEvent.FromEnemy(SimEventType.EnemySpawned, e));
            return e;
        }

        /// <summary>父进程按节拍要孩子：低于上限才孵，满员返回 null。</summary>
        public Enemy TrySpawnChild(Enemy parent)
        {
            if (CountChildren(parent) >= Config.ParentMaxChildren)
                return null;
            Vec2 offset = Vec2.FromAngle((float)_rng.NextDouble() * (float)System.Math.PI * 2f) * 0.8f;
            Enemy child = Spawn(EnemyKind.Zombie, parent.Position + offset);
            child.ParentId = parent.Id;
            return child;
        }

        public int CountChildren(Enemy parent)
        {
            int count = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (!e.Dead && e.ParentId == parent.Id && e.Id != parent.Id)
                    count++;
            }
            return count;
        }

        public Enemy NearestEnemy(Vec2 position)
        {
            Enemy best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e.Dead)
                    continue;
                float sqr = (e.Position - position).SqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>清场（验收台用）：全场死亡但不触发分裂/reap 逻辑，列表直接清空。</summary>
        public void Clear()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Dead)
                    continue;
                _enemies[i].Dead = true;
                Publish(SimEvent.FromEnemy(SimEventType.EnemyDied, _enemies[i]));
            }
            _enemies.Clear();
        }

        // —— 子弹 ——

        /// <summary>玩家侧子弹（attack() 从表现层进来）。direction 会被归一化。</summary>
        public SimBullet SpawnPlayerBullet(Vec2 origin, Vec2 direction, float damage, float hitRadius)
        {
            var b = new SimBullet
            {
                Id = _nextId++,
                Position = origin,
                Direction = direction.Normalized,
                Speed = Config.PlayerBulletSpeed,
                Damage = damage,
                HitRadius = hitRadius,
                FromPlayer = true,
            };
            _bullets.Add(b);
            return b;
        }

        /// <summary>敌人侧子弹（Brain 调用）。异常的子弹带异常类型标签，表现层据此着色。</summary>
        public SimBullet SpawnEnemyBullet(Enemy shooter, Vec2 target, float speed, float atk)
        {
            string label = shooter.Kind == EnemyKind.Exception
                ? ExceptionNames[_rng.Next(ExceptionNames.Length)]
                : null;
            var b = new SimBullet
            {
                Id = _nextId++,
                Position = shooter.Position,
                Direction = (target - shooter.Position).Normalized,
                Speed = speed,
                Damage = atk,
                HitRadius = Config.BulletHitRadius,
                FromPlayer = false,
                Label = label,
            };
            _bullets.Add(b);
            return b;
        }

        // —— 伤害结算 ——

        /// <summary>对敌结算一次伤害（受伤倍率在此应用），归零走 Kill。</summary>
        public void DamageEnemy(Enemy e, float amount)
        {
            if (e.Dead)
                return;
            e.Hp -= amount * e.DamageTakenMultiplier;
            if (e.Hp <= 0f)
                Kill(e, byDamage: true);
        }

        /// <summary>看门狗式范围爆炸：对玩家按无敌帧规则结算，随后自毁本体。</summary>
        public void Explode(Enemy self, float radius, float damage)
        {
            Publish(SimEvent.FromEnemy(SimEventType.Exploded, self));
            if (!PlayerDead && PlayerInvuln <= 0f
                && Vec2.Distance(self.Position, PlayerPosition) <= radius + Config.PlayerRadius)
            {
                DamagePlayer(damage);
            }
            Kill(self, byDamage: false);
        }

        /// <summary>事件出口：Step 完表现层 DrainEvents 消费（飘字/日志/特效）。</summary>
        public List<SimEvent> DrainEvents()
        {
            if (_events.Count == 0)
                return _events;    // 复用空列表，调用方只读不持有
            var drained = _events;
            _events = new List<SimEvent>();
            return drained;
        }

        public void Publish(SimEvent ev) => _events.Add(ev);

        private void DamagePlayer(float amount)
        {
            if (PlayerDead)
                return;

            // 护盾先吸收：完全吸收则不掉血、不给无敌帧（盾还没破）
            if (PlayerShield > 0f)
            {
                float absorbed = System.Math.Min(PlayerShield, amount);
                PlayerShield -= absorbed;
                amount -= absorbed;
                _events.Add(new SimEvent { Type = SimEventType.ShieldAbsorbed, Position = PlayerPosition, Amount = absorbed });
                if (amount <= 0f)
                    return;
            }

            PlayerHp = System.Math.Max(0f, PlayerHp - amount);
            PlayerInvuln = Config.PlayerInvulnTime;
            _events.Add(new SimEvent { Type = SimEventType.PlayerHit, Position = PlayerPosition, Amount = amount });
        }

        private void Kill(Enemy e, bool byDamage)
        {
            if (e.Dead)
                return;
            e.Dead = true;
            Publish(SimEvent.FromEnemy(SimEventType.EnemyDied, e));

            // 深拷贝（§2.5）：被击杀时分裂为两份下一世代拷贝，朝两侧弹开
            if (byDamage && e.Kind == EnemyKind.DeepCopy && e.Generation < 2)
            {
                Vec2 splitAxis = (e.Position - PlayerPosition).Normalized;
                Vec2 perp = new Vec2(-splitAxis.Y, splitAxis.X);
                for (int s = -1; s <= 1; s += 2)
                    Spawn(EnemyKind.DeepCopy, e.Position + perp * (s * 0.6f), e.Generation + 1);
            }

            // 父进程（§2.10）：死亡时全场僵尸进程被 reap
            if (e.Kind == EnemyKind.Parent)
            {
                for (int i = 0; i < _enemies.Count; i++)
                {
                    Enemy z = _enemies[i];
                    if (!z.Dead && z.ParentId == e.Id)
                        Kill(z, byDamage: false);
                }
                Publish(SimEvent.FromEnemy(SimEventType.ChildrenReaped, e));
            }
        }

        // —— 主循环 ——

        /// <summary>推进一步。顺序：计时（无敌帧/击退衰减）→ Brain → 子弹 → 接触结算 → 清尸。</summary>
        public void Step(float dt)
        {
            Time += dt;
            if (PlayerInvuln > 0f)
                PlayerInvuln -= dt;
            DecayKnockback(dt);

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e.Dead)
                    continue;
                e.Brain?.Tick(e, this, dt);
            }

            StepBullets(dt);
            SettleContact();
            _enemies.RemoveAll(IsDead);
        }

        private static bool IsDead(Enemy e) => e.Dead;

        private void StepBullets(float dt)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                SimBullet b = _bullets[i];
                b.Position += b.Direction * (b.Speed * dt);
                b.Age += dt;

                bool hit = false;
                if (b.FromPlayer)
                {
                    for (int j = 0; j < _enemies.Count; j++)
                    {
                        Enemy e = _enemies[j];
                        if (e.Dead)
                            continue;
                        if (Vec2.Distance(b.Position, e.Position) <= b.HitRadius + e.Radius)
                        {
                            DamageEnemy(e, b.Damage);
                            hit = true;
                            break;
                        }
                    }
                }
                else if (!PlayerDead && !b.PassedThroughPlayer
                    && Vec2.Distance(b.Position, PlayerPosition) <= b.HitRadius + Config.PlayerRadius)
                {
                    if (PlayerInvuln <= 0f)
                    {
                        DamagePlayer(b.Damage);
                        hit = true;
                    }
                    else
                    {
                        // 无敌帧中穿过：这颗弹对玩家作废，继续飞但不许回头补刀
                        b.PassedThroughPlayer = true;
                    }
                }

                if (hit || b.Age >= Config.BulletLifetime)
                    b.Dead = true;
            }
            _bullets.RemoveAll(IsBulletDead);
        }

        private static bool IsBulletDead(SimBullet b) => b.Dead;

        /// <summary>§0 碰撞结算：接触瞬间结算一次，受击后的全局无敌帧是**唯一**的受击间隔
        /// （挡住所有来源，含同时贴脸的多只怪）。结算同时给玩家远离该敌人的击退，
        /// 击退负责把玩家弹出重叠区，不再依赖每敌冷却。</summary>
        private void SettleContact()
        {
            if (PlayerDead)
                return;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e.Dead || e.Atk <= 0f)
                    continue;
                if (PlayerInvuln > 0f)
                    return;   // 全局无敌帧：唯一的受击间隔
                if (Vec2.Distance(e.Position, PlayerPosition) <= e.Radius + Config.PlayerRadius)
                {
                    DamagePlayer(e.Atk);
                    ApplyKnockback(PlayerPosition - e.Position);
                }
            }
        }

        private void ApplyKnockback(Vec2 away)
        {
            if (away.SqrMagnitude < 1e-6f)
                away = new Vec2(0f, 1f);   // 完全重合时方向未定义，给个固定弹开方向
            _knockbackVelocity = away.Normalized * Config.ContactKnockbackSpeed;
            _knockbackRemaining = Config.KnockbackTime;
            PlayerKnockback = _knockbackVelocity;
        }

        private void DecayKnockback(float dt)
        {
            if (_knockbackRemaining <= 0f)
            {
                PlayerKnockback = Vec2.Zero;
                return;
            }
            _knockbackRemaining -= dt;
            float k = System.Math.Max(0f, _knockbackRemaining / Config.KnockbackTime);
            PlayerKnockback = _knockbackVelocity * k;
        }
    }
}
