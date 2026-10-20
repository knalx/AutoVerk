using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace AutoVerk.Ui
{
    /// <summary>
    /// The mod's window: a Jotunn wood panel the window key shows and hides. Three looks:
    /// <list type="bullet">
    /// <item>Form (idle or finished): a Skills tab to pick a skill, fill in its fields, Preview and Start, and a Settings
    /// tab to turn interruptions on and off. Holds the mouse and the input.</item>
    /// <item>Paused: why, with Resume and Stop. Holds the mouse and the input.</item>
    /// <item>Running: a small status block under the minimap that leaves the game its input.</item>
    /// </list>
    /// Its top-right corner stays where it was dragged. Valheim rebuilds its GUI on every scene change, so the
    /// panel is made when first shown and forgotten on a rebuild.
    /// </summary>
    internal sealed class Window
    {
        private const float Width = 460f;
        private const float Height = 560f;
        private const float CompactWidth = 400f;
        private const float Row = 34f;
        private const float TabsY = -66f;
        private const float SkillY = -110f;
        private const float FirstParamY = -190f;

        public enum Look { Form, Paused, Running }

        private enum Tab { Skills, Settings }

        public sealed class Actions
        {
            public Action Start;
            /// <summary>Show the chosen skill's preview, or hide the one showing.</summary>
            public Action Preview;
            public Action Resume;
            public Action Stop;
            /// <summary>A tab or the skill was switched: redraw now.</summary>
            public Action Changed;
            /// <summary>The window was hidden at this top-right corner (offset from the screen's top-right).</summary>
            public Action<Vector2> Moved;
        }

        /// <summary>One row of the Settings tab: a checkbox, saved as it changes.</summary>
        public sealed class Setting
        {
            public string Id;
            public string Label;
            public Func<bool> On;
            public Action<bool> SetOn;
        }

        /// <summary>What the window shows; built by the plugin on every refresh.</summary>
        public sealed class View
        {
            public Look Look;
            /// <summary>Label and value rows: ("doing", "collect › move_to Wood"). Values may hold colour tags.</summary>
            public List<KeyValuePair<string, string>> Rows = new List<KeyValuePair<string, string>>();
            /// <summary>Free text under the form: the last run's outcome, or why Start didn't start.</summary>
            public string Body;
            public List<Setting> Settings = new List<Setting>();
            /// <summary>The preview button's text, "Preview" or "Hide"; null hides the button.</summary>
            public string PreviewButton;
        }

        private sealed class TabButton
        {
            public Image Image;
            public Text Text;
        }

        private readonly Actions _actions;
        private readonly SkillForm _form;
        private Vector2 _corner;
        private Tab _tab;
        private GameObject _canvas;
        private GameObject _panel;
        private Text _title;
        private Text _rows;
        private Text _body;
        private GameObject _tabsGroup;
        private readonly TabButton[] _tabs = new TabButton[2];
        private GameObject _formGroup;
        private Choice _skill;
        private Text _description;
        private GameObject _params;
        private string _paramsKey;
        private GameObject _settingsGroup;
        private string _settingsKey;
        private readonly List<(Setting setting, Toggle check)> _settingRows = new List<(Setting, Toggle)>();
        private GameObject _start;
        private GameObject _preview;
        private GameObject _resume;
        private GameObject _stop;
        private bool _compact;
        private bool _blocking;
        private bool _interactive = true;

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
            bool compact = v.Look == Look.Running;
            bool form = v.Look == Look.Form;
            float width = (compact ? CompactWidth : Width) - 40f;
            // The width first: the wrapped height depends on it.
            _rows.rectTransform.sizeDelta = new Vector2(width, _rows.rectTransform.sizeDelta.y);
            _rows.text = string.Join("\n", v.Rows.Select(r => $"<color=#b4b4b4>{r.Key}</color>   {r.Value}"));
            SetCompact(compact);
            _tabsGroup.SetActive(form);
            _formGroup.SetActive(form && _tab == Tab.Skills);
            _settingsGroup.SetActive(form && _tab == Tab.Settings);
            _start.SetActive(form && _tab == Tab.Skills);
            _preview.SetActive(form && _tab == Tab.Skills && v.PreviewButton != null);
            if (v.PreviewButton != null) _preview.GetComponentInChildren<Text>().text = v.PreviewButton;
            _resume.SetActive(v.Look == Look.Paused);
            _stop.SetActive(v.Look == Look.Paused);

            float top = -50f;
            var body = compact ? "" : v.Body ?? "";
            if (form)
            {
                for (int i = 0; i < _tabs.Length; i++)
                {
                    bool on = (int)_tab == i;
                    _tabs[i].Text.color = on ? GUIManager.Instance.ValheimOrange : new Color(0.75f, 0.75f, 0.75f);
                    _tabs[i].Image.color = on ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                }
                if (_tab == Tab.Skills)
                {
                    ShowForm();
                    top = FirstParamY - Row * (_form.Skill?.Params.Count ?? 0) + 10f;
                }
                else
                {
                    ShowSettings(v.Settings);
                    top = SkillY - Row * v.Settings.Count + 10f;
                    body = "Interruptions step in during any run when something happens, then the run carries on.";
                }
            }
            _rows.gameObject.SetActive(v.Rows.Count > 0);
            PlaceTop(_rows.rectTransform, 20f, top, width, _rows.preferredHeight);
            if (v.Rows.Count > 0) top -= _rows.preferredHeight + 8f;
            _body.text = body;
            PlaceTop(_body.rectTransform, 20f, top, width, Math.Max(20f, top + Height - 60f));
        }

        /// <summary>Plugin unloading (hot reload or quit): leave nothing behind.</summary>
        public void Destroy()
        {
            Block(false);
            if (Visible) RememberCorner();
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
            _panel = null;
            _canvas = null;
        }

        private void ShowForm()
        {
            _skill.Set(_form.Skills.Select(s => s.Id).ToList(), _form.Selected, "no skills");
            var info = _form.Skill;
            _description.text = info?.Description ?? "";
            var key = $"{info?.Id}#{_form.Revision}";
            if (key != _paramsKey)
            {
                _paramsKey = key;
                CreateParams(info);
            }
        }

        /// <summary>One row per parameter of the chosen skill: its name, a box, checkbox or dropdown, and what it is.</summary>
        private void CreateParams(SkillInfo info)
        {
            if (_params != null) UnityEngine.Object.Destroy(_params);
            _params = Group(_formGroup.transform, "Params");
            if (info == null) return;
            var c = _params.transform;
            float y = FirstParamY;
            foreach (var param in info.Params)
            {
                var p = param;
                Label(c, p.Name, 20f, y, 90f, 28f, 15, Color.white);
                if (p.Kind == ParamKind.Bool)
                {
                    var check = Check(c, 115f, y, on => _form.Set(p, on ? "yes" : "no"));
                    check.SetIsOnWithoutNotify(_form.Value(p) == "yes");
                }
                else if (p.Kind == ParamKind.Choice) Dropdown(c, y, p, p.Options.ToList());
                else if (p.Suggestions != null) Dropdown(c, y, p, Suggested(p));
                else
                {
                    var content = p.Kind == ParamKind.Number ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
                    Input(c, 115f, y, 150f, _form.Value(p), content, v => _form.Set(p, v));
                }
                Label(c, p.Description, 275f, y, 165f, 28f, 13, new Color(0.7f, 0.7f, 0.7f));
                y -= Row;
            }
        }

        /// <summary>The values a parameter suggests, empty first when it means something ("anything").</summary>
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

        /// <summary>
        /// A dropdown of values. An empty value with no meaning of its own takes the first one, so a seed is chosen
        /// as soon as you carry one; a value typed in the console that isn't offered is kept.
        /// </summary>
        private void Dropdown(Transform parent, float y, Param p, List<string> values)
        {
            var current = _form.Value(p);
            if (current == "" && p.EmptyText == null && values.Count > 0)
            {
                current = values[0];
                _form.Set(p, current);
            }
            if (current != "" && !values.Contains(current)) values.Add(current);
            var choice = new Choice(Drop(parent, 115f, y, 150f), i => _form.Set(p, values[i]));
            choice.Set(values.Select(v => v == "" ? p.EmptyText ?? "" : v).ToList(), values.IndexOf(current), "none found");
        }

        /// <summary>The Settings tab: a row per interruption, rebuilt when the set of them changes.</summary>
        private void ShowSettings(List<Setting> settings)
        {
            var key = string.Join(",", settings.Select(s => s.Id));
            if (key != _settingsKey)
            {
                _settingsKey = key;
                foreach (Transform child in _settingsGroup.transform) UnityEngine.Object.Destroy(child.gameObject);
                _settingRows.Clear();
                float y = SkillY;
                foreach (var s in settings)
                {
                    var setting = s;
                    Label(_settingsGroup.transform, setting.Label, 20f, y, 360f, 28f, 15, Color.white);
                    var check = Check(_settingsGroup.transform, 400f, y, on => setting.SetOn(on));
                    _settingRows.Add((setting, check));
                    y -= Row;
                }
            }
            foreach (var (setting, check) in _settingRows)
                if (check.isOn != setting.On()) check.SetIsOnWithoutNotify(setting.On());
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

        /// <summary>Full window or the small run block, sized to what it shows; the top-right corner stays put.</summary>
        private void SetCompact(bool compact)
        {
            var size = compact ? new Vector2(CompactWidth, 45f + _rows.preferredHeight + 14f) : new Vector2(Width, Height);
            var rt = (RectTransform)_panel.transform;
            var corner = compact ? RunCorner() : _corner;
            if (compact == _compact && rt.sizeDelta == size && (rt.anchoredPosition + size / 2f - corner).sqrMagnitude < 1f) return;
            if (compact && !_compact) RememberCorner();
            _compact = compact;
            rt.sizeDelta = size;
            rt.anchoredPosition = corner - size / 2f;
            Place(_title.rectTransform, 20f, -28f, size.x - 40f, 30f);
            _title.fontSize = compact ? 18 : 22;
            _rows.fontSize = _body.fontSize = compact ? 14 : 15;
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

        private void Create()
        {
            var gui = GUIManager.Instance;
            var root = gui != null ? Canvas() : null;
            if (root == null) return;

            // Back on screen if the resolution or the GUI scale changed since the corner was saved.
            var screen = CanvasSize();
            _corner.x = Mathf.Clamp(_corner.x, -Math.Max(Width, screen.x) + Width, 0f);
            _corner.y = Mathf.Clamp(_corner.y, -Math.Max(Height, screen.y) + 60f, 0f);
            _panel = gui.CreateWoodpanel(root, new Vector2(1f, 1f), new Vector2(1f, 1f), _corner - new Vector2(Width, Height) / 2f,
                Width, Height, draggable: true);
            _panel.name = "AutoVerkWindow";
            _compact = false;
            _paramsKey = null;
            _settingsKey = null;
            _settingRows.Clear();

            _title = Label(_panel.transform, "AutoVerk", 20f, -28f, Width - 40f, 30f, 22, gui.ValheimOrange);
            _title.alignment = TextAnchor.MiddleCenter;
            _rows = Label(_panel.transform, "", 20f, -60f, Width - 40f, 20f, 15, Color.white);
            _body = Label(_panel.transform, "", 20f, -125f, Width - 40f, 150f, 15, Color.white);
            foreach (var t in new[] { _rows, _body })
            {
                t.alignment = TextAnchor.UpperLeft;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = true;
            }

            _tabsGroup = Group(_panel.transform, "Tabs");
            var names = new[] { "Skills", "Settings" };
            float tabWidth = (Width - 40f - 6f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var tab = (Tab)i;
                var go = gui.CreateButton(names[i], _tabsGroup.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f + i * (tabWidth + 6f) + tabWidth / 2f, TabsY), tabWidth, 32f);
                go.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _tab = tab;
                    _actions.Changed?.Invoke();
                });
                _tabs[i] = new TabButton { Image = go.GetComponent<Image>(), Text = go.GetComponentInChildren<Text>() };
            }

            _formGroup = Group(_panel.transform, "Form");
            var c = _formGroup.transform;
            Label(c, "skill", 20f, SkillY, 90f, 28f, 15, Color.white);
            _skill = new Choice(Drop(c, 115f, SkillY, 325f), i =>
            {
                _form.Select(i);
                _actions.Changed?.Invoke();
            });
            _description = Label(c, "", 20f, SkillY - 38f, Width - 40f, 40f, 14, new Color(0.8f, 0.8f, 0.8f));
            _description.alignment = TextAnchor.UpperLeft;
            _settingsGroup = Group(_panel.transform, "Settings");

            _start = Button(_panel.transform, "Start", new Vector2(70f, 35f), 110f, _actions.Start);
            _preview = Button(_panel.transform, "Preview", new Vector2(190f, 35f), 110f, _actions.Preview);
            _resume = Button(_panel.transform, "Resume", new Vector2(70f, 35f), 110f, _actions.Resume);
            _stop = Button(_panel.transform, "Stop", new Vector2(190f, 35f), 110f, _actions.Stop);

            _panel.SetActive(false);
        }

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

        /// <summary>A top-left anchored rect: x is its left edge, y its centre line.</summary>
        private static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchoredPosition = new Vector2(x + width / 2f, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        /// <summary>A top-left anchored rect by its top edge.</summary>
        private static void PlaceTop(RectTransform rt, float x, float top, float width, float height) =>
            Place(rt, x, top - height / 2f, width, height);

        /// <summary>Text anchored to the top-left corner: x, y are the rect's left edge and centre line.</summary>
        private static Text Label(Transform parent, string text, float x, float y, float width, float height, int size, Color color)
        {
            var gui = GUIManager.Instance;
            var t = gui.CreateText(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + width / 2f, y),
                gui.AveriaSerifBold, size, color, true, Color.black, width, height, false).GetComponent<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            return t;
        }

        private static Dropdown Drop(Transform parent, float x, float y, float width) =>
            GUIManager.Instance.CreateDropDown(parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x + width / 2f, y), 15, width, 30f).GetComponent<Dropdown>();

        /// <summary>A Valheim-styled checkbox with no label of its own; x, y are its left edge and centre line.</summary>
        private static Toggle Check(Transform parent, float x, float y, Action<bool> changed)
        {
            const float size = 28f;
            var go = GUIManager.Instance.CreateToggle(parent, size, size);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x + size / 2f, y);
            foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = "";
            var toggle = go.GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(on => changed(on));
            return toggle;
        }

        /// <summary>A text box; changed gets every edit.</summary>
        private static InputField Input(Transform parent, float x, float y, float width, string value,
            InputField.ContentType content, Action<string> changed)
        {
            var go = GUIManager.Instance.CreateInputField(parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x + width / 2f, y), content, "", 15, width, 28f);
            var field = go.GetComponent<InputField>();
            field.text = value;
            field.onValueChanged.AddListener(v => changed(v));
            return field;
        }

        /// <summary>A button anchored to the bottom-left corner.</summary>
        private static GameObject Button(Transform parent, string text, Vector2 pos, float width, Action onClick)
        {
            var go = GUIManager.Instance.CreateButton(text, parent, Vector2.zero, Vector2.zero, pos, width, 32f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            return go;
        }
    }
}
