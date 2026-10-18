using System;

namespace AutoVerk.Engine
{
    /// <summary>
    /// A skill that works the game directly, every frame. Set the controls you need in every <see cref="Tick"/>:
    /// they last one frame.
    /// </summary>
    public abstract class Primitive : Skill
    {
        /// <summary>Once at the start, and again after a pause. Check the target here and fail early.</summary>
        protected virtual Result Begin() => Result.Running;

        /// <summary>Every frame until it returns a result other than <see cref="Result.Running"/>.</summary>
        protected abstract Result Tick();

        /// <summary>Once, however it ended (ok, failed, interrupted, cancelled).</summary>
        protected virtual void End() { }

        internal sealed override Result OnStart() => Begin();
        internal sealed override Result OnTick() => Tick();
        internal sealed override void OnStop() => End();

        internal sealed override void OnInterrupt()
        {
            End();
            Restart();
        }
    }

    /// <summary>Do nothing for some seconds.</summary>
    public sealed class Wait : Primitive
    {
        private readonly double _seconds;

        public Wait(double seconds) { _seconds = seconds; }

        protected override Result Tick()
        {
            Status = $"waiting {Math.Max(0, _seconds - Elapsed):0} s";
            return Elapsed >= _seconds ? Result.Success : Result.Running;
        }

        public override string ToString() => $"wait {_seconds:0.#} s";
    }

    /// <summary>Wait until a condition is true; fails with timeout after <c>timeout</c> seconds.</summary>
    public sealed class WaitUntil : Primitive
    {
        private readonly Func<bool> _condition;
        private readonly double _timeout;

        public WaitUntil(Func<bool> condition, double timeout = 30) { _condition = condition; _timeout = timeout; }

        protected override Result Tick()
        {
            if (_condition()) return Result.Success;
            return Elapsed > _timeout ? Result.Fail("timeout", $"still waiting after {_timeout:0} s") : Result.Running;
        }
    }
}
