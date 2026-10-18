using System.Collections.Generic;
using System.Globalization;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills;
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

        public Runner Runner { get; private set; }

        private void Awake()
        {
            Log = Logger;
            _windowKey = Config.Bind("Window", "Key", new KeyboardShortcut(KeyCode.F7), "Opens and closes the window.");
            _windowPosition = Config.Bind("Window", "Position", "-20, -100",
                "The window's top-right corner, from the screen's top-right corner. Saved when you move it.");
            _lastStart = Config.Bind("Window", "LastStart", "", "The window's last start, as a console line. Filled in next time.");

            Runner = new Runner(() => Time.timeAsDouble, text => Log.LogInfo(text));
            Runner.Guards.Add(new PlayerGuard());
            Runner.Guards.Add(new HumanInputGuard(_windowKey));
            Runner.Guards.Add(new MenuGuard());

            Builtins.Register();
            Commands.Register(this);

            _form = new SkillForm();
            _form.Refresh();
            _form.Restore(_lastStart.Value);
            _window = new Window(new Window.Actions
            {
                Start = StartFromWindow,
                Resume = () => _message = ResumeWhenFree(),
                Stop = () => Runner.Stop("stopped from the window"),
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
                    Runner.Start(_pending);
                    _pending = null;
                    _message = null;
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

        private Window.View BuildView()
        {
            var v = new Window.View();
            switch (Runner.State)
            {
                case RunState.Running:
                    v.Look = Window.Look.Running;
                    v.Rows.Add(Row("doing", Runner.Path));
                    if (Runner.Status != null) v.Rows.Add(Row("now", Runner.Status));
                    break;
                case RunState.Paused:
                    v.Look = Window.Look.Paused;
                    v.Rows.Add(Row("paused", Runner.PauseReason));
                    v.Rows.Add(Row("doing", Runner.Path));
                    v.Body = _message ?? "Resume starts the interrupted step again; it checks its target first.";
                    break;
                default:
                    v.Look = Window.Look.Form;
                    v.Body = _message ?? (Runner.State == RunState.Finished ? Commands.Describe(Runner) : null);
                    break;
            }
            return v;
        }

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

        private static Vector2 ParseCorner(string text)
        {
            var parts = (text ?? "").Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return new Vector2(x, y);
            return new Vector2(-20f, -100f);
        }

        private void OnDestroy()
        {
            Runner?.Stop("the mod was unloaded");
            Body.Clear();
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
