using System;
using System.Collections.Generic;

namespace AutoVerk.Engine
{
    /// <summary>One step of a composite: run a skill, end with a failure, or ask for a pause.</summary>
    public sealed class Step
    {
        internal Skill Skill;
        internal bool MustSucceed;
        internal Result Failure;
        internal string PauseReason;
    }

    /// <summary>
    /// A skill that runs other skills. Write <see cref="Run"/> as an iterator that yields steps; returning from
    /// it ends the skill ok. <c>yield return null</c> waits one frame.
    /// </summary>
    public abstract class Composite : Skill
    {
        private IEnumerator<Step> _steps;
        private Step _step;

        /// <summary>The result of the last skill that ended.</summary>
        protected Result Last { get; private set; } = Result.Success;

        protected abstract IEnumerable<Step> Run();

        /// <summary>Run a skill; if it fails, this skill fails with the same result.</summary>
        protected static Step Do(Skill skill) => new Step { Skill = skill, MustSucceed = true };

        /// <summary>Run a skill and carry on either way; its result is in <see cref="Last"/>.</summary>
        protected static Step Try(Skill skill) => new Step { Skill = skill };

        protected static Step Wait(double seconds) => Do(new Wait(seconds));

        protected static Step WaitUntil(Func<bool> condition, double timeout = 30) => Do(new WaitUntil(condition, timeout));

        /// <summary>End this skill, failed. The method doesn't carry on after it.</summary>
        protected static Step Fail(string code, string detail = null) => new Step { Failure = Result.Fail(code, detail) };

        /// <summary>Pause the run for the player to decide; carry on after Resume.</summary>
        protected static Step Pause(string reason) => new Step { PauseReason = reason };

        internal override Skill Child => _step?.Skill;

        internal sealed override Result OnStart()
        {
            _steps = Run().GetEnumerator();
            return Advance();
        }

        internal sealed override Result OnTick()
        {
            var child = _step?.Skill;
            if (child != null)
            {
                var result = child.Step(CurrentRun, Depth + 1);
                if (result.IsRunning) return Result.Running;
                Last = result;
                if (_step.MustSucceed && result.Failed) return result;
            }
            return Advance();
        }

        /// <summary>Takes the next step. Its skill starts on the next frame, so a loop of instant failures can't hang a frame.</summary>
        private Result Advance()
        {
            if (!_steps.MoveNext())
            {
                _step = null;
                return Result.Success;
            }
            _step = _steps.Current;
            if (_step?.Failure != null) return _step.Failure;
            if (_step?.PauseReason != null) CurrentRun.PauseRequest = _step.PauseReason;
            return Result.Running;
        }

        internal sealed override void OnInterrupt() => _step?.Skill?.Interrupt();

        internal sealed override void OnStop()
        {
            _step?.Skill?.Cancel();
            _steps?.Dispose();
        }
    }
}
