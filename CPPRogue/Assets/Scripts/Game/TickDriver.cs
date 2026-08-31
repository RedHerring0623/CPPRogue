using System.Collections;
using System.Collections.Generic;
using CPPRogue.Core.Code;
using CPPRogue.Core.Code.Runtime;
using CPPRogue.Core.Combat;
using CPPRogue.Core.Computing;
using UnityEngine;

namespace CPPRogue.Game
{
    /// <summary>
    /// TickDriver：Unity 表现层的调度器。每 TickInterval 秒开始一个 tick；
    /// 语句间 StatementInterval 秒（逐句演出 + HUD 高亮）。
    ///
    /// 执行时间窗（LootDesign.md §1，基础 1s + 局外时间片兑换）：
    /// 窗内跑完 → 下一 tick = max(固定间隔, 跑完时刻)；
    /// 超窗 → 3s 宽限（移速线性降到 0，每秒 10% 最大生命，穿盾穿无敌帧）；
    /// 宽限耗尽 → 强杀执行（kill -9）+ 眩晕 3s（不调度任何代码，怪物照常伤害）。
    /// 支持热替换 Routine（ESC 编辑后继续）——变量黑板保留，当前 tick 从头开始。
    /// </summary>
    public sealed class TickDriver : MonoBehaviour
    {
        public float TickInterval = 3f;
        public float StatementInterval = 0.2f;

        /// <summary>执行时间窗（秒）：单次 tick 允许占用的墙钟时间，来自局外成长（MetaProgress）。</summary>
        public float ExecutionWindowSeconds = 1f;

        private const float GraceSeconds = 3f;               // 超窗宽限（固定常数，不随材料增长）
        private const float GraceDamagePerSecond = 0.1f;     // 宽限期每秒最大生命百分比
        private const float StunSeconds = 3f;

        private readonly Interpreter _interpreter = new Interpreter();
        private ExecContext _ctx;
        private Routine _routine;
        private RoutineHud _hud;
        private PlayerController _player;
        private float _baseSpeed;
        private Coroutine _loop;

        public void Setup(Routine routine, ICombatWorld world, RoutineHud hud, PlayerController player)
        {
            _routine = routine;
            _hud = hud;
            _player = player;
            _baseSpeed = player != null ? player.Speed : 0f;
            _ctx = new ExecContext(
                world,
                BuiltinTable.CreateDefault(),
                new CpuBudget(32),
                new Blackboard(),
                null,
                null,
                new PlayerPropertySource(player));
            _loop = StartCoroutine(TickLoop());
        }

        /// <summary>热替换 Routine（ESC 编辑后的继续）。</summary>
        public void ReplaceRoutine(Routine routine)
        {
            _routine = routine;
            if (_loop != null)
                StopCoroutine(_loop);
            _loop = StartCoroutine(TickLoop());
        }

        private IEnumerator TickLoop()
        {
            var betweenStatements = new WaitForSeconds(StatementInterval);
            while (true)
            {
                // 局已结束（死亡/撤离结算）：不再调度新 tick
                while (GameRun.Over)
                    yield return null;

                _ctx.World.BeginTick();   // "持续 1 tick" 的护盾在这里清空
                _ctx.Tick++;
                float startedAt = Time.time;
                float lastGraceElapsed = 0f;
                bool killed = false;

                IEnumerator<StepInfo> steps = _interpreter.Execute(_routine, _ctx).GetEnumerator();
                while (true)
                {
                    float graceElapsed = Time.time - startedAt - ExecutionWindowSeconds;
                    if (graceElapsed >= GraceSeconds)
                    {
                        killed = true;   // 宽限耗尽：强杀（kill -9），走眩晕
                        break;
                    }
                    if (graceElapsed > 0f)
                    {
                        ApplyGrace(graceElapsed, ref lastGraceElapsed);
                    }
                    else if (lastGraceElapsed > 0f)
                    {
                        // 宽限内跑完：惩罚即止，移速恢复
                        lastGraceElapsed = 0f;
                        RestoreSpeed();
                    }

                    if (!steps.MoveNext())
                        break;
                    StepInfo step = steps.Current;
                    if (_hud != null)
                        _hud.ApplyStep(step);
                    UpdateStatus(step);
                    if (graceElapsed > 0f && _hud != null)
                        _hud.SetStatus($"Tick {_ctx.Tick}  |  超窗！宽限 {GraceSeconds - graceElapsed:0.0}s  |  移速下降 · 正在丢血");
                    yield return betweenStatements;
                }
                steps.Dispose();

                if (killed)
                {
                    if (_hud != null)
                    {
                        _hud.Log("SIGKILL: 执行超出时间窗，进程被强制终止 —— 眩晕 3s");
                        _hud.ClearHighlight();
                    }
                    UpdateStatus(null);
                    yield return Stun();
                }
                else
                {
                    if (_hud != null)
                        _hud.ClearHighlight();
                    UpdateStatus(null);
                }

                float total = Time.time - startedAt;
                yield return new WaitForSeconds(Mathf.Max(0f, TickInterval - total));
            }
        }

        /// <summary>宽限惩罚：移速线性降到 0 + 按经过时间比例扣最大生命（穿盾穿无敌帧）。</summary>
        private void ApplyGrace(float graceElapsed, ref float lastGraceElapsed)
        {
            if (_player != null)
                _player.Speed = _baseSpeed * Mathf.Clamp01(1f - graceElapsed / GraceSeconds);
            float delta = graceElapsed - lastGraceElapsed;
            lastGraceElapsed = graceElapsed;
            if (delta > 0f)
                _ctx.World.TimeoutPunish(GraceDamagePerSecond * delta);
        }

        private IEnumerator Stun()
        {
            if (_player != null)
                _player.Speed = 0f;
            if (_hud != null)
                _hud.SetStatus("眩晕中 —— 本 tick 已被 kill -9，代码停摆 3s");
            float end = Time.time + StunSeconds;
            while (Time.time < end && !GameRun.Over)
                yield return null;
            RestoreSpeed();
        }

        private void RestoreSpeed()
        {
            if (_player != null)
                _player.Speed = _baseSpeed;
        }

        private void UpdateStatus(StepInfo step)
        {
            if (_hud == null)
                return;
            string current;
            if (step == null)
                current = "-";
            else if (step.Status == StepStatus.Executed)
                current = "OK";
            else if (step.Status == StepStatus.OptimizedOut)
                current = "skipped";
            else
                current = "HUNG";
            _hud.SetStatus($"Tick {_ctx.Tick}  |  Cycles {_ctx.Budget.Remaining}/{_ctx.Budget.Capacity}  |  {current}");
        }
    }
}
