using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// Gather around you, nearest first: everything plantable that's ripe (crops, and seeds like barley and flax),
    /// everything wild that grows back by itself (berries, mushrooms), or one kind of plant (by the item it gives,
    /// like Raspberry); if asked, the loose stones and branches too.
    /// Then picks up what fell on the ground. Leaves what's under water; skips a plant it can't get to or pick;
    /// stops when the pack is full.
    /// </summary>
    public sealed class Gather : Composite
    {
        private const int MaxPlants = 300;

        /// <summary>The plants value for every wild plant that grows back by itself.</summary>
        public const string Wild = "@wild";

        private readonly string _item;
        private readonly float _radius;
        private readonly bool _loose;
        private readonly HashSet<Pickable> _skipped = new HashSet<Pickable>();
        private readonly Dictionary<Pickable, string> _reasons = new Dictionary<Pickable, string>();

        /// <param name="item">What the plants give, like Raspberry, or null for everything plantable.</param>
        /// <param name="loose">Also pick up loose stones and branches, and stone and wood on the ground.</param>
        public Gather(string item, float radius, bool loose = false)
        {
            _item = string.IsNullOrEmpty(item) ? null : item;
            _radius = radius;
            _loose = loose;
        }

        /// <summary>What a gather of this kind takes: a ripe plant of the kind, or a loose stone or branch.</summary>
        private static bool Matches(Pickable p, string item, bool loose)
        {
            if (p?.m_itemPrefab == null || !PlantData.Reachable(p)) return false;
            if (loose && PlantData.IsLoose(p)) return true;
            if (item == null) return p.CanBePicked() && PlantData.IsPlantable(p);
            if (item == Wild) return PlantData.IsWild(p);
            return PlantData.IsRipePlant(p) && PlantData.Yield(p) == item;
        }

        private bool Wanted(Pickable p) => !_skipped.Contains(p) && Matches(p, _item, _loose);

        private static int Carried(string item) => Senses.Count(item);

        /// <summary>How the window says a plants value: the two groups, or the item's name.</summary>
        public static string What(string item) =>
            string.IsNullOrEmpty(item) ? "Everything plantable" : item == Wild ? "Everything wild" : Senses.ItemName(item);

        protected override IEnumerable<Step> Run()
        {
            Subject = $"{What(_item)}{(_loose ? ", stones and wood" : "")} · within {_radius:0} m";
            AgainLine = $"gather plants=\"{_item ?? ""}\" radius={_radius:0} loose={(_loose ? "yes" : "no")}";
            AgainLabel = "Gather again";
            // How many of each kind it carried before its first plant of that kind: the rest is gathered.
            var before = new Dictionary<string, int>();
            bool full = false;

            for (int i = 0; i < MaxPlants; i++)
            {
                SetFact("Gathered", Total(before).ToString());
                SetFact("Left", Senses.Around<Pickable>(_radius).Count(Wanted).ToString());
                if (_skipped.Count > 0) SetFact("Skipped", _skipped.Count.ToString());
                // Look: the nearest ripe plant, read afresh each pass.
                var plant = Senses.Nearest<Pickable>(_radius, Wanted);
                if (plant == null) break;
                var item = PlantData.Yield(plant);
                var name = Senses.ItemName(item);
                if (!Senses.Player.GetInventory().CanAddItem(plant.m_itemPrefab, plant.m_amount))
                {
                    full = true;
                    break;
                }
                if (!before.ContainsKey(item)) before[item] = Carried(item);

                Status = PlantData.IsLoose(plant) ? $"picking up {name}" : $"gathering {name}";
                yield return Try(new MoveTo(plant));
                if (Last.Ok && plant != null) yield return Try(new Interact(plant));
                if (plant != null && !Last.Ok)
                {
                    _skipped.Add(plant);
                    _reasons[plant] = Skips.Reason(Last);
                }
            }

            // What fell next to the plants and the auto-pickup didn't get, and with loose on, stone and wood.
            var lying = new HashSet<string>(before.Keys);
            if (_loose) lying.UnionWith(PlantData.LooseItems);
            if (!full && lying.Count > 0)
            {
                foreach (var item in lying.Where(k => !before.ContainsKey(k)).ToList()) before[item] = Carried(item);
                Status = "picking up what's on the ground";
                yield return Try(new Collect(_radius, lying));
            }

            ClearFacts();
            int total = Total(before);
            SetFact("Gathered", total == 0 ? "nothing"
                : $"{total}: {string.Join(", ", before.Keys.Where(k => Carried(k) > before[k]).Select(k => $"{Senses.ItemName(k)} {Carried(k) - before[k]}"))}");
            if (_skipped.Count > 0)
                SetFact("Skipped", string.Join(", ", _reasons.Values.GroupBy(r => r).Select(g => $"{g.Count()} {g.Key}")));
            Headline = full ? "Stopped: your pack is full"
                : total == 0 && _skipped.Count == 0 ? "Nothing ripe here"
                : null;
            Advice = full ? "Empty your pack, then gather again." : null;
            Report = string.Join(" · ", new[] { Headline }.Where(h => h != null).Concat(Facts.Select(f => $"{f.Key}: {f.Value}")));
        }

        /// <summary>Items gained so far, of every kind gathered.</summary>
        private static int Total(Dictionary<string, int> before) =>
            before.Sum(b => System.Math.Max(0, Carried(b.Key) - b.Value));

        /// <summary>Green poles on the plants it would gather, yellow on the stones and branches.</summary>
        public string Preview()
        {
            Markers.Hide();
            var found = Senses.Around<Pickable>(_radius).Where(Wanted).ToList();
            int loose = 0;
            foreach (var p in found)
            {
                bool isLoose = PlantData.IsLoose(p);
                if (isLoose) loose++;
                Markers.Pole(p.transform.position, isLoose ? Markers.Clear : Markers.Free, Utils.GetPrefabName(p.gameObject));
            }
            if (found.Count == 0) return "Nothing ripe to gather here";
            return $"<color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(Markers.Free)}>{found.Count - loose} plants</color>" +
                   (loose > 0 ? $" · <color=#{UnityEngine.ColorUtility.ToHtmlStringRGB(Markers.Clear)}>{loose} stones and branches</color>" : "");
        }

        /// <summary>The window's box: how many it would gather, and whether there's room for them.</summary>
        public static FormSummary Summary(FormValues v)
        {
            var item = v.Item("plants");
            item = item == "" ? null : item;
            float radius = v.Number("radius") ?? 0f;
            bool loose = v.Bool("loose");
            var found = Senses.Around<Pickable>(radius).Where(p => Matches(p, item, loose)).ToList();
            int stones = found.Count(PlantData.IsLoose);
            string text = $"{found.Count - stones} × {What(item)}" + (loose ? $" · {stones} stones and branches" : "") + $" · within {radius:0} m";
            string warning = null;
            if (found.Count == 0) warning = "Nothing ripe to gather here";
            else if (!Senses.Player.GetInventory().CanAddItem(found[0].m_itemPrefab, found[0].m_amount)) warning = "Your pack is full";
            return new FormSummary { Text = text, Warning = warning };
        }

        public override string ToString() => _item == null ? "gather" : $"gather {_item}";
    }
}
