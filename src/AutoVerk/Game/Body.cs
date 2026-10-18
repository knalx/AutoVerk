using HarmonyLib;
using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>
    /// Presses the player's controls for primitives. Every control lasts one frame: the plugin clears them all
    /// before each tick, so when no primitive sets them the player has the controls back. Presses (use, attack,
    /// equip, place) happen at once, as one tap of the key.
    /// </summary>
    public static class Body
    {
        internal static Vector3? WalkTarget;
        internal static Vector3? LookTarget;
        internal static bool Tilt;
        internal static bool Running;
        internal static bool JumpPressed;
        internal static Vector3? PlaceAim;

        /// <summary>Walk (or run) toward a point, as if holding Forward while facing it.</summary>
        public static void WalkToward(Vector3 point, bool run = false)
        {
            WalkTarget = point;
            Running = run;
            LookTarget ??= point;
        }

        /// <summary>Face a point. With tilt, also aim the view up or down at it, as melee swings need.</summary>
        public static void LookAt(Vector3 point, bool tilt = false)
        {
            LookTarget = point;
            Tilt = tilt;
        }

        /// <summary>Press Jump once.</summary>
        public static void Jump() => JumpPressed = true;

        /// <summary>Press Use on an object, as if the crosshair were on it.</summary>
        public static void Use(GameObject target) => GameApi.Interact(Senses.Player, target);

        /// <summary>Press Attack once with what's in hand. False when the game won't swing now (staggered, mid-dodge).</summary>
        public static bool Attack() => Senses.Player.StartAttack(null, false);

        /// <summary>Equip an item from the inventory, as a hotbar key does. False when the game refuses.</summary>
        public static bool Equip(ItemDrop.ItemData item) => Senses.Player.EquipItem(item);

        /// <summary>Point the build tool's placement at a spot on the ground, instead of the crosshair.</summary>
        public static void AimPlacement(Vector3 spot) => PlaceAim = spot;

        /// <summary>Press Place once; the game places the selected piece where placement aims.</summary>
        public static void Place() => GameApi.PlacePressedTime(Senses.Player) = Time.time;

        public static bool Active => WalkTarget != null || LookTarget != null || JumpPressed;

        internal static void Clear()
        {
            WalkTarget = null;
            LookTarget = null;
            Tilt = false;
            Running = false;
            JumpPressed = false;
            PlaceAim = null;
        }
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    internal static class PlayerControllerPatch
    {
        private static void Postfix(PlayerController __instance)
        {
            var player = Player.m_localPlayer;
            if (!Body.Active || player == null || __instance.gameObject != player.gameObject) return;

            if (Body.LookTarget != null) Face(player, Body.LookTarget.Value, Body.Tilt);
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

        private static void Face(Player player, Vector3 point, bool tilt)
        {
            var to = point - player.m_eye.position;
            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            player.SetLookDir(flat.normalized);
            player.FaceLookDirection();
            if (tilt) GameApi.LookPitch(player) = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -45f, 45f);
        }
    }

    /// <summary>Aims placement at Body.PlaceAim: a ray straight down onto the spot, with the game's own masks and reach.</summary>
    [HarmonyPatch(typeof(Player), "PieceRayTest")]
    internal static class PieceRayTestPatch
    {
        private static bool Prefix(Player __instance, ref bool __result, ref Vector3 point, ref Vector3 normal,
            ref Piece piece, ref Heightmap heightmap, ref Collider waterSurface, bool water)
        {
            if (Body.PlaceAim == null || __instance != Player.m_localPlayer) return true;

            point = Vector3.zero;
            normal = Vector3.zero;
            piece = null;
            heightmap = null;
            waterSurface = null;
            __result = false;

            int mask = water ? GameApi.PlaceWaterRayMask(__instance) : GameApi.PlaceRayMask(__instance);
            var spot = Body.PlaceAim.Value;
            if (!Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 10f, mask)) return false;

            float reach = __instance.m_maxPlaceDistance;
            var ghost = GameApi.PlacementGhost(__instance);
            var ghostPiece = ghost ? ghost.GetComponent<Piece>() : null;
            if (ghostPiece != null) reach += ghostPiece.m_extraPlacementDistance;
            if (!hit.collider || hit.collider.attachedRigidbody || Vector3.Distance(__instance.m_eye.position, hit.point) >= reach)
                return false;

            point = hit.point;
            normal = hit.normal;
            piece = hit.collider.GetComponentInParent<Piece>();
            heightmap = hit.collider.GetComponent<Heightmap>();
            waterSurface = hit.collider.gameObject.layer == LayerMask.NameToLayer("Water") ? hit.collider : null;
            __result = true;
            return false;
        }
    }
}
