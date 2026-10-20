using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Distance from the player's eyes, which is what the game's use and place reach measure.</summary>
        public static float EyeDistance(Vector3 point) => Vector3.Distance(Player.m_eye.position, point);

        /// <summary>The box around an object's solid colliders, or a point box at its position when it has none.</summary>
        public static Bounds Bounds(Component target)
        {
            var box = new Bounds(target.transform.position, Vector3.zero);
            bool any = false;
            foreach (var c in target.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (any) box.Encapsulate(c.bounds);
                else box = c.bounds;
                any = true;
            }
            return box;
        }

        /// <summary>The middle of an object, to aim at: a creature's centre, or the middle of its colliders.</summary>
        public static Vector3 Center(Component target) =>
            target is Character c ? c.GetCenterPoint() : Bounds(target).center;

        /// <summary>The point of an object's box nearest to a point.</summary>
        public static Vector3 NearestPoint(Component target, Vector3 from) => Bounds(target).ClosestPoint(from);

        /// <summary>An inventory item's id, its prefab name: "CarrotSeeds".</summary>
        public static string ItemId(ItemDrop.ItemData item) => item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;

        /// <summary>The first inventory item with this id, or null.</summary>
        public static ItemDrop.ItemData FindItem(string id) =>
            Player.GetInventory().GetAllItems().FirstOrDefault(i => ItemId(i) == id);

        /// <summary>How many of an item the inventory holds.</summary>
        public static int Count(string id) =>
            Player.GetInventory().GetAllItems().Where(i => ItemId(i) == id).Sum(i => i.m_stack);

        /// <summary>The item in the right hand: weapon or tool. Null when bare-handed.</summary>
        public static ItemDrop.ItemData InHand() => GameApi.RightItem(Player);

        /// <summary>A one- or two-handed weapon that swings (axes, clubs, swords, pickaxes) and does damage.</summary>
        public static bool IsMeleeWeapon(ItemDrop.ItemData item)
        {
            var s = item?.m_shared;
            if (s?.m_attack == null) return false;
            bool hand = s.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon ||
                        s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                        s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
            bool swings = s.m_attack.m_attackType == Attack.AttackType.Horizontal || s.m_attack.m_attackType == Attack.AttackType.Vertical;
            return hand && swings && item.GetDamage().GetTotalDamage() > 0;
        }

        /// <summary>The melee weapon in the inventory that does the most damage and isn't broken, or null.</summary>
        public static ItemDrop.ItemData BestWeapon() =>
            Player.GetInventory().GetAllItems()
                .Where(i => IsMeleeWeapon(i) && !(i.m_shared.m_useDurability && i.m_durability <= 0))
                .OrderByDescending(i => i.GetDamage().GetTotalDamage())
                .FirstOrDefault();

        /// <summary>A melee weapon that does pickaxe damage: it breaks rocks.</summary>
        public static bool IsPickaxe(ItemDrop.ItemData item) => IsMeleeWeapon(item) && item.m_shared.m_damages.m_pickaxe > 0;

        /// <summary>The pickaxe in the inventory of at least a tool tier that does the most pickaxe damage and isn't broken, or null.</summary>
        public static ItemDrop.ItemData Pickaxe(int tier = 0) =>
            Player.GetInventory().GetAllItems()
                .Where(i => IsPickaxe(i) && i.m_shared.m_toolTier >= tier && !(i.m_shared.m_useDurability && i.m_durability <= 0))
                .OrderByDescending(i => i.GetDamage().m_pickaxe)
                .FirstOrDefault();

        /// <summary>The pieces the tool in hand can place (hammer, cultivator, hoe), or null.</summary>
        public static PieceTable BuildPieces() => InHand()?.m_shared?.m_buildPieces;

        /// <summary>A living creature that's hostile to the player.</summary>
        public static bool IsEnemy(Character c) => c != null && !c.IsDead() && BaseAI.IsEnemy(c, Player);

        /// <summary>Every object of a type within a radius of the player.</summary>
        public static List<T> Around<T>(float radius) where T : Component
        {
            var found = new List<T>();
            if (Player == null) return found;
            var from = Player.transform.position;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (Vector3.Distance(from, candidate.transform.position) <= radius) found.Add(candidate);
            return found;
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
