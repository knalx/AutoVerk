using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// Break the rocks within some metres of a set of spots with a pickaxe, nearest first: a solid thing within a
    /// plant's grow radius stops it growing. Takes the pickaxe each rock needs; a rock it can't break or reach is
    /// skipped and named in the report.
    /// </summary>
    public sealed class ClearRocks : Composite
    {
        private const int MaxPasses = 200;
        private const int MaxTries = 3;
        /// <summary>Stand this far from the rock's surface to swing.</summary>
        private const float StandOff = 0.5f;
        /// <summary>The rock refused for a reason a retry won't change.</summary>
        private static readonly HashSet<string> Final = new HashSet<string> { "no_tool", "missed" };

        private readonly List<Vector3> _spots;
        private readonly float _radius;
        private readonly Dictionary<GameObject, string> _skipped = new Dictionary<GameObject, string>();
        private readonly Dictionary<GameObject, int> _tries = new Dictionary<GameObject, int>();

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
                var go = rock.gameObject;
                var name = Utils.GetPrefabName(go);
                int tier = Rocks.Tier(rock);
                var pickaxe = Senses.Pickaxe(tier);
                if (pickaxe == null)
                {
                    _skipped[go] = Senses.Pickaxe() == null ? $"{name}, no pickaxe" : $"{name}, needs a tier {tier} pickaxe";
                    continue;
                }
                Status = $"breaking {name}, {broken} broken";

                yield return Do(new Equip(pickaxe));
                if (!Senses.Player.HaveStamina(pickaxe.m_shared.m_attack.m_attackStamina)) yield return Try(new Rest());
                var from = Senses.Player.transform.position;
                var surface = Rocks.NearestSurface(Rocks.Colliders(rock, part), from);
                var away = from - surface;
                away.y = 0f;
                if (away.magnitude > StandOff + 0.4f)
                {
                    yield return Try(new MoveTo(surface + away.normalized * StandOff, stopAt: 0.35f));
                    if (!Last.Ok)
                    {
                        Refused(go, name, Last);
                        continue;
                    }
                }

                yield return Try(new Mine(rock, part));
                if (Last.Ok) broken++;
                else if (Last.Code != "gone")
                {
                    Refused(go, name, Last);
                    if (Last.Code == "no_stamina") yield return Try(new Rest());
                }
            }
            Report = $"{broken} rock part(s) broken"
                     + (_skipped.Count > 0 ? $"; skipped {string.Join(", ", _skipped.Values)}" : "");
        }

        private Component Nearest(out Collider part)
        {
            Component best = null;
            part = null;
            float bestDistance = float.MaxValue;
            var from = Senses.Player.transform.position;
            foreach (var spot in _spots)
            {
                var rock = Rocks.Find(spot, _radius, out var p, r => _skipped.ContainsKey(r.gameObject));
                if (rock == null) continue;
                float d = Senses.FlatDistance(from, Rocks.ClosestPoint(p, from));
                if (d >= bestDistance) continue;
                best = rock;
                part = p;
                bestDistance = d;
            }
            return best;
        }

        private void Refused(GameObject rock, string name, Result result)
        {
            _tries.TryGetValue(rock, out var n);
            _tries[rock] = ++n;
            if (Final.Contains(result.Code) || n >= MaxTries) _skipped[rock] = $"{name}, {result.Detail ?? result.Code}";
        }

        public override string ToString() => $"clear_rocks around {_spots.Count} spot(s)";
    }
}
