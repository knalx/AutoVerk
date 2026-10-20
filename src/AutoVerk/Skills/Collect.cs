using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>Pick up the items lying around: all of them, or some kinds. Skips any it can't reach or carry.</summary>
    public sealed class Collect : Composite
    {
        private const int MaxDrops = 100;

        private readonly float _radius;
        private readonly HashSet<string> _items;
        private readonly HashSet<ItemDrop> _skipped = new HashSet<ItemDrop>();

        /// <param name="item">An item id like Wood, or null for anything.</param>
        public Collect(float radius = 10, string item = null)
            : this(radius, string.IsNullOrEmpty(item) ? null : new[] { item }) { }

        /// <param name="items">Item ids like Carrot and Raspberry, or null for anything.</param>
        public Collect(float radius, IEnumerable<string> items)
        {
            _radius = radius;
            _items = items != null ? new HashSet<string>(items) : null;
        }

        private bool Wanted(ItemDrop d) =>
            !d.IsPiece() && !_skipped.Contains(d) && (_items == null || _items.Contains(Utils.GetPrefabName(d.gameObject)));

        private static int Carried() => Senses.Player.GetInventory().GetAllItems().Sum(i => i.m_stack);

        protected override IEnumerable<Step> Run()
        {
            int before = Carried();
            Subject = $"{(_items == null ? "Everything" : string.Join(", ", _items.Select(Senses.ItemName)))} · within {_radius:0} m";
            for (int i = 0; i < MaxDrops; i++)
            {
                SetFact("Picked up", (Carried() - before).ToString());
                if (_skipped.Count > 0) SetFact("Skipped", _skipped.Count.ToString());
                var drop = Senses.Nearest<ItemDrop>(_radius, Wanted);
                if (drop == null) break;
                Status = $"picking up {Senses.ItemName(Utils.GetPrefabName(drop.gameObject))}";
                yield return Try(new MoveTo(drop, stopAt: 1f));
                // Walking close often picks it up already: the game's auto-pickup.
                if (drop == null) continue;
                if (Last.Ok) yield return Try(new Interact(drop));
                if (drop != null && !Last.Ok) _skipped.Add(drop);
            }
            SetFact("Picked up", (Carried() - before).ToString());
            if (_skipped.Count > 0) SetFact("Skipped", $"{_skipped.Count}, out of reach or too heavy");
            Report = $"{Carried() - before} item(s) picked up" + (_skipped.Count > 0 ? $", {_skipped.Count} skipped" : "");
        }

        public override string ToString() => _items == null ? "collect" : $"collect {string.Join(", ", _items)}";
    }
}
