using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills;
using AutoVerk.Ui;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
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

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private ConfigEntry<KeyboardShortcut> _windowKey;
        /// <summary>A run started from the console waits until the console is closed.</summary>
        private Skill _pending;
        private bool _resumePending;
        private RunState _shownState;

        public Runner Runner { get; private set; }

        private void Awake()
        {
            Log = Logger;
            _windowKey = Config.Bind("Window", "Key", new KeyboardShortcut(KeyCode.F7), "Opens and closes the window.");

            Runner = new Runner(() => Time.timeAsDouble, text => Log.LogInfo(text));
            Runner.Guards.Add(new PlayerGuard());
            Runner.Guards.Add(new HumanInputGuard(_windowKey));
            Runner.Guards.Add(new MenuGuard());

            Builtins.Register();
            Commands.Register(this);
            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, Guid);
        }

        private void Update()
        {
            Body.Clear();
            if (Senses.OpenMenu() == null)
            {
                // Start on the frame after the console closes, so the key that closed it doesn't pause the run.
                if (_pending != null)
                {
                    Runner.Start(_pending);
                    _pending = null;
                    return;
                }
                if (_resumePending)
                {
                    Runner.Resume();
                    _resumePending = false;
                    return;
                }
            }
            Runner.Tick();
            ShowChanges();
        }

        /// <summary>Starts now, or when the open menu (the console) closes.</summary>
        internal string StartWhenFree(Skill skill)
        {
            if (Senses.Player == null) return "not in a world";
            var busy = Senses.Busy(Senses.Player);
            if (busy != null) return $"can't start: the character is {busy}";
            _pending = skill;
            return $"{skill} starts when you close the console";
        }

        internal string ResumeWhenFree()
        {
            if (Runner.State != RunState.Paused) return Commands.Describe(Runner);
            _resumePending = true;
            return "resumes when you close the console";
        }

        /// <summary>Says a pause or an end in the middle of the screen.</summary>
        private void ShowChanges()
        {
            if (Runner.State == _shownState) return;
            _shownState = Runner.State;
            if (MessageHud.instance == null) return;
            if (Runner.State == RunState.Paused)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"AutoVerk paused: {Runner.PauseReason}");
            else if (Runner.State == RunState.Finished)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, $"AutoVerk: {Runner.Outcome}");
        }

        private void OnDestroy()
        {
            Runner?.Stop("the mod was unloaded");
            Body.Clear();
            Commands.Unregister();
            _harmony?.UnpatchSelf();
        }
    }
}
