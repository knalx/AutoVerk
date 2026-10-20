using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;
using AutoVerk.Skills.Farm;
using UnityEngine;

namespace AutoVerk.Skills
{
    /// <summary>Registers the skills and reflexes that come with AutoVerk.</summary>
    internal static class Builtins
    {
        public static void Register()
        {
            Registry.Register(new SkillInfo
            {
                Id = "wait",
                Description = "Do nothing for some seconds.",
                Params = { Param.Number("seconds", "how long", 3) },
                Create = args => new Wait(args.Number("seconds")),
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "move_to",
                Description = "Walk or run straight ahead some metres.",
                Params = { Param.Number("ahead", "metres in front of you", 10), Param.Bool("run", "run instead of walking") },
                Create = args => new MoveTo(Ahead(args.Number("ahead")), run: args.Bool("run")),
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "equip",
                Description = "Equip an item from the inventory.",
                Params = { Param.Item("item", "item id, like AxeStone") },
                Create = args => new Equip(args.Item("item")),
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "rest",
                Description = "Stand still until stamina is full.",
                Create = args => new Rest(),
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "strike",
                Description = "Swing the weapon in hand at the nearest enemy or tree in reach until it falls.",
                Params = { Param.Choice("target", "what to strike", "enemy", "tree"), Param.Number("radius", "metres", 4) },
                Create = args =>
                {
                    float radius = args.Number("radius");
                    Component target = args.Text("target") == "tree"
                        ? (Component)Senses.Nearest<TreeBase>(radius)
                        : Senses.Nearest<Character>(radius, Senses.IsEnemy);
                    return new Strike(target);
                },
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "mine",
                Description = "Break the nearest rock in reach with the pickaxe in hand, one part of a big one.",
                Params = { Param.Number("radius", "metres", 4) },
                Create = args =>
                {
                    var rock = Rocks.Find(Senses.Player.transform.position, args.Number("radius"), out var part);
                    return new Mine(rock, part);
                },
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "place",
                Description = "Place a piece from the tool in hand a few metres ahead.",
                Params = { Param.Text("piece", "piece id, like wood_floor"), Param.Number("ahead", "metres in front of you", 2) },
                Create = args => new Place(args.Text("piece"), Ahead(args.Number("ahead"))),
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "collect",
                Title = "Pick up items",
                Doing = "Picking up",
                StartText = "Start picking up",
                Description = "Picks up the items lying around you: everything, or one kind.",
                Params =
                {
                    Param.Number("radius", "metres", 10).Shows("Radius", hint: _ => "metres around you"),
                    Param.Item("item", "only this item, like Wood", "").WithSuggestions(() => Senses.Around<ItemDrop>(30)
                        .Where(d => !d.IsPiece()).Select(d => Utils.GetPrefabName(d.gameObject)).Distinct().OrderBy(n => n), "Anything")
                        .Shows("Item", hint: _ => "or one kind only"),
                },
                Create = args => new Collect(args.Number("radius"), args.Item("item")),
                // A building block now: gather picks up what falls.
                ForDevelopers = true,
            });
            Registry.Register(new SkillInfo
            {
                Id = "gather",
                Title = "Gather plants",
                Doing = "Gathering",
                StartText = "Start gathering",
                StopNote = "Stop keeps what's gathered.",
                Description = "Picks what's ripe around you: everything you can plant, everything wild, or one kind.",
                Params =
                {
                    Param.Item("plants", "what the plants give, like Raspberry; @wild for every wild one", "")
                        .WithSuggestions(() => new[] { Gather.Wild }.Concat(PlantData.RipeAround(50)), "Everything plantable")
                        .WithNames(Gather.What)
                        .Shows("Plants", hint: v => v.Text("plants") == "" ? "crops and seeds"
                            : v.Text("plants") == Gather.Wild ? "berries, mushrooms, thistle" : "only this kind"),
                    Param.Number("radius", "metres", 20).Shows("Radius", hint: _ => "metres around you"),
                    Param.Bool("loose", "Pick up stones and wood").Shows("Also", hint: _ => "lying around"),
                },
                Summary = Gather.Summary,
                Create = args => new Gather(args.Item("plants"), args.Number("radius"), args.Bool("loose")),
                Preview = args => new Gather(args.Item("plants"), args.Number("radius"), args.Bool("loose")).Preview(),
            });
            Registry.Register(new SkillInfo
            {
                Id = "plant",
                Title = "Plant a field",
                Doing = "Planting",
                StartText = "Start planting",
                StopNote = "Stop keeps what's planted. You can fill the rest later.",
                Description = "Clears the ground, tills it with the Cultivator and sows your crop.",
                Params =
                {
                    Param.Item("seed", "what to plant", "").WithSuggestions(PlantData.SeedsCarried)
                        .Shows("Seeds", "Crop", v => $"{Senses.Count(v.Item("seed"))} in your pack")
                        .ShownWhen(NewField),
                    Param.Text("field", "or fill up a saved one", "").WithSuggestions(Fields.Names, "New field")
                        .Shows("Field", "Field", v => NewField(v) ? "or pick a saved one" : "keeps its crop and size")
                        .WithButton("Delete", v => Fields.Delete(v.Text("field")), v => !NewField(v), confirm: true, clears: true),
                    Param.Number("count", "spots in a new field", 20)
                        .Shows("Spots", "Field", _ => "plants in total")
                        .ShownWhen(NewField),
                    Param.Number("line", "spots per line", 10)
                        .Shows("Per row", "Field", v => PlantField.Rows(v) is int rows ? $"= {rows} row{(rows == 1 ? "" : "s")}" : "")
                        .ShownWhen(NewField),
                    Param.Choice("layout", "how the lines sit", "staggered", "grid")
                        .Shows("Rows", "Field", v => v.Text("layout") == "grid" ? "spots lined up across rows" : "every other row shifted half a spot")
                        .ShownWhen(NewField),
                    Param.Bool("stones", "Pick up loose stones", true)
                        .Shows("Stones", "Clear the ground first", _ => "on each spot"),
                    Param.Bool("rocks", "Break rocks in the way")
                        .Shows("Rocks", "Clear the ground first", _ => "uses your pickaxe"),
                },
                Summary = PlantField.Summary,
                Create = Plant,
                Preview = args => Plant(args).Preview(),
            });

            Registry.Register(new FightBack());
        }

        private static bool NewField(FormValues v) => v.Text("field") == "";

        private static PlantField Plant(Args args) =>
            new PlantField(args.Item("seed"), args.Text("field"), (int)args.Number("count"), (int)args.Number("line"),
                args.Text("layout") == "staggered", args.Bool("stones"), args.Bool("rocks"));

        /// <summary>A point some metres in front of the player.</summary>
        private static Vector3 Ahead(float metres)
        {
            var player = Senses.Player;
            return player.transform.position + player.transform.forward * metres;
        }
    }
}
