using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using HarmonyLib;

namespace AutoVerk.Ui
{
    /// <summary>The console's <c>autoverk</c> command: skills, run, pause, resume, stop, status.</summary>
    internal static class Commands
    {
        private const string Name = "autoverk";
        private const string Usage = "autoverk skills [all] | run <skill> [name=value …] | pause | resume | stop | status";

        public static void Register(Plugin plugin)
        {
            new Terminal.ConsoleCommand(Name, Usage, args => Run(plugin, args), optionsFetcher: () =>
                new List<string> { "skills", "run", "pause", "resume", "stop", "status" });
        }

        public static void Unregister() =>
            AccessTools.StaticFieldRefAccess<Terminal, Dictionary<string, Terminal.ConsoleCommand>>("commands").Remove(Name);

        private static void Run(Plugin plugin, Terminal.ConsoleEventArgs args)
        {
            var print = (System.Action<string>)args.Context.AddString;
            var verb = args.Args.Length > 1 ? args.Args[1] : "status";
            var runner = plugin.Runner;
            switch (verb)
            {
                case "skills":
                    bool all = args.Args.Length > 2 && args.Args[2] == "all";
                    foreach (var s in all ? Registry.AllSkills : Registry.Skills)
                        print($"{s.Usage}  · {(s.ForDevelopers ? "(dev) " : "")}{s.Description}");
                    if (!all) print("'autoverk skills all' lists the developer skills too");
                    break;
                case "run":
                    var line = string.Join(" ", args.Args.Skip(2));
                    var skill = Registry.FromLine(line, out var error);
                    if (skill == null) print(error);
                    else print(plugin.StartWhenFree(skill));
                    break;
                case "pause":
                    runner.Pause("paused from the console");
                    print(Describe(runner));
                    break;
                case "resume":
                    print(plugin.ResumeWhenFree());
                    break;
                case "stop":
                    runner.Stop("stopped from the console");
                    print(Describe(runner));
                    break;
                case "status":
                    print(Describe(runner));
                    break;
                default:
                    print(Usage);
                    break;
            }
        }

        public static string Describe(Runner runner)
        {
            switch (runner.State)
            {
                case RunState.Running: return $"running: {runner.Path}{(runner.Status != null ? " · " + runner.Status : "")}";
                case RunState.Paused: return $"paused ({runner.PauseReason}): {runner.Path}";
                case RunState.Finished: return $"finished: {runner.Root} → {runner.Outcome}{(runner.Root.Report != null ? " · " + runner.Root.Report : "")}";
                default: return "idle";
            }
        }
    }
}
