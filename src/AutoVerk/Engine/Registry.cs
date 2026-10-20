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

        /// <summary>What the dropdown says for an empty value: "new", "anything".</summary>
        public string EmptyText { get; private set; }

        /// <summary>The window's name for it: "Per row". Defaults to the name.</summary>
        public string Label { get; private set; }

        /// <summary>The window groups parameters under this heading: "Crop". Null for none.</summary>
        public string Section { get; private set; }

        /// <summary>The window's note right of the box, read afresh as the form changes: "6 in your pack". Defaults to the description.</summary>
        public Func<FormValues, string> Hint { get; private set; }

        /// <summary>A line under the row, or null: "The new field is laid out in front of you."</summary>
        public Func<FormValues, string> Note { get; private set; }

        /// <summary>Whether the window shows the row now; a hidden one keeps its value.</summary>
        public Func<FormValues, bool> Shown { get; private set; }

        /// <summary>How the window says a value: "Staggered" for staggered.</summary>
        public Func<string, string> Names { get; private set; }

        /// <summary>Offer these values in the window's dropdown.</summary>
        public Param WithSuggestions(Func<IEnumerable<string>> suggestions, string emptyText = null)
        {
            Suggestions = suggestions;
            EmptyText = emptyText;
            return this;
        }

        /// <summary>For the window: the row's label and section, and a hint that may follow the other fields.</summary>
        public Param Shows(string label, string section = null, Func<FormValues, string> hint = null)
        {
            Label = label;
            Section = section;
            Hint = hint;
            return this;
        }

        public Param WithNote(Func<FormValues, string> note)
        {
            Note = note;
            return this;
        }

        public Param ShownWhen(Func<FormValues, bool> shown)
        {
            Shown = shown;
            return this;
        }

        public Param WithNames(Func<string, string> names)
        {
            Names = names;
            return this;
        }

        /// <summary>A button beside the box, or null: "Delete" for a saved field.</summary>
        public ParamButton Button { get; private set; }

        /// <summary>
        /// A button beside the box, shown when <paramref name="shown"/> says. Pressing it calls
        /// <paramref name="press"/>, which says what it did; with <paramref name="confirm"/> the first press only
        /// asks; with <paramref name="clears"/> the box is emptied after.
        /// </summary>
        public Param WithButton(string label, Func<FormValues, string> press, Func<FormValues, bool> shown = null,
            bool confirm = false, bool clears = false)
        {
            Button = new ParamButton { Label = label, Press = press, Shown = shown, Confirm = confirm, Clears = clears };
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

        private string _title;
        private string _doing;

        /// <summary>The window's name for it: "Plant a field". Defaults to the id.</summary>
        public string Title { get => _title ?? Id; set => _title = value; }

        /// <summary>The window's heading while it runs, without the dots: "Planting". Defaults to the title.</summary>
        public string Doing { get => _doing ?? Title; set => _doing = value; }

        /// <summary>The window's start button: "Start planting".</summary>
        public string StartText { get; set; } = "Start";

        /// <summary>The window's preview button: "Show in world".</summary>
        public string PreviewText { get; set; } = "Preview";

        /// <summary>What stopping leaves behind, under the Stop button: "Stop keeps what's planted."</summary>
        public string StopNote { get; set; }

        /// <summary>The box over the start button, read afresh as the form changes. Null for none.</summary>
        public Func<FormValues, FormSummary> Summary { get; set; }
        public List<Param> Params { get; } = new List<Param>();
        public Func<Args, Skill> Create { get; set; }

        /// <summary>
        /// Optional: show where the skill would work without doing anything, like markers on a field's spots, and
        /// say what it found in a line (rich text colours are fine). The window's Preview button shows the line in
        /// the summary box while the markers stand; <c>autoverk preview</c> prints it.
        /// </summary>
        public Func<Args, string> Preview { get; set; }

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

        internal static bool IsYes(string v) => v == "yes" || v == "true" || v == "1";
        internal static bool IsNo(string v) => v == "no" || v == "false" || v == "0";
    }

    /// <summary>The form's values as they stand, unchecked, for hints and the summary while the player types.</summary>
    public sealed class FormValues
    {
        private readonly Func<string, string> _value;

        public FormValues(Func<string, string> value) { _value = value; }

        public string Text(string name) => _value(name) ?? "";
        public string Item(string name) => Text(name);
        public bool Bool(string name) => Args.IsYes(Text(name));

        /// <summary>Null while the box doesn't hold a number.</summary>
        public float? Number(string name) =>
            float.TryParse(Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (float?)null;
    }

    /// <summary>A button beside a parameter's box: see <see cref="Param.WithButton"/>.</summary>
    public sealed class ParamButton
    {
        public string Label;
        public Func<FormValues, string> Press;
        public Func<FormValues, bool> Shown;
        public bool Confirm;
        public bool Clears;
    }

    /// <summary>The window's box over the start button: what will happen, and a warning in orange.</summary>
    public sealed class FormSummary
    {
        public string Text;
        public string Warning;
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
            var args = ArgsFromLine(line, out var info, out error);
            if (args == null) return null;
            var skill = info.Create(args);
            if (skill != null) skill.Info = info;
            return skill;
        }

        /// <summary>Previews a skill from a console line: what it found, or why not.</summary>
        public static string PreviewLine(string line)
        {
            var args = ArgsFromLine(line, out var info, out var error);
            if (args == null) return error;
            return info.Preview == null ? $"{info.Id} has no preview" : info.Preview(args);
        }

        private static Args ArgsFromLine(string line, out SkillInfo info, out string error)
        {
            info = null;
            var id = Args.ParseLine(line, out var given, out error);
            if (id == null) return null;
            info = Find(id);
            if (info == null)
            {
                error = $"no skill '{id}'; 'autoverk skills' lists them";
                return null;
            }
            return Args.Check(info, given, out error);
        }
    }
}
