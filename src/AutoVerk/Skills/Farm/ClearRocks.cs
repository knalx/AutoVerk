using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// Break the rocks within some metres of a set of spots with a pickaxe, nearest first: a solid thing within a
    /// plant's grow radius stops it growing. A big rock goes part by part. A part it can't break or reach is
    /// skipped and named in the report. Fails at once if there's a rock and no pickaxe at all.
    /// </summary>
    public sealed class ClearRocks : Composite
    {
        private const int MaxPasses = 200;
        private const int MaxTries = 3;
        /// <summary>The part refused for a reason a retry won't change.</summary>
        private static readonly HashSet<string> Final = new HashSet<string> { "no_tool", "missed" };

        private readonly List<Vector3> _spots;
        private readonly float _radius;
        private readonly Dictionary<Collider, string> _skipped = new Dictionary<Collider, string>();
        private readonly Dictionary<Collider, string> _names = new Dictionary<Collider, string>();
        private readonly Dictionary<Collider, int> _tries = new Dictionary<Collider, int>();

        public ClearRocks(IEnumerable<Vector3> spots, float radius)
        {
            _spots = spots.ToList();
            _radius = radius;
        }

        protected override IEnumerable<Step> Run()
        {
            int broken = 0;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                // Look: the rock nearest to the player on any spot, read afresh each pass.
                var rock = Nearest(out var part);
                if (rock == null) break;
                var name = Rocks.Name(rock);
                _names[part] = name;
                if (Senses.Pickaxe() == null)
                    yield return Fail("no_tool", $"no pickaxe in the inventory to break the {name}; take one, or uncheck rocks");
                Status = $"breaking {name}, {broken} broken";

                yield return Try(new BreakRock(rock, part));
                if (Last.Ok) broken++;
                else Refused(part, Last);
                // Something on the way it couldn't jump over: the run stops, nothing is broken on the way.
                if (_skipped.ContainsKey(part) && Skips.IsWalk(Last))
                {
                    Summarize(broken);
                    yield return Fail(Last.Code, $"can't get to the {name}: {Last.Detail}");
                }
            }
            Summarize(broken);
        }

        /// <summary>"Broke 2 pieces; left Rock ×2 (can't hit it from there)".</summary>
        private void Summarize(int broken) =>
            Report = $"Broke {broken} piece{(broken == 1 ? "" : "s")}" +
                     (_skipped.Count > 0 ? $"; left {Skips.Grouped(_skipped, p => _names[p])}" : "");

        private Component Nearest(out Collider part)
        {
            Component best = null;
            part = null;
            float bestDistance = float.MaxValue;
            var from = Senses.Player.transform.position;
            foreach (var spot in _spots)
            {
                var rock = Rocks.Find(spot, _radius, out var p, _skipped.ContainsKey);
                if (rock == null) continue;
                float d = Senses.FlatDistance(from, Rocks.ClosestPoint(p, from));
                if (d >= bestDistance) continue;
                best = rock;
                part = p;
                bestDistance = d;
            }
            return best;
        }

        private void Refused(Collider part, Result result)
        {
            _tries.TryGetValue(part, out var n);
            _tries[part] = ++n;
            if (Final.Contains(result.Code) || n >= MaxTries) _skipped[part] = Skips.Reason(result);
        }

        public override string ToString() => $"clear_rocks around {_spots.Count} spot(s)";
    }
}
