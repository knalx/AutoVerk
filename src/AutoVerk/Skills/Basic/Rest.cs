using AutoVerk.Engine;
using AutoVerk.Game;

namespace AutoVerk.Skills.Basic
{
    /// <summary>Stand still until stamina is full.</summary>
    public sealed class Rest : Primitive
    {
        private const float Timeout = 60f;

        protected override Result Tick()
        {
            var p = Senses.Player;
            float now = p.GetStamina(), max = p.GetMaxStamina();
            if (now >= max - 0.5f) return Result.Success;
            if (Elapsed > Timeout) return Result.Fail("timeout", $"stamina {now:0}/{max:0} after {Timeout:0} s");
            Status = $"resting until stamina is back: {now:0} of {max:0}";
            return Result.Running;
        }
    }
}
