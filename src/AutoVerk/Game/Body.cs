using HarmonyLib;
using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>
    /// Presses the player's controls for primitives. Every control lasts one frame: the plugin clears them all
    /// before each tick, so when no primitive sets them the player has the controls back.
    /// </summary>
    public static class Body
    {
        internal static Vector3? WalkTarget;
        internal static Vector3? LookTarget;
        internal static bool Running;
        internal static bool JumpPressed;

        /// <summary>Walk (or run) toward a point, as if holding Forward while facing it.</summary>
        public static void WalkToward(Vector3 point, bool run = false)
        {
            WalkTarget = point;
            Running = run;
            LookTarget ??= point;
        }

        /// <summary>Face a point (sideways only).</summary>
        public static void LookAt(Vector3 point) => LookTarget = point;

        /// <summary>Press Jump once.</summary>
        public static void Jump() => JumpPressed = true;

        public static bool Active => WalkTarget != null || LookTarget != null || JumpPressed;

        internal static void Clear()
        {
            WalkTarget = null;
            LookTarget = null;
            Running = false;
            JumpPressed = false;
        }
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    internal static class PlayerControllerPatch
    {
        private static void Postfix(PlayerController __instance)
        {
            var player = Player.m_localPlayer;
            if (!Body.Active || player == null || __instance.gameObject != player.gameObject) return;

            if (Body.LookTarget != null)
            {
                var look = Body.LookTarget.Value - player.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f) player.SetLookDir(look.normalized);
            }
            var move = Vector3.zero;
            if (Body.WalkTarget != null)
            {
                var dir = Body.WalkTarget.Value - player.transform.position;
                dir.y = 0f;
                // The game moves relative to the look direction: forward, once facing the target.
                if (dir.sqrMagnitude > 0.0001f)
                {
                    player.SetLookDir(dir.normalized);
                    move = Vector3.forward;
                }
            }
            bool jump = Body.JumpPressed;
            Body.JumpPressed = false;
            player.SetControls(move, attack: false, attackHold: false, secondaryAttack: false, secondaryAttackHold: false,
                block: false, blockHold: false, jump: jump, crouch: false, run: Body.Running, autoRun: false);
        }
    }
}
