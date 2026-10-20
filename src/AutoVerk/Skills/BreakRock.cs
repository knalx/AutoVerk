using System.Collections.Generic;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>
    /// Break one rock, or one part of a big one: take the weakest pickaxe that can, walk up to it, mine it. Rests
    /// when stamina runs out.
    /// </summary>
    public sealed class BreakRock : Composite
    {
        private const int MaxTries = 3;
        /// <summary>Stand this far from the rock's surface to swing.</summary>
        private const float StandOff = 0.5f;

        private readonly UnityEngine.Component _rock;
        private readonly UnityEngine.Collider _part;

        public BreakRock(UnityEngine.Component rock, UnityEngine.Collider part = null)
        {
            _rock = rock;
            _part = part;
        }

        protected override IEnumerable<Step> Run()
        {
            if (_rock == null) yield break;
            var name = Rocks.Name(_rock);
            int tier = Rocks.Tier(_rock);
            if (Senses.Pickaxe() == null) yield return Fail("no_tool", "no pickaxe in the inventory");
            var pickaxe = Senses.Pickaxe(tier);
            if (pickaxe == null) yield return Fail("no_tool", $"needs a tier {tier} pickaxe");
            Status = $"breaking {name}";

            yield return Do(new Equip(pickaxe));
            var from = Senses.Player.transform.position;
            var surface = Rocks.NearestSurface(Rocks.Colliders(_rock, _part), from);
            var away = from - surface;
            away.y = 0f;
            var walk = Result.Success;
            if (away.magnitude > StandOff + 0.4f)
            {
                // Stuck against the rock itself is still close enough to swing: mine says when it isn't.
                yield return Try(new MoveTo(surface + away.normalized * StandOff, stopAt: 0.35f, name: name));
                walk = Last;
            }

            for (int i = 0; i < MaxTries; i++)
            {
                if (!Senses.Player.HaveStamina(pickaxe.m_shared.m_attack.m_attackStamina)) yield return Try(new Rest());
                yield return Try(new Mine(_rock, _part));
                if (Last.Code != "no_stamina") break;
            }
            if (Last.Code == "too_far" && walk.Failed) yield return Fail(walk.Code, walk.Detail);
            // Gone is fine: it broke some other way.
            if (!Last.Ok && Last.Code != "gone") yield return Fail(Last.Code, Last.Detail);
        }

        public override string ToString() => $"break_rock {Rocks.Name(_rock)}";
    }
}
