using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AutoVerk.Ui
{
    /// <summary>
    /// The mod's window: a Jotunn wood panel the window key shows and hides, sized to what it shows. Four looks:
    /// <list type="bullet">
    /// <item>Form (idle): a Tasks tab to pick a task, fill in its fields (grouped in sections, each with a hint),
    /// read the summary, show it in the world and start; a Settings tab to turn interruptions on and off.</item>
    /// <item>Running: what it's doing and how far it got, under the minimap. It leaves the game its input, so it
    /// has no buttons: any key pauses.</item>
    /// <item>Paused: the same, with Resume and Stop.</item>
    /// <item>Finished: how it ended, with a button to carry on the job, and Close.</item>
    /// </list>
    /// Form, Paused and Finished hold the mouse and the input. The top-right corner stays where it was dragged.
    /// Valheim rebuilds its GUI on every scene change, so the panel is made when first shown and forgotten on a
    /// rebuild.
    /// </summary>
    internal sealed class Window
    {
        private const float Width = 500f;
        private const float RunWidth = 400f;
        private const float Pad = 20f;
        private const float TitleY = -30f;
        private const float Top = -62f;
        private const float RowHeight = 34f;
        private const float Row = 40f;
        private const float LabelWidth = 95f;
        private const float ControlX = 120f;
        private const float ControlWidth = 200f;
        private const float HintX = 332f;
        private const float HintWidth = 150f;
        private const float FactLabelX = 40f;
        private const float FactValueX = 165f;
        private const float ButtonHeight = 40f;
        private const int SummarySize = 15;

        private static readonly Color Grey = new Color(0.8f, 0.78f, 0.74f);
        private static readonly Color Dim = new Color(0.68f, 0.65f, 0.6f);

        public enum Look { Form, Running, Paused, Finished }

        private enum Tab { Tasks, Settings }

        public sealed class Actions
        {
            public Action Start;
            /// <summary>Show the chosen task's preview, or hide the one showing.</summary>
            public Action Preview;
            /// <summary>A parameter's own button was pressed (and confirmed, if it asks).</summary>
            public Action<Param> Press;
            public Action Resume;
            public Action Stop;
            /// <summary>Finished: carry on the job (Fill the rest), or set up a new one.</summary>
            public Action Again;
            /// <summary>Finished: put the result away.</summary>
            public Action Close;
            /// <summary>A tab or the task was switched: redraw now.</summary>
            public Action Changed;
            /// <summary>The window was hidden at this top-right corner (offset from the screen's top-right).</summary>
            public Action<Vector2> Moved;
        }

        /// <summary>One row of the Settings tab: a checkbox, saved as it changes, under its section's heading.</summary>
        public sealed class Setting
        {
            public string Id;
            public string Section;
            public string Label;
            public Func<bool> On;
            public Action<bool> SetOn;
        }

        /// <summary>What the window shows; built by the plugin on every refresh.</summary>
        public sealed class View
        {
            public Look Look;
            /// <summary>Running, paused, finished: "Planting…", "Paused", "Stopped: out of carrot seeds".</summary>
            public string Heading;
            /// <summary>Under the heading: "Carrot · field carrots-4".</summary>
            public string Subject;
            /// <summary>Label and value rows: ("Planted", "4").</summary>
            public List<KeyValuePair<string, string>> Facts = new List<KeyValuePair<string, string>>();
            /// <summary>The small line at the bottom.</summary>
            public string Footnote;
            /// <summary>Finished: the left button, "Fill the rest" or "New task".</summary>
            public string AgainButton;
            /// <summary>Form: why Start didn't start, or what the preview found.</summary>
            public string Message;
            public List<Setting> Settings = new List<Setting>();
            /// <summary>Markers stand in the world: the preview button says Hide.</summary>
            public bool Previewing;
            /// <summary>Form: what the preview found, in the summary box under the summary.</summary>
            public string PreviewText;
        }

        /// <summary>One field of the chosen task: label, box (or checkbox and caption), hint, and a note under it.</summary>
        private sealed class ParamRow
        {
            public Param P;
            public Text Label;
            public RectTransform Control;
            public Text Caption;
            public Text Hint;
            public Text Note;
            /// <summary>The parameter's own button, in place of the hint while it's shown.</summary>
            public GameObject Button;
            /// <summary>Pressed once, it asks again before it acts: for this value only.</summary>
            public string ArmedFor;
        }

        private sealed class Section
        {
            public Text Header;
            public readonly List<ParamRow> Rows = new List<ParamRow>();
        }

        private readonly Actions _actions;
        private readonly SkillForm _form;
        private Vector2 _corner;
        private Tab _tab;
        private GameObject _canvas;
        private GameObject _panel;
        private Text _title;
        private Look _look;
        private bool _compact;
        private bool _blocking;
        private bool _interactive = true;

        // Form
        private GameObject _formGroup;
        private GameObject[] _tabs;
        private GameObject _tasksGroup;
        private Choice _task;
        private RectTransform _taskRect;
        private Text _description;
        private RectTransform _formDivider;
        private GameObject _params;
        private string _paramsKey;
        private readonly List<Section> _sections = new List<Section>();
        private RectTransform _summaryBox;
        private Text _summary;
        private Text _warning;
        private Text _found;
        private Text _message;
        private GameObject _preview;
        private GameObject _start;
        private GameObject _settingsGroup;
        private string _settingsKey;
        private readonly List<(Setting setting, Text label, Toggle check)> _settingRows = new List<(Setting, Text, Toggle)>();
        private Text _settingsNote;

        // Running, paused, finished
        private GameObject _statusGroup;
        private Text _heading;
        private Text _subject;
        private RectTransform _statusDivider;
        private GameObject _factsGroup;
        private readonly List<(Text label, Text value)> _facts = new List<(Text, Text)>();
        private GameObject _left;
        private GameObject _right;
        private Text _footnote;

        public Window(Actions actions, SkillForm form, Vector2 corner)
        {
            _actions = actions;
            _form = form;
            _corner = corner;
        }

        public bool Visible => _panel != null && _panel.activeSelf;

        /// <summary>The window holds the mouse and blocks game input.</summary>
        public bool Blocking => _blocking;

        /// <summary>GUIManager.OnCustomGUIAvailable: the old GUI is gone with the scene.</summary>
        public void OnGuiRebuilt()
        {
            _canvas = null;
            _panel = null;
            Block(false);
        }

        public void Toggle() => SetVisible(!Visible);

        public void SetVisible(bool visible)
        {
            if (visible && _panel == null) Create();
            if (_panel == null) return;
            if (!visible && _panel.activeSelf) RememberCorner();
            _panel.SetActive(visible);
            Block(visible && _interactive);
        }

        /// <summary>Passive while a run goes (the game must keep its input), interactive otherwise.</summary>
        public void SetInteractive(bool interactive)
        {
            _interactive = interactive;
            Block(Visible && interactive);
        }

        public void Show(View v)
        {
            if (!Visible) return;
            _look = v.Look;
            bool form = v.Look == Look.Form;
            _formGroup.SetActive(form);
            _statusGroup.SetActive(!form);
            float width = v.Look == Look.Running ? RunWidth : Width;
            float height = form ? LayOutForm(v) : LayOutStatus(v, width);
            Resize(new Vector2(width, height), v.Look == Look.Running);
        }

        /// <summary>Plugin unloading (hot reload or quit): leave nothing behind.</summary>
        public void Destroy()
        {
            Block(false);
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _canvas = null;
            _panel = null;
        }

        // ---- Form ----

        /// <summary>Places the form top to bottom and returns its height.</summary>
        private float LayOutForm(View v)
        {
            float inner = Width - 2 * Pad;
            float y = Top;
            float tabWidth = (inner - 12f) / 2f;
            for (int i = 0; i < _tabs.Length; i++)
            {
                Put(_tabs[i], Pad + i * (tabWidth + 12f), y, tabWidth, RowHeight);
                Style(_tabs[i], (int)_tab == i);
            }
            y -= RowHeight + 14f;

            _tasksGroup.SetActive(_tab == Tab.Tasks);
            _settingsGroup.SetActive(_tab == Tab.Settings);
            _start.SetActive(_tab == Tab.Tasks);
            _preview.SetActive(_tab == Tab.Tasks && (_form.CanPreview || v.Previewing));
            if (_tab == Tab.Settings) return LayOutSettings(v, y);

            var info = _form.Skill;
            var skills = _form.Skills;
            _task.Set(skills.Select(s => s.Title).ToList(), _form.Selected, "no tasks");
            Put(_taskRect, Pad, y, inner, 36f);
            y -= 36f + 10f;

            _description.text = info?.Description ?? "";
            y -= PutText(_description, Pad, y, inner) + 12f;
            Put(_formDivider, Pad, y, inner, 2f);
            y -= 16f;

            ShowParams(info);
            var values = _form.Values;
            foreach (var section in _sections)
            {
                var shown = section.Rows.Where(r => Safe(() => r.P.Shown?.Invoke(values) ?? true, true)).ToList();
                bool header = section.Header != null && shown.Count > 0;
                if (section.Header != null) section.Header.gameObject.SetActive(header);
                if (header)
                {
                    Put(section.Header, Pad, y, inner, 22f);
                    y -= 28f;
                }
                foreach (var row in section.Rows)
                {
                    bool on = shown.Contains(row);
                    row.Label.gameObject.SetActive(on);
                    row.Control.gameObject.SetActive(on);
                    row.Hint.gameObject.SetActive(on);
                    if (row.Caption != null) row.Caption.gameObject.SetActive(on);
                    if (row.Button != null && !on) row.Button.SetActive(false);
                    var note = on ? Safe(() => row.P.Note?.Invoke(values), null) : null;
                    row.Note.gameObject.SetActive(!string.IsNullOrEmpty(note));
                    if (!on) continue;

                    Put(row.Label, Pad, y, LabelWidth, RowHeight);
                    if (row.Caption != null)
                    {
                        Put(row.Control, ControlX, y - 3f, 28f, 28f);
                        Put(row.Caption, ControlX + 36f, y, ControlWidth - 30f, RowHeight);
                    }
                    else Put(row.Control, ControlX, y, ControlWidth, RowHeight);
                    var button = row.P.Button;
                    bool pressable = button != null && Safe(() => button.Shown?.Invoke(values) ?? true, false);
                    if (row.Button != null) row.Button.SetActive(pressable);
                    row.Hint.gameObject.SetActive(!pressable);
                    if (pressable)
                    {
                        var value = _form.Value(row.P);
                        if (row.ArmedFor != value) row.ArmedFor = null;
                        SetText(row.Button, row.ArmedFor != null ? $"Really {button.Label.ToLowerInvariant()}?" : button.Label);
                        Style(row.Button, row.ArmedFor != null);
                        Put(row.Button, HintX, y, HintWidth, RowHeight);
                    }
                    else
                    {
                        row.Hint.text = Safe(() => row.P.Hint != null ? row.P.Hint(values) : row.Caption == null ? row.P.Description : null, "") ?? "";
                        Put(row.Hint, HintX, y, HintWidth, RowHeight);
                    }
                    y -= Row;
                    if (string.IsNullOrEmpty(note)) continue;
                    row.Note.text = note;
                    y -= PutText(row.Note, Pad, y + 4f, inner) + 2f;
                }
                y -= 4f;
            }

            // The box: what will happen, what will go wrong, and what the preview found in the world.
            var summary = info?.Summary != null ? Safe(() => info.Summary(values), null) : null;
            var lines = new[] { (_summary, summary?.Text), (_warning, summary?.Warning), (_found, v.PreviewText) };
            bool box = lines.Any(l => !string.IsNullOrEmpty(l.Item2));
            _summaryBox.gameObject.SetActive(box);
            if (box)
            {
                y -= 8f;
                float inside = -8f;
                foreach (var (text, line) in lines)
                {
                    text.gameObject.SetActive(!string.IsNullOrEmpty(line));
                    if (string.IsNullOrEmpty(line)) continue;
                    text.text = line;
                    inside -= PutText(text, 10f, inside, inner - 20f) + 2f;
                }
                float boxHeight = -inside + 6f;
                Put(_summaryBox, Pad, y, inner, boxHeight);
                y -= boxHeight + 12f;
            }

            _message.gameObject.SetActive(!string.IsNullOrEmpty(v.Message));
            if (!string.IsNullOrEmpty(v.Message))
            {
                _message.text = v.Message;
                y -= PutText(_message, Pad, y, inner) + 10f;
            }

            float half = (inner - 12f) / 2f;
            SetText(_preview, v.Previewing ? "Hide" : info?.PreviewText ?? "Preview");
            SetText(_start, info?.StartText ?? "Start");
            Style(_start, true);
            Put(_preview, Pad, y, half, ButtonHeight);
            Put(_start, Pad + half + 12f, y, half, ButtonHeight);
            return -(y - ButtonHeight - Pad);
        }

        /// <summary>The chosen task's rows, made again when the task or its suggestions changed.</summary>
        private void ShowParams(SkillInfo info)
        {
            var key = $"{info?.Id}#{_form.Revision}";
            if (key == _paramsKey) return;
            _paramsKey = key;
            if (_params != null) UnityEngine.Object.Destroy(_params);
            _params = Group(_tasksGroup.transform, "Params");
            _sections.Clear();
            if (info == null) return;
            var gui = GUIManager.Instance;
            var c = _params.transform;
            foreach (var param in info.Params)
            {
                var p = param;
                var section = _sections.LastOrDefault();
                if (section == null || (section.Rows.Count > 0 && section.Rows[0].P.Section != p.Section))
                {
                    section = new Section();
                    if (p.Section != null)
                    {
                        section.Header = Label(c, Spaced(p.Section), 13, gui.ValheimOrange);
                    }
                    _sections.Add(section);
                }
                var row = new ParamRow { P = p };
                row.Label = Label(c, p.Label ?? p.Name, 16, Color.white);
                if (p.Kind == ParamKind.Bool)
                {
                    var check = Check(c, on => _form.Set(p, on ? "yes" : "no"));
                    check.SetIsOnWithoutNotify(_form.Value(p) == "yes");
                    row.Control = (RectTransform)check.transform;
                    row.Caption = Label(c, p.Description, 15, Color.white);
                }
                else if (p.Kind == ParamKind.Choice) row.Control = Dropdown(c, p, p.Options.ToList());
                else if (p.Suggestions != null) row.Control = Dropdown(c, p, Suggested(p));
                else
                {
                    var content = p.Kind == ParamKind.Number ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
                    row.Control = (RectTransform)Input(c, _form.Value(p), content, v => _form.Set(p, v)).transform;
                }
                row.Hint = Label(c, "", 13, Grey);
                row.Hint.verticalOverflow = VerticalWrapMode.Overflow;
                if (p.Button != null)
                {
                    var r = row;
                    r.Button = Button(c, p.Button.Label, () =>
                    {
                        var value = _form.Value(p);
                        if (p.Button.Confirm && r.ArmedFor != value) r.ArmedFor = value;
                        else
                        {
                            r.ArmedFor = null;
                            _actions.Press?.Invoke(p);
                        }
                        _actions.Changed?.Invoke();
                    });
                    r.Button.GetComponentInChildren<Text>().fontSize = 15;
                }
                row.Note = Label(c, "", 14, Grey);
                row.Note.alignment = TextAnchor.UpperLeft;
                section.Rows.Add(row);
            }
        }

        /// <summary>The values a parameter suggests, empty first when it means something ("New field").</summary>
        private static List<string> Suggested(Param p)
        {
            var values = new List<string>();
            if (p.EmptyText != null) values.Add("");
            try
            {
                values.AddRange(p.Suggestions().Where(s => !string.IsNullOrEmpty(s)));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"suggestions for {p.Name}: {e.Message}");
            }
            return values;
        }

        /// <summary>How the window says a value: the empty text, the parameter's own names, an item's name, or Capitalized.</summary>
        private static string Display(Param p, string value)
        {
            if (value == "") return p.EmptyText ?? "";
            if (p.Names != null) return Safe(() => p.Names(value), value);
            if (p.Kind == ParamKind.Item) return Senses.ItemName(value);
            if (p.Kind == ParamKind.Choice) return char.ToUpperInvariant(value[0]) + value.Substring(1);
            return value;
        }

        /// <summary>
        /// A dropdown of values. An empty value with no meaning of its own takes the first one, so a seed is chosen
        /// as soon as you carry one; a value typed in the console that isn't offered is kept.
        /// </summary>
        private RectTransform Dropdown(Transform parent, Param p, List<string> values)
        {
            var current = _form.Value(p);
            if (current == "" && p.EmptyText == null && values.Count > 0)
            {
                current = values[0];
                _form.Set(p, current);
            }
            if (current != "" && !values.Contains(current)) values.Add(current);
            var drop = Drop(parent);
            var choice = new Choice(drop, i => _form.Set(p, values[i]));
            choice.Set(values.Select(v => Display(p, v)).ToList(), values.IndexOf(current), "none found");
            return (RectTransform)drop.transform;
        }

        /// <summary>The Settings tab: a row per setting under its section's heading, rebuilt when the set changes.</summary>
        private float LayOutSettings(View v, float y)
        {
            var key = string.Join(",", v.Settings.Select(s => s.Id));
            if (key != _settingsKey)
            {
                _settingsKey = key;
                foreach (Transform child in _settingsGroup.transform)
                    if (child != _settingsNote.transform) UnityEngine.Object.Destroy(child.gameObject);
                _settingRows.Clear();
                foreach (var s in v.Settings)
                {
                    var setting = s;
                    if (setting.Section != null && setting.Section != _settingRows.LastOrDefault().setting?.Section)
                        _settingRows.Add((null, Label(_settingsGroup.transform, Spaced(setting.Section), 13, GUIManager.Instance.ValheimOrange), null));
                    var label = Label(_settingsGroup.transform, setting.Label, 15, Color.white);
                    var check = Check(_settingsGroup.transform, on => setting.SetOn(on));
                    _settingRows.Add((setting, label, check));
                }
            }
            float inner = Width - 2 * Pad;
            foreach (var (setting, label, check) in _settingRows)
            {
                if (setting == null)
                {
                    // A section's heading.
                    Put(label, Pad, y, inner, 22f);
                    y -= 28f;
                    continue;
                }
                Put(label, Pad, y, inner - 40f, RowHeight);
                Put(check, Pad + inner - 28f, y - 3f, 28f, 28f);
                if (check.isOn != setting.On()) check.SetIsOnWithoutNotify(setting.On());
                y -= Row;
            }
            y -= 6f;
            y -= PutText(_settingsNote, Pad, y, inner);
            return -(y - Pad);
        }

        // ---- Running, paused, finished ----

        private float LayOutStatus(View v, float width)
        {
            var gui = GUIManager.Instance;
            float inner = width - 2 * Pad;
            float y = Top;
            _heading.text = v.Heading ?? "";
            _heading.color = v.Look == Look.Running ? Color.white : gui.ValheimOrange;
            y -= PutText(_heading, Pad, y, inner) + 2f;
            _subject.gameObject.SetActive(!string.IsNullOrEmpty(v.Subject));
            if (!string.IsNullOrEmpty(v.Subject))
            {
                _subject.text = v.Subject;
                y -= PutText(_subject, Pad, y, inner);
            }
            y -= 10f;
            Put(_statusDivider, Pad, y, inner, 2f);
            y -= 14f;

            while (_facts.Count < v.Facts.Count)
                _facts.Add((Label(_factsGroup.transform, "", 15, Color.white), Label(_factsGroup.transform, "", 15, Color.white)));
            for (int i = 0; i < _facts.Count; i++)
            {
                var (label, value) = _facts[i];
                bool on = i < v.Facts.Count;
                label.gameObject.SetActive(on);
                value.gameObject.SetActive(on);
                if (!on) continue;
                label.text = v.Facts[i].Key;
                value.text = v.Facts[i].Value;
                Put(label, FactLabelX, y, FactValueX - FactLabelX - 8f, 26f);
                float h = Mathf.Max(26f, PutText(value, FactValueX, y - 3f, width - FactValueX - Pad) + 6f);
                y -= h;
            }

            bool buttons = v.Look != Look.Running;
            _left.SetActive(buttons);
            _right.SetActive(buttons);
            if (buttons)
            {
                y -= 16f;
                float half = (inner - 12f) / 2f;
                SetText(_left, v.Look == Look.Paused ? "Resume" : v.AgainButton ?? "New task");
                SetText(_right, v.Look == Look.Paused ? "Stop" : "Close");
                Style(_left, true);
                Put(_left, Pad, y, half, ButtonHeight);
                Put(_right, Pad + half + 12f, y, half, ButtonHeight);
                y -= ButtonHeight;
            }
            _footnote.gameObject.SetActive(!string.IsNullOrEmpty(v.Footnote));
            if (!string.IsNullOrEmpty(v.Footnote))
            {
                y -= 10f;
                _footnote.text = v.Footnote;
                y -= PutText(_footnote, Pad, y, inner);
            }
            return -(y - Pad + 4f);
        }

        private void OnLeft()
        {
            if (_look == Look.Paused) _actions.Resume?.Invoke();
            else _actions.Again?.Invoke();
        }

        private void OnRight()
        {
            if (_look == Look.Paused) _actions.Stop?.Invoke();
            else _actions.Close?.Invoke();
        }

        // ---- The panel ----

        private void Create()
        {
            var gui = GUIManager.Instance;
            var root = gui != null ? Canvas() : null;
            if (root == null) return;

            // Back on screen if the resolution or the GUI scale changed since the corner was saved.
            var screen = CanvasSize();
            _corner.x = Mathf.Clamp(_corner.x, -Math.Max(Width, screen.x) + Width, 0f);
            _corner.y = Mathf.Clamp(_corner.y, -Math.Max(400f, screen.y) + 60f, 0f);
            _panel = gui.CreateWoodpanel(root, new Vector2(1f, 1f), new Vector2(1f, 1f), _corner - new Vector2(Width, 400f) / 2f,
                Width, 400f, draggable: true);
            _panel.name = "AutoVerkWindow";
            _compact = false;
            _paramsKey = null;
            _settingsKey = null;
            _settingRows.Clear();
            _sections.Clear();
            _facts.Clear();

            _title = Label(_panel.transform, "AutoVerk", 30, gui.ValheimOrange);
            _title.alignment = TextAnchor.MiddleCenter;

            _formGroup = Group(_panel.transform, "Form");
            var f = _formGroup.transform;
            var names = new[] { "Tasks", "Settings" };
            _tabs = new GameObject[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var tab = (Tab)i;
                _tabs[i] = Button(f, names[i], () =>
                {
                    _tab = tab;
                    _actions.Changed?.Invoke();
                });
            }

            _tasksGroup = Group(f, "Tasks");
            var t = _tasksGroup.transform;
            var taskDrop = Drop(t, 17);
            _taskRect = (RectTransform)taskDrop.transform;
            _task = new Choice(taskDrop, i =>
            {
                _form.Select(i);
                _actions.Changed?.Invoke();
            });
            _description = Label(t, "", 14, Grey);
            _description.alignment = TextAnchor.UpperLeft;
            _formDivider = Divider(t);
            _summaryBox = Box(t);
            // One size for the whole box; colour tells the lines apart.
            _summary = Label(_summaryBox, "", SummarySize, Color.white);
            _summary.alignment = TextAnchor.MiddleCenter;
            _warning = Label(_summaryBox, "", SummarySize, gui.ValheimOrange);
            _warning.alignment = TextAnchor.MiddleCenter;
            _found = Label(_summaryBox, "", SummarySize, Grey);
            _found.alignment = TextAnchor.MiddleCenter;
            _message = Label(t, "", 14, gui.ValheimOrange);
            _message.alignment = TextAnchor.UpperLeft;
            _preview = Button(f, "Preview", () => _actions.Preview?.Invoke());
            _start = Button(f, "Start", () => _actions.Start?.Invoke());

            _settingsGroup = Group(f, "Settings");
            _settingsNote = Label(_settingsGroup.transform,
                "Interruptions step in during any run when something happens, then the run carries on. Without jumping, " +
                "a walk that gets stuck gives up at once.", 14, Grey);
            _settingsNote.alignment = TextAnchor.UpperLeft;

            _statusGroup = Group(_panel.transform, "Status");
            var s = _statusGroup.transform;
            _heading = Label(s, "", 20, Color.white);
            _heading.alignment = TextAnchor.UpperCenter;
            _subject = Label(s, "", 14, Grey);
            _subject.alignment = TextAnchor.UpperCenter;
            _statusDivider = Divider(s);
            _factsGroup = Group(s, "Facts");
            _left = Button(s, "Resume", OnLeft);
            _right = Button(s, "Stop", OnRight);
            _footnote = Label(s, "", 13, Dim);
            _footnote.alignment = TextAnchor.UpperCenter;

            _panel.SetActive(false);
        }

        /// <summary>Sizes the panel, keeping its top-right corner; the run block has its own place under the minimap.</summary>
        private void Resize(Vector2 size, bool compact)
        {
            var rt = (RectTransform)_panel.transform;
            var corner = compact ? RunCorner() : _corner;
            if (compact == _compact && rt.sizeDelta == size && (rt.anchoredPosition + size / 2f - corner).sqrMagnitude < 1f) return;
            if (compact && !_compact) RememberCorner();
            _compact = compact;
            rt.sizeDelta = size;
            rt.anchoredPosition = corner - size / 2f;
            Put(_title, Pad, TitleY + 18f, size.x - 2 * Pad, 36f);
        }

        /// <summary>The full window's place, where the player dragged it. The run block has its own fixed place.</summary>
        private void RememberCorner()
        {
            if (_compact) return;
            var rt = (RectTransform)_panel.transform;
            var corner = rt.anchoredPosition + rt.sizeDelta / 2f;
            if ((corner - _corner).sqrMagnitude < 1f) return;
            _corner = corner;
            _actions.Moved?.Invoke(corner);
        }

        /// <summary>
        /// Where the run block's top-right corner goes: right under the minimap, its right edge in line with the
        /// map's. Without the small map, near the top.
        /// </summary>
        private static Vector2 RunCorner()
        {
            var root = Minimap.instance != null ? Minimap.instance.m_smallRoot : null;
            if (root == null || !root.activeInHierarchy) return new Vector2(-20f, -60f);
            var corners = new Vector3[4];
            ((RectTransform)root.transform).GetWorldCorners(corners); // [0] bottom-left, [2] top-right, screen pixels
            float scale = Screen.width / CanvasSize().x;
            return new Vector2((corners[2].x - Screen.width) / scale, (corners[0].y - Screen.height) / scale - 8f);
        }

        /// <summary>
        /// The window's own canvas, beside Jotunn's CustomGUIFront and drawn over it. Jotunn's canvas has a fixed
        /// scale of 1, so a window there ignores the game's GUI scale and is too small on a large screen. Valheim's
        /// GuiScaler on this canvas sets the game UI's scale: the screen against 1920×1080, times the GUI scale.
        /// </summary>
        private Transform Canvas()
        {
            if (_canvas != null) return _canvas.transform;
            var front = GUIManager.CustomGUIFront;
            if (front == null) return null;
            _canvas = new GameObject("AutoVerkCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rt = (RectTransform)_canvas.transform;
            rt.SetParent(front.transform.parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2001;
            _canvas.GetComponent<CanvasScaler>().referencePixelsPerUnit = 50;
            _canvas.AddComponent<GuiScaler>();
            return _canvas.transform;
        }

        /// <summary>The canvas size in its own units: the screen over the scale GuiScaler sets.</summary>
        private static Vector2 CanvasSize()
        {
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) * PlatformPrefs.GetFloat("GuiScale", GuiScaler.PlatformDefaultScaling);
            return new Vector2(Screen.width, Screen.height) / Math.Max(0.1f, scale);
        }

        private void Block(bool state)
        {
            if (_blocking == state) return;
            _blocking = state;
            GUIManager.BlockInput(state);
        }

        // ---- Controls ----

        /// <summary>A dropdown and the entries it holds, rebuilt only when they change (not while it's open).</summary>
        private sealed class Choice
        {
            private readonly Dropdown _dropdown;
            private string _key;

            public Choice(Dropdown dropdown, Action<int> changed)
            {
                _dropdown = dropdown;
                _dropdown.onValueChanged.AddListener(i => changed(i));
            }

            public void Set(List<string> options, int selected, string emptyText)
            {
                bool empty = options.Count == 0;
                if (empty) options = new List<string> { emptyText ?? "" };
                var key = string.Join("\n", options);
                if (key != _key)
                {
                    _key = key;
                    _dropdown.ClearOptions();
                    _dropdown.AddOptions(options);
                }
                _dropdown.interactable = !empty;
                if (empty || selected < 0) selected = 0;
                if (_dropdown.value != selected) _dropdown.SetValueWithoutNotify(selected);
            }
        }

        /// <summary>"Crop" → "C R O P": the mockup's small spaced capitals.</summary>
        private static string Spaced(string section) => string.Join(" ", section.ToUpperInvariant().ToCharArray());

        /// <summary>Calls a skill's own hint or summary; one that throws shows nothing instead of breaking the window.</summary>
        private static T Safe<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"window: {e.Message}");
                return fallback;
            }
        }

        /// <summary>An empty full-size child, so a group of controls shows and hides together.</summary>
        private static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Places a control by its top-left corner, from the parent's top-left.</summary>
        private static void Put(Component c, float x, float top, float width, float height)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x + width / 2f, top - height / 2f);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static void Put(GameObject go, float x, float top, float width, float height) => Put(go.transform, x, top, width, height);

        /// <summary>Places wrapped text by its top-left corner and returns its height.</summary>
        private static float PutText(Text t, float x, float top, float width)
        {
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            float height = Mathf.Max(t.fontSize + 6f, t.preferredHeight + 2f);
            Put(t, x, top, width, height);
            return height;
        }

        /// <summary>Text anchored to the top-left corner, placed later.</summary>
        private static Text Label(Transform parent, string text, int size, Color color)
        {
            var gui = GUIManager.Instance;
            var t = gui.CreateText(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                gui.AveriaSerifBold, size, color, true, new Color(0f, 0f, 0f, 0.6f), 100f, 20f, false).GetComponent<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        private static Dropdown Drop(Transform parent, int fontSize = 15) =>
            GUIManager.Instance.CreateDropDown(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, fontSize, ControlWidth, RowHeight)
                .GetComponent<Dropdown>();

        /// <summary>A Valheim-styled checkbox with no label of its own.</summary>
        private static Toggle Check(Transform parent, Action<bool> changed)
        {
            const float size = 28f;
            var go = GUIManager.Instance.CreateToggle(parent, size, size);
            foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = "";
            var toggle = go.GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(on => changed(on));
            return toggle;
        }

        /// <summary>A text box; changed gets every edit.</summary>
        private static InputField Input(Transform parent, string value, InputField.ContentType content, Action<string> changed)
        {
            var go = GUIManager.Instance.CreateInputField(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                content, "", 16, ControlWidth, RowHeight);
            var field = go.GetComponent<InputField>();
            field.text = value;
            field.onValueChanged.AddListener(v => changed(v));
            return field;
        }

        private static GameObject Button(Transform parent, string text, Action onClick)
        {
            var go = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 100f, ButtonHeight);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var label = go.GetComponentInChildren<Text>();
            label.font = GUIManager.Instance.AveriaSerifBold;
            label.fontSize = 18;
            return go;
        }

        private static void SetText(GameObject button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label.text != text) label.text = text;
        }

        /// <summary>The button to press, or the tab that's open: orange text in an orange frame.</summary>
        private static void Style(GameObject button, bool marked)
        {
            var orange = GUIManager.Instance.ValheimOrange;
            button.GetComponentInChildren<Text>().color = marked ? orange : Color.white;
            var outline = button.GetComponent<Outline>() ?? button.AddComponent<Outline>();
            outline.effectColor = orange;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = marked;
        }

        /// <summary>A thin dark line across.</summary>
        private static RectTransform Divider(Transform parent)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            return (RectTransform)go.transform;
        }

        /// <summary>A dark box to set text on.</summary>
        private static RectTransform Box(Transform parent)
        {
            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.05f, 0.8f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            return (RectTransform)go.transform;
        }
    }
}
