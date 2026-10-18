using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoVerk.Engine
{
    public enum ParamKind { Number, Text, Bool, Item, Choice }

    /// <summary>One parameter of a registered skill. Without a default it's required.</summary>
    public sealed class Param
    {
        public string Name { get; private set; }
        public ParamKind Kind { get; private set; }
        public string Description { get; private set; }
        public string Default { get; private set; }
        /// <summary>Choice only.</summary>
        public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>();

        public bool Required => Default == null;

        /// <summary>Values to offer in a dropdown (the seeds you carry), read when the window opens. Typing others is fine.</summary>
        public Func<IEnumerable<string>> Suggestions { get; private set; }

        /// <summary>What the dropdown says for an empty value: "new field here", "anything".</summary>
        public string EmptyText { get; private set; }

        /// <summary>Offer these values in the window's dropdown.</summary>
        public Param WithSuggestions(Func<IEnumerable<string>> suggestions, string emptyText = null)
        {
            Suggestions = suggestions;
            EmptyText = emptyText;
            return this;
        }

        public static Param Number(string name, string description, double? fallback = null) =>
            new Param { Name = name, Kind = ParamKind.Number, Description = description, Default = fallback?.ToString(CultureInfo.InvariantCulture) };

        public static Param Text(string name, string description, string fallback = null) =>
            new Param { Name = name, Kind = ParamKind.Text, Description = description, Default = fallback };

        public static Param Bool(string name, string description, bool fallback = false) =>
            new Param { Name = name, Kind = ParamKind.Bool, Description = description, Default = fallback ? "yes" : "no" };

        /// <summary>An inventory item's id, like CarrotSeeds.</summary>
        public static Param Item(string name, string description, string fallback = null) =>
            new Param { Name = name, Kind = ParamKind.Item, Description = description, Default = fallback };

        public static Param Choice(string name, string description, params string[] options) =>
            new Param { Name = name, Kind = ParamKind.Choice, Description = description, Default = options.FirstOrDefault(), Options = options };

        public override string ToString() => Required ? $"{Name}=<{Kind.ToString().ToLowerInvariant()}>" : $"[{Name}={Default}]";
    }

    /// <summary>A skill the window and the console can start.</summary>
    public sealed class SkillInfo
    {
        /// <summary>Lowercase snake_case; prefix it with your mod's name: mymod.pick_stones.</summary>
        public string Id { get; set; }
        public string Description { get; set; }
        public List<Param> Params { get; } = new List<Param>();
        public Func<Args, Skill> Create { get; set; }

        /// <summary>
        /// A building block to test from the console (move_to, strike), not a job for the player: left out of the
        /// window and of <c>autoverk skills</c>, listed by <c>autoverk skills all</c>.
        /// </summary>
        public bool ForDevelopers { get; set; }

        public string Usage => string.Join(" ", new[] { Id }.Concat(Params.Select(p => p.ToString())));
    }

    /// <summary>Checked parameter values for one start of a skill.</summary>
    public sealed class Args
    {
        private readonly Dictionary<string, string> _values;

        private Args(Dictionary<string, string> values) { _values = values; }

        public string Text(string name) => _values.TryGetValue(name, out var v) ? v : null;
        public string Item(string name) => Text(name);
        public float Number(string name) => float.Parse(Text(name), CultureInfo.InvariantCulture);
        public bool Bool(string name) => IsYes(Text(name));

        /// <summary>The values with defaults filled in, or null and why not.</summary>
        public static Args Check(SkillInfo info, IReadOnlyDictionary<string, string> given, out string error)
        {
            error = null;
            var unknown = given.Keys.Where(k => info.Params.All(p => p.Name != k)).ToList();
            if (unknown.Count > 0)
            {
                error = $"unknown parameter {string.Join(", ", unknown)}; usage: {info.Usage}";
                return null;
            }
            var values = new Dictionary<string, string>();
            foreach (var p in info.Params)
            {
                if (!given.TryGetValue(p.Name, out var v)) v = p.Default;
                if (v == null)
                {
                    error = $"{p.Name} is required; usage: {info.Usage}";
                    return null;
                }
                if (p.Kind == ParamKind.Number && !float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    error = $"{p.Name} must be a number, not '{v}'";
                else if (p.Kind == ParamKind.Bool && !IsYes(v) && !IsNo(v))
                    error = $"{p.Name} must be yes or no, not '{v}'";
                else if (p.Kind == ParamKind.Choice && !p.Options.Contains(v))
                    error = $"{p.Name} must be one of {string.Join(", ", p.Options)}";
                if (error != null) return null;
                values[p.Name] = v;
            }
            return new Args(values);
        }

        /// <summary>"farm.gather crop=Carrot name=\"Fine wood\"" → the id and the key=value pairs, or null and why not.</summary>
        public static string ParseLine(string line, out Dictionary<string, string> values, out string error)
        {
            values = new Dictionary<string, string>();
            error = null;
            var words = Split(line ?? "");
            if (words.Count == 0)
            {
                error = "no skill given";
                return null;
            }
            foreach (var word in words.Skip(1))
            {
                int eq = word.IndexOf('=');
                if (eq <= 0)
                {
                    error = $"'{word}' isn't name=value";
                    return null;
                }
                values[word.Substring(0, eq)] = word.Substring(eq + 1);
            }
            return words[0];
        }

        private static List<string> Split(string line)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            bool quoted = false, any = false;
            foreach (var c in line)
            {
                if (c == '"') { quoted = !quoted; any = true; }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (any) words.Add(word.ToString());
                    word.Clear();
                    any = false;
                }
                else { word.Append(c); any = true; }
            }
            if (any) words.Add(word.ToString());
            return words;
        }

        private static bool IsYes(string v) => v == "yes" || v == "true" || v == "1";
        private static bool IsNo(string v) => v == "no" || v == "false" || v == "0";
    }

    /// <summary>The skills the window and the console can start, and the reflexes. Register yours at start-up.</summary>
    public static class Registry
    {
        private static readonly Dictionary<string, SkillInfo> ById = new Dictionary<string, SkillInfo>();
        private static readonly List<Reflex> ReflexList = new List<Reflex>();

        /// <summary>Adds the skill, replacing one with the same id.</summary>
        public static void Register(SkillInfo info)
        {
            if (string.IsNullOrEmpty(info.Id) || info.Create == null)
                throw new ArgumentException("a skill needs an Id and Create");
            ById[info.Id] = info;
        }

        /// <summary>Adds the reflex, replacing one with the same id.</summary>
        public static void Register(Reflex reflex)
        {
            ReflexList.RemoveAll(r => r.Id == reflex.Id);
            ReflexList.Add(reflex);
        }

        /// <summary>Removes the skill or reflex with this id.</summary>
        public static void Unregister(string id)
        {
            ById.Remove(id);
            ReflexList.RemoveAll(r => r.Id == id);
        }

        public static SkillInfo Find(string id) => id != null && ById.TryGetValue(id, out var info) ? info : null;

        /// <summary>The skills for the player, by id.</summary>
        public static IEnumerable<SkillInfo> Skills => AllSkills.Where(s => !s.ForDevelopers);

        /// <summary>The player's skills and the developers' ones, by id.</summary>
        public static IEnumerable<SkillInfo> AllSkills => ById.Values.OrderBy(s => s.Id);

        public static IReadOnlyList<Reflex> Reflexes => ReflexList;

        public static IEnumerable<Reflex> EnabledReflexes => ReflexList.Where(r => r.Enabled).ToList();

        /// <summary>Creates a skill from a console line, or null and why not.</summary>
        public static Skill FromLine(string line, out string error)
        {
            var id = Args.ParseLine(line, out var given, out error);
            if (id == null) return null;
            var info = Find(id);
            if (info == null)
            {
                error = $"no skill '{id}'; 'autoverk skills' lists them";
                return null;
            }
            var args = Args.Check(info, given, out error);
            return args == null ? null : info.Create(args);
        }
    }
}
