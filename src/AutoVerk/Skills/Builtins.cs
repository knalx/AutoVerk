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
                Description = "Pick up the items lying around.",
                Params =
                {
                    Param.Number("radius", "metres", 10),
                    Param.Item("item", "only this item, like Wood", "").WithSuggestions(() => Senses.Around<ItemDrop>(30)
                        .Where(d => !d.IsPiece()).Select(d => Utils.GetPrefabName(d.gameObject)).Distinct().OrderBy(n => n), "anything"),
                },
                Create = args => new Collect(args.Number("radius"), args.Item("item")),
            });
            Registry.Register(new SkillInfo
            {
                Id = "plant",
                Description = "Plant a field with the Cultivator: clears stones and rocks in the way, cultivates and sows. " +
                              "A new field is laid out in front of you and saved; a saved one keeps its seed.",
                Params =
                {
                    Param.Item("seed", "what to plant", "").WithSuggestions(PlantData.SeedsCarried),
                    Param.Text("field", "or fill up a saved one", "").WithSuggestions(Fields.Names, "new"),
                    Param.Number("count", "spots in a new field", 20),
                    Param.Number("line", "spots per line", 10),
                    Param.Choice("layout", "how the lines sit", "staggered", "grid"),
                    Param.Bool("stones", "pick up stones on the spots", true),
                    Param.Bool("rocks", "break rocks in the way with a pickaxe"),
                },
                Create = Plant,
                Preview = args => Plant(args).Preview(),
            });

            Registry.Register(new FightBack());
        }

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
