using System.Collections.Generic;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>Walk to the nearest pickable (berries, stones, ripe crops) and pick it.</summary>
    public sealed class Pick : Composite
    {
        private readonly float _radius;
        private readonly string _item;

        /// <param name="item">The id of the item it gives, like Raspberry, or null for any.</param>
        public Pick(float radius = 20, string item = null)
        {
            _radius = radius;
            _item = string.IsNullOrEmpty(item) ? null : item;
        }

        protected override IEnumerable<Step> Run()
        {
            var target = Senses.Nearest<Pickable>(_radius, p =>
                !p.GetPicked() && p.m_itemPrefab != null && (_item == null || p.m_itemPrefab.name == _item));
            if (target == null) yield return Fail("not_found", $"nothing to pick within {_radius:0} m");
            yield return Do(new MoveTo(target));
            yield return Do(new Interact(target));
        }

        public override string ToString() => _item == null ? "pick" : $"pick {_item}";
    }
}
