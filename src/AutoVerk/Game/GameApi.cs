using System;
using HarmonyLib;
using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>The game's private members Body and Senses use, checked against 1.0.20.</summary>
    internal static class GameApi
    {
        public static readonly AccessTools.FieldRef<Player, int> PlaceRayMask = AccessTools.FieldRefAccess<Player, int>("m_placeRayMask");
        public static readonly AccessTools.FieldRef<Player, int> PlaceWaterRayMask = AccessTools.FieldRefAccess<Player, int>("m_placeWaterRayMask");
        public static readonly AccessTools.FieldRef<Player, float> PlacePressedTime = AccessTools.FieldRefAccess<Player, float>("m_placePressedTime");
        public static readonly AccessTools.FieldRef<Player, float> LastToolUseTime = AccessTools.FieldRefAccess<Player, float>("m_lastToolUseTime");
        public static readonly AccessTools.FieldRef<Player, bool> NoPlacementCost = AccessTools.FieldRefAccess<Player, bool>("m_noPlacementCost");
        public static readonly AccessTools.FieldRef<Player, GameObject> PlacementGhost = AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");
        /// <summary>The view's pitch in degrees, positive looks down. Melee swings take their vertical aim from it.</summary>
        public static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        public static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> RightItem = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_rightItem");

        private static readonly Action<Player, GameObject, bool, bool> InteractCall =
            AccessTools.MethodDelegate<Action<Player, GameObject, bool, bool>>(
                AccessTools.Method(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) }));

        private static readonly Func<Plant, bool> HaveGrowSpaceCall =
            AccessTools.MethodDelegate<Func<Plant, bool>>(AccessTools.Method(typeof(Plant), "HaveGrowSpace"));

        /// <summary>What the game does when the player presses Use on a hovered object.</summary>
        public static void Interact(Player player, GameObject target) => InteractCall(player, target, false, false);

        /// <summary>The game's grow-space rule, on any Plant (the placement ghost too).</summary>
        public static bool HaveGrowSpace(Plant plant) => HaveGrowSpaceCall(plant);
    }
}
