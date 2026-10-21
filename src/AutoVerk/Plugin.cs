using System.Collections.Generic;
using System.Globalization;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills;
using AutoVerk.Skills.Basic;
using AutoVerk.Ui;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace AutoVerk
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInProcess("valheim")]
    // No [NetworkCompatibility]: Jotunn then treats the mod as client-side only, never required or version-checked.
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "autoverk";
        public const string Name = "AutoVerk";
        public const string Version = "0.1.0";

        private const float RefreshSeconds = 0.25f;

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private ConfigEntry<KeyboardShortcut> _windowKey;
        private ConfigEntry<string> _windowPosition;
        private ConfigEntry<string> _lastStart;
        /// <summary>On or off for each interruption (reflex), by id; other mods' ones are added as they show up.</summary>
        private readonly Dictionary<string, ConfigEntry<bool>> _interruptions = new Dictionary<string, ConfigEntry<bool>>();
        private ConfigEntry<bool> _jumpWhenStuck;
        private ConfigEntry<bool> _stopWhenOverweight;
        private SkillForm _form;
        private Window _window;
        private float _nextRefresh;
        private bool _closeOnNextFrame;
        /// <summary>Why the window's Start didn't start, or what it's waiting for; cleared when a run starts.</summary>
        private string _message;
        /// <summary>A run started from the console waits until the console is closed.</summary>
        private Skill _pending;
        private bool _resumePending;
        private RunState _shownState;
        /// <summary>The finished run's result was closed: the window shows the form again.</summary>
        private bool _resultSeen;
        /// <summary>What the preview found, shown while its markers stand.</summary>
        private string _previewText;

        public Runner Runner { get; private set; }

        private void Awake()
        {
            Log = Logger;
            _windowKey = Config.Bind("Window", "Key", new KeyboardShortcut(KeyCode.F7),
                "Opens and closes the window. A Unity key name, then any modifiers: F8, or K + LeftAlt.");
            _windowPosition = Config.Bind("Window", "Position", "",
                "The window's top-right corner, from the screen's top-right corner. Empty: under the minimap. Saved when you drag it.");
            _lastStart = Config.Bind("Window", "LastStart", "", "The window's last start, as a console line. Filled in next time.");
            _jumpWhenStuck = Config.Bind("Movement", "JumpWhenStuck", true,
                "Jump (twice) when stuck on the way before giving up. Off: give up at once.");
            MoveTo.JumpWhenStuck = _jumpWhenStuck.Value;
            _stopWhenOverweight = Config.Bind("Run", "StopWhenOverweight", true,
                "Stop any task when your pack gets heavier than you can carry.");
            OverweightGuard.Enabled = _stopWhenOverweight.Value;

            Runner = new Runner(() => Time.timeAsDouble, text => Log.LogInfo(text));
            Runner.Guards.Add(new PlayerGuard());
            Runner.Guards.Add(new OverweightGuard());
            Runner.Guards.Add(new HumanInputGuard(_windowKey));
            Runner.Guards.Add(new MenuGuard());

            Builtins.Register();
            Commands.Register(this);
            ApplyInterruptionSettings();

            _form = new SkillForm();
            _form.Refresh();
            _form.Restore(_lastStart.Value);
            _window = new Window(new Window.Actions
            {
                Start = StartFromWindow,
                Preview = PreviewFromWindow,
                Press = p =>
                {
                    _message = _form.Press(p);
                    _nextRefresh = 0f;
                },
                Resume = () => _message = ResumeWhenFree(),
                Stop = () => Runner.Stop("stopped from the window"),
                Again = AgainFromWindow,
                Close = () =>
                {
                    _resultSeen = true;
                    _window.SetVisible(false);
                },
                Changed = () => _nextRefresh = 0f,
                Moved = corner => _windowPosition.Value = string.Format(CultureInfo.InvariantCulture, "{0:0}, {1:0}", corner.x, corner.y),
            }, _form, ParseCorner(_windowPosition.Value));
            GUIManager.OnCustomGUIAvailable += _window.OnGuiRebuilt;

            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, Guid);
        }

        private void Update()
        {
            // Esc closes the window only if it held the input before this frame: an Esc that just paused a run
            // must leave the freshly opened window up.
            bool wasBlocking = _window.Visible && _window.Blocking;
            Body.Clear();
            TickRun();
            ShowChanges();
            // The game only takes our controls while nothing blocks its input.
            _window.SetInteractive(Runner.State != RunState.Running);

            // Close one frame after Esc: while the window blocks input, Jotunn makes the game menu ignore Esc, and
            // unblocking in the same frame could open the game menu right behind it.
            if (_closeOnNextFrame)
            {
                _closeOnNextFrame = false;
                _window.SetVisible(false);
            }
            if (Senses.Player == null)
            {
                if (_window.Visible) _window.SetVisible(false);
            }
            else if (WindowKeyDown())
            {
                if (!_window.Visible) _form.Refresh();
                _window.Toggle();
                _nextRefresh = 0f;
            }
            else if (wasBlocking && ZInput.GetKeyDown(KeyCode.Escape, false)) _closeOnNextFrame = true;

            if (_window.Visible && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                _window.Show(BuildView());
            }
        }

        private void TickRun()
        {
            if (Senses.OpenMenu() == null)
            {
                // Start on the frame after the console closes, so the key that closed it doesn't pause the run.
                if (_pending != null)
                {
                    ApplyInterruptionSettings();
                    Runner.Start(_pending);
                    _pending = null;
                    _message = null;
                    _resultSeen = false;
                    return;
                }
                if (_resumePending)
                {
                    Runner.Resume();
                    _resumePending = false;
                    _message = null;
                    return;
                }
            }
            Runner.Tick();
        }

        /// <summary>Starts on the next frame with no menu open: now from the window, or when the console closes.</summary>
        internal string StartWhenFree(Skill skill)
        {
            if (Senses.Player == null) return "not in a world";
            var busy = Senses.Busy(Senses.Player);
            if (busy != null) return $"can't start: the character is {busy}";
            Markers.Hide();
            _previewText = null;
            _pending = skill;
            var menu = Senses.OpenMenu();
            return menu == null ? $"starting {skill}" : $"{skill} starts when you close the {menu}";
        }

        internal string ResumeWhenFree()
        {
            if (Runner.State != RunState.Paused) return Commands.Describe(Runner);
            _resumePending = true;
            var menu = Senses.OpenMenu();
            return menu == null ? "resuming" : $"resumes when you close the {menu}";
        }

        private void StartFromWindow()
        {
            var skill = _form.Create(out var error);
            if (skill == null)
            {
                _message = error;
                _nextRefresh = 0f;
                return;
            }
            _lastStart.Value = _form.Line;
            _message = StartWhenFree(skill);
            _nextRefresh = 0f;
        }

        /// <summary>
        /// Shows the chosen skill's preview, its line in the summary box while the markers stand, or hides the one
        /// showing. A preview that stood no markers said why not: that goes under the box like Start's errors.
        /// </summary>
        private void PreviewFromWindow()
        {
            _message = null;
            _previewText = null;
            if (Markers.Showing) Markers.Hide();
            else
            {
                var said = _form.Preview();
                if (Markers.Showing) _previewText = said;
                else _message = said;
            }
            _nextRefresh = 0f;
        }

        /// <summary>Finished: carry on the job its own way (Fill the rest), or back to the form for a new one.</summary>
        private void AgainFromWindow()
        {
            _resultSeen = true;
            var line = Runner.Root?.AgainLine;
            if (line == null)
            {
                _nextRefresh = 0f;
                return;
            }
            var skill = Registry.FromLine(line, out var error);
            _message = skill == null ? error : StartWhenFree(skill);
            _nextRefresh = 0f;
        }

        private Window.View BuildView()
        {
            var v = new Window.View();
            var root = Runner.Root;
            var info = root?.Info;
            switch (Runner.State)
            {
                case RunState.Running:
                    v.Look = Window.Look.Running;
                    v.Heading = $"{info?.Doing ?? root.ToString()}…";
                    Describe(v, root);
                    v.Footnote = "Any key pauses.";
                    break;
                case RunState.Paused:
                    v.Look = Window.Look.Paused;
                    v.Heading = "Paused";
                    Describe(v, root);
                    v.Footnote = $"{Capitalized(Runner.PauseReason)}. {info?.StopNote ?? "Resume carries on where it was."}";
                    break;
                case RunState.Finished when !_resultSeen:
                    v.Look = Window.Look.Finished;
                    var outcome = Runner.Outcome;
                    v.Heading = root.Headline ?? (outcome.Ok ? "Done"
                        : outcome.Code == "stopped" ? "Stopped"
                        : $"Stopped: {outcome.Detail ?? outcome.Code}");
                    Describe(v, root);
                    if (v.Facts.Count == 0 && root.Report != null) v.Facts.Add(Row("Result", root.Report));
                    v.AgainButton = root.AgainLine != null ? root.AgainLabel ?? "Again" : "New task";
                    v.Footnote = _message ?? root.Advice ?? (outcome.Code == "stopped" ? info?.StopNote : null);
                    break;
                default:
                    v.Look = Window.Look.Form;
                    v.Message = _message;
                    v.Previewing = Markers.Showing;
                    // Markers also stand on a finished run's skipped spots, with no preview line.
                    v.PreviewText = Markers.Showing ? _previewText : null;
                    ApplyInterruptionSettings();
                    foreach (var reflex in Registry.Reflexes)
                    {
                        var r = reflex;
                        var entry = _interruptions[r.Id];
                        v.Settings.Add(new Window.Setting
                        {
                            Id = r.Id,
                            Section = "Interruptions",
                            Label = r.Description,
                            Note = "Steps in during any task, then the task goes on.",
                            On = () => entry.Value,
                            SetOn = on => r.Enabled = entry.Value = on,
                        });
                    }
                    v.Settings.Add(new Window.Setting
                    {
                        Id = "jump",
                        Section = "Movement",
                        Label = "Jump when stuck on the way",
                        Note = "Off: the hero gives up on a spot as soon as something blocks the way.",
                        On = () => _jumpWhenStuck.Value,
                        SetOn = on => MoveTo.JumpWhenStuck = _jumpWhenStuck.Value = on,
                    });
                    v.Settings.Add(new Window.Setting
                    {
                        Id = "overweight",
                        Section = "Any task",
                        Label = "Stop when overweight",
                        Note = "Ends the task as soon as your pack is heavier than you can carry.",
                        On = () => _stopWhenOverweight.Value,
                        SetOn = on => OverweightGuard.Enabled = _stopWhenOverweight.Value = on,
                    });
                    break;
            }
            return v;
        }

        /// <summary>Gives each interruption its config entry, and its on or off from there.</summary>
        private void ApplyInterruptionSettings()
        {
            foreach (var reflex in Registry.Reflexes)
            {
                if (!_interruptions.TryGetValue(reflex.Id, out var entry))
                    _interruptions[reflex.Id] = entry = Config.Bind("Interruptions", reflex.Id, true, reflex.Description);
                reflex.Enabled = entry.Value;
            }
        }

        /// <summary>
        /// The run's subject and facts. While it runs or waits, what it's doing this moment goes first, from the
        /// deepest step that says: "Mining Large rock (swing 2)", "Resting until stamina is back: 34 of 100".
        /// </summary>
        private void Describe(Window.View v, Skill root)
        {
            v.Subject = root.Subject;
            if (Runner.State != RunState.Finished && Runner.Status != null) v.Facts.Add(Row("Now", Capitalized(Runner.Status)));
            v.Facts.AddRange(root.Facts);
        }

        private static string Capitalized(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static KeyValuePair<string, string> Row(string label, string value) => new KeyValuePair<string, string>(label, value);

        /// <summary>Says a pause or an end in the middle of the screen, and opens the window on it.</summary>
        private void ShowChanges()
        {
            if (Runner.State == _shownState) return;
            _shownState = Runner.State;
            if (Runner.State != RunState.Paused && Runner.State != RunState.Finished) return;
            if (MessageHud.instance != null)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Runner.State == RunState.Paused
                    ? $"AutoVerk paused: {Runner.PauseReason}"
                    : $"AutoVerk: {Runner.Outcome}");
            // Not over a game menu: the inventory or the console keeps the input.
            if (Senses.Player == null || Senses.OpenMenu() != null) return;
            _form.Refresh();
            _window.SetInteractive(true);
            _window.SetVisible(true);
            _nextRefresh = 0f;
        }

        /// <summary>ZInput reads the keyboard at a low level, so this works while the window blocks game input.</summary>
        private bool WindowKeyDown()
        {
            var shortcut = _windowKey.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
            foreach (var modifier in shortcut.Modifiers)
                if (!ZInput.GetKey(modifier, false)) return false;
            return true;
        }

        /// <summary>
        /// The saved corner, or null for under the minimap: empty, unreadable, or the old default (-20, -100) that
        /// was saved before the window could be dragged.
        /// </summary>
        private static Vector2? ParseCorner(string text)
        {
            var parts = (text ?? "").Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                !(x == -20f && y == -100f))
                return new Vector2(x, y);
            return null;
        }

        private void OnDestroy()
        {
            Runner?.Stop("the mod was unloaded");
            Body.Clear();
            Markers.Destroy();
            if (_window != null)
            {
                GUIManager.OnCustomGUIAvailable -= _window.OnGuiRebuilt;
                _window.Destroy();
            }
            Commands.Unregister();
            _harmony?.UnpatchSelf();
        }
    }
}
