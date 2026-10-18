using System.Collections.Generic;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>
    /// Pick everything pickable around you, nearest first: berries, mushrooms, stones, ripe crops. Skips any it
    /// can't reach, and stops when the inventory is full.
    /// </summary>
    public sealed class Pick : Composite
    {
        private const int MaxPicks = 200;

        private readonly float _radius;
        private readonly string _item;
        private readonly HashSet<Pickable> _skipped = new HashSet<Pickable>();

        /// <param name="item">The id of the item it gives, like Raspberry, or null for any.</param>
        public Pick(float radius = 20, string item = null)
        {
            _radius = radius;
            _item = string.IsNullOrEmpty(item) ? null : item;
        }

        private bool Wanted(Pickable p) =>
            !p.GetPicked() && p.m_itemPrefab != null && !_skipped.Contains(p) && (_item == null || p.m_itemPrefab.name == _item);

        protected override IEnumerable<Step> Run()
        {
            int picked = 0;
            for (int i = 0; i < MaxPicks; i++)
            {
                var target = Senses.Nearest<Pickable>(_radius, Wanted);
                if (target == null) break;
                Status = $"picking {target.m_itemPrefab.name} ({picked} so far)";
                yield return Try(new MoveTo(target));
                if (Last.Ok) yield return Try(new Interact(target));
                if (Last.Ok) picked++;
                else if (Last.Code == "inventory_full") yield return Fail("inventory_full", $"inventory full after {picked} picked");
                else _skipped.Add(target);
            }
            if (picked == 0 && _skipped.Count == 0)
                yield return Fail("not_found", $"nothing to pick within {_radius:0} m");
            Report = $"{picked} picked" + (_skipped.Count > 0 ? $", {_skipped.Count} skipped" : "");
        }

        public override string ToString() => _item == null ? "pick" : $"pick {_item}";
    }
}
