using System;
using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>Reads the game for skills. Nothing here changes it.</summary>
    public static class Senses
    {
        public static Player Player => Player.m_localPlayer;

        /// <summary>Distance along the ground, ignoring height.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>The nearest object of a type within a radius of the player that matches, or null.</summary>
        public static T Nearest<T>(float radius, Func<T, bool> match = null) where T : Component
        {
            var player = Player;
            if (player == null) return null;
            var from = player.transform.position;
            T best = null;
            float bestDistance = radius;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(from, candidate.transform.position);
                if (d > bestDistance || (match != null && !match(candidate))) continue;
                best = candidate;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>Why the character can't be driven now ("sitting"), or null.</summary>
        public static string Busy(Player p)
        {
            if (p.IsDead()) return "dead";
            if (p.InCutscene()) return "in a cutscene";
            if (p.IsTeleporting()) return "teleporting";
            if (p.InBed()) return "in bed";
            if (p.IsRiding()) return "riding";
            if (p.IsAttachedToShip()) return "on a ship";
            if (p.IsAttached()) return "attached to something";
            if (p.IsSitting()) return "sitting";
            return null;
        }

        /// <summary>The game screen that takes the player's input, or null.</summary>
        public static string OpenMenu()
        {
            if (Menu.IsVisible()) return "game menu";
            if (InventoryGui.IsVisible()) return "inventory";
            if (Minimap.IsOpen()) return "map";
            if (global::Console.IsVisible()) return "console";
            if (Chat.instance != null && Chat.instance.HasFocus()) return "chat";
            if (StoreGui.IsVisible()) return "trader";
            if (Hud.IsPieceSelectionVisible()) return "build menu";
            if (TextViewer.instance != null && TextViewer.instance.IsVisible()) return "text";
            // Not TextInput.IsVisible(): Jotunn makes it true while any mod window blocks input.
            if (TextInput.instance != null && TextInput.instance.m_panel != null && TextInput.instance.m_panel.activeSelf) return "text input";
            return null;
        }
    }
}
