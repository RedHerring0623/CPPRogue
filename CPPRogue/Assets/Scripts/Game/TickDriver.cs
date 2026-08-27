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
    /// TickDriver：Unity 表现层的调度器（DemoUI 蓝本的正式版）。
    /// 每 TickInterval 秒开始一个 tick；语句间 StatementInterval 秒（逐句演出 + HUD 高亮）；
    /// 下一 tick = max(固定间隔, 程序跑完时刻)。
    /// 支持热替换 Routine（ESC 编辑后继续）——变量黑板保留，当前 tick 从头开始。
    /// </summary>
    public sealed class TickDriver : MonoBehaviour
    {
        public float TickInterval = 3f;
        public float StatementInterval = 0.2f;

        private readonly Interpreter _interpreter = new Interpreter();
        private ExecContext _ctx;
        private Routine _routine;
        private RoutineHud _hud;
        private Coroutine _loop;

        public void Setup(Routine routine, ICombatWorld world, RoutineHud hud, PlayerController player)
        {
            _routine = routine;
            _hud = hud;
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
                // 进程已终止：不再调度新 tick（死亡弹窗在场；当前 tick 演完即停）
                while (GameRun.Over)
                    yield return null;

                _ctx.Tick++;
                float startedAt = Time.time;

                // 步骤机：拉一步 = 执行一条语句；等 StatementInterval 再拉下一步
                foreach (StepInfo step in _interpreter.Execute(_routine, _ctx))
                {
                    if (_hud != null)
                        _hud.ApplyStep(step);
                    UpdateStatus(step);
                    yield return betweenStatements;
                }
                if (_hud != null)
                    _hud.ClearHighlight();
                UpdateStatus(null);

                // max 规则：程序没跑完绝不开始下一个 tick
                float elapsed = Time.time - startedAt;
                float wait = Mathf.Max(0f, TickInterval - elapsed);
                yield return new WaitForSeconds(wait);
            }
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
