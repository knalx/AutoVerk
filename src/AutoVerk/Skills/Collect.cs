using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>Pick up the items lying around: all of them, or one kind. Skips any it can't reach or carry.</summary>
    public sealed class Collect : Composite
    {
        private const int MaxDrops = 100;

        private readonly float _radius;
        private readonly string _item;
        private readonly HashSet<ItemDrop> _skipped = new HashSet<ItemDrop>();

        /// <param name="item">An item id like Wood, or null for anything.</param>
        public Collect(float radius = 10, string item = null)
        {
            _radius = radius;
            _item = string.IsNullOrEmpty(item) ? null : item;
        }

        private bool Wanted(ItemDrop d) =>
            !d.IsPiece() && !_skipped.Contains(d) && (_item == null || Utils.GetPrefabName(d.gameObject) == _item);

        private static int Carried() => Senses.Player.GetInventory().GetAllItems().Sum(i => i.m_stack);

        protected override IEnumerable<Step> Run()
        {
            int before = Carried();
            for (int i = 0; i < MaxDrops; i++)
            {
                var drop = Senses.Nearest<ItemDrop>(_radius, Wanted);
                if (drop == null) break;
                Status = $"collecting {Utils.GetPrefabName(drop.gameObject)}";
                yield return Try(new MoveTo(drop, stopAt: 1f));
                // Walking close often picks it up already: the game's auto-pickup.
                if (drop == null) continue;
                if (Last.Ok) yield return Try(new Interact(drop));
                if (drop != null && !Last.Ok) _skipped.Add(drop);
            }
            Report = $"{Carried() - before} item(s) picked up" + (_skipped.Count > 0 ? $", {_skipped.Count} skipped" : "");
        }

        public override string ToString() => _item == null ? "collect" : $"collect {_item}";
    }
}
