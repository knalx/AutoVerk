using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

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
            });
            Registry.Register(new SkillInfo
            {
                Id = "move_to",
                Description = "Walk or run straight ahead some metres.",
                Params = { Param.Number("ahead", "metres in front of you", 10), Param.Bool("run", "run instead of walking") },
                Create = args =>
                {
                    var player = Senses.Player;
                    return new MoveTo(player.transform.position + player.transform.forward * args.Number("ahead"), run: args.Bool("run"));
                },
            });
        }
    }
}
