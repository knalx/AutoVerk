using System.Collections.Generic;
using AutoVerk.Engine;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AutoVerk.Game
{
    /// <summary>Death or leaving the world ends the run.</summary>
    internal sealed class PlayerGuard : IGuard
    {
        public GuardHit Check()
        {
            var player = Senses.Player;
            if (player == null) return GuardHit.End("left_world", "the character left the world");
            if (player.IsDead()) return GuardHit.End("died", "the character died");
            return null;
        }
    }

    /// <summary>A game menu, the inventory, the map or the chat pauses the run.</summary>
    internal sealed class MenuGuard : IGuard
    {
        public GuardHit Check()
        {
            var menu = Senses.OpenMenu();
            return menu != null ? GuardHit.Pause($"{menu} open") : null;
        }
    }

    /// <summary>Any key (except the window key), click or gamepad button pauses the run. Mouse movement doesn't.</summary>
    internal sealed class HumanInputGuard : IGuard
    {
        private readonly ConfigEntry<KeyboardShortcut> _windowKey;

        public HumanInputGuard(ConfigEntry<KeyboardShortcut> windowKey) { _windowKey = windowKey; }

        public GuardHit Check()
        {
            var input = Pressed();
            return input != null ? GuardHit.Pause(input) : null;
        }

        private string Pressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
            {
                // Input System key names don't match KeyCode for digits, the keypad or punctuation, so the window
                // key isn't matched by name: when the game sees it go down, one non-modifier key this frame is it.
                var main = _windowKey?.Value.MainKey ?? KeyCode.None;
                bool windowKey = main != KeyCode.None && ZInput.GetKeyDown(main, false);
                var pressed = new List<string>();
                foreach (var key in kb.allKeys)
                {
                    if (key == null || !key.wasPressedThisFrame) continue;
                    if (windowKey && IsModifier(key.keyCode)) continue;
                    pressed.Add(key.displayName);
                }
                if (pressed.Count > (windowKey ? 1 : 0)) return $"you pressed {string.Join(" + ", pressed)}";
            }
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                  mouse.middleButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame ||
                                  mouse.backButton.wasPressedThisFrame))
                return "you clicked";
            var pad = Gamepad.current;
            if (pad != null)
                foreach (var control in pad.allControls)
                    if (control is ButtonControl b && b.wasPressedThisFrame) return "gamepad input";
            return null;
        }

        private static bool IsModifier(Key k) =>
            k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl ||
            k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftCommand || k == Key.RightCommand;
    }
}
