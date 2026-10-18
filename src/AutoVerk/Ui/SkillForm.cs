using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;

namespace AutoVerk.Ui
{
    /// <summary>
    /// The window's start form: the chosen skill and what's typed in its fields, kept per skill while the game
    /// runs. Plain C#, so it's unit-tested. The last start is saved as a console line and restored from it.
    /// </summary>
    internal sealed class SkillForm
    {
        private readonly Dictionary<string, Dictionary<string, string>> _values = new Dictionary<string, Dictionary<string, string>>();
        private string _id;

        public IReadOnlyList<SkillInfo> Skills { get; private set; } = Array.Empty<SkillInfo>();

        /// <summary>The chosen skill, or null when none is registered.</summary>
        public SkillInfo Skill => Skills.FirstOrDefault(s => s.Id == _id);

        public int Selected => Skills.ToList().FindIndex(s => s.Id == _id);

        /// <summary>Goes up when the skill or its suggestions may have changed: the window then rebuilds the fields.</summary>
        public int Revision { get; private set; }

        /// <summary>Reads the registered skills again, keeping the choice when it's still there.</summary>
        public void Refresh()
        {
            Skills = Registry.Skills.ToList();
            if (Skill == null) _id = Skills.FirstOrDefault()?.Id;
            Revision++;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Skills.Count || Skills[index].Id == _id) return;
            _id = Skills[index].Id;
            Revision++;
        }

        /// <summary>What the field shows: what was typed, else the default.</summary>
        public string Value(Param p) =>
            _id != null && _values.TryGetValue(_id, out var typed) && typed.TryGetValue(p.Name, out var v) ? v : p.Default ?? "";

        public void Set(Param p, string value)
        {
            if (_id == null) return;
            if (!_values.TryGetValue(_id, out var typed)) _values[_id] = typed = new Dictionary<string, string>();
            typed[p.Name] = value ?? "";
        }

        /// <summary>The chosen skill as filled in, or null and why not.</summary>
        public Skill Create(out string error)
        {
            var info = Skill;
            if (info == null)
            {
                error = "no skills registered";
                return null;
            }
            // An empty field counts as not given, so a required one says so and an optional one takes its default.
            var given = info.Params.Select(p => (p.Name, Value: Value(p))).Where(v => v.Value != "")
                .ToDictionary(v => v.Name, v => v.Value);
            var args = Args.Check(info, given, out error);
            if (args == null) return null;
            try
            {
                return info.Create(args);
            }
            catch (Exception e)
            {
                error = $"{info.Id} couldn't start: {e.Message}";
                return null;
            }
        }

        /// <summary>The form as a console line: collect radius=10 item=Wood.</summary>
        public string Line
        {
            get
            {
                var info = Skill;
                if (info == null) return "";
                return string.Join(" ", new[] { info.Id }.Concat(info.Params.Select(p => $"{p.Name}={Quote(Value(p))}")));
            }
        }

        /// <summary>Chooses the line's skill and fills in its values, if that skill is registered.</summary>
        public void Restore(string line)
        {
            var id = Args.ParseLine(line, out var values, out _);
            var info = Registry.Find(id);
            if (info == null || info.ForDevelopers) return;
            if (Skills.Count == 0) Refresh();
            _id = id;
            foreach (var p in info.Params)
                if (values.TryGetValue(p.Name, out var v)) Set(p, v);
        }

        private static string Quote(string v) => v.Any(char.IsWhiteSpace) ? $"\"{v}\"" : v;
    }
}
