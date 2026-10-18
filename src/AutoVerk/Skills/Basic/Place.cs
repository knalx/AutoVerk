using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Basic
{
    /// <summary>
    /// Place a piece from the build tool in hand (hammer, cultivator, hoe) at a spot within reach: select it, aim
    /// placement at the spot, press place once. The game's own rules decide, and take the cost. A plant is checked
    /// for grow space first, so a seed isn't wasted.
    /// </summary>
    public sealed class Place : Primitive
    {
        private const float ConfirmSeconds = 0.6f;

        private readonly Vector3 _spot;
        private readonly string _name;
        private Piece _piece;

        private int _aimedFrames;
        private double _pressedAt;
        private float _toolUseBefore;

        public Place(Piece piece, Vector3 spot)
        {
            _piece = piece;
            _spot = spot;
            _name = piece != null ? Utils.GetPrefabName(piece.gameObject) : "the piece";
        }

        /// <summary>The piece by its prefab name (wood_floor), looked up in the tool in hand when it starts.</summary>
        public Place(string pieceName, Vector3 spot)
        {
            _spot = spot;
            _name = pieceName;
        }

        protected override Result Begin()
        {
            _aimedFrames = 0;
            _pressedAt = -1;
            var p = Senses.Player;
            var table = Senses.BuildPieces();
            if (table == null || !p.InPlaceMode()) return Result.Fail("no_tool", "no build tool in hand");
            if (_piece == null)
                _piece = table.m_pieces.Where(g => g != null && g.name == _name).Select(g => g.GetComponent<Piece>()).FirstOrDefault();
            if (_piece == null || !table.m_pieces.Contains(_piece.gameObject))
                return Result.Fail("no_tool", $"the {Senses.ItemId(Senses.InHand())} can't place {_name}");
            if (!p.IsPieceAvailable(_piece)) return Result.Fail("refused", $"{_name} isn't unlocked yet");

            float reach = p.m_maxPlaceDistance + _piece.m_extraPlacementDistance;
            float distance = Senses.EyeDistance(_spot);
            if (distance >= reach) return Result.Fail("too_far", $"the spot is {distance:0.0} m away, reach {reach:0.0} m");
            float stamina = Senses.InHand().m_shared.m_attack.m_attackStamina;
            if (!p.HaveStamina(stamina)) return Result.Fail("no_stamina", $"stamina {p.GetStamina():0}, placing needs {stamina:0}");
            if (!GameApi.NoPlacementCost(p) && !p.HaveRequirements(_piece, Player.RequirementMode.CanBuild))
                return Result.Fail("no_materials", "missing " + string.Join(", ", _piece.m_resources
                    .Where(r => r.m_resItem != null).Select(r => $"{r.m_amount} {r.m_resItem.gameObject.name}")));
            if (!p.SetSelectedPiece(_piece)) return Result.Fail("refused", $"can't select {_name}");
            return Result.Running;
        }

        protected override Result Tick()
        {
            var p = Senses.Player;
            Body.AimPlacement(_spot);
            Body.LookAt(_spot);

            if (_pressedAt < 0)
            {
                // The game moves the placement ghost to the aim in its own update: give it a frame first.
                if (_aimedFrames++ < 1) return Result.Running;
                var status = p.GetPlacementStatus();
                if (status != Player.PlacementStatus.Valid) return Refused(status);
                var ghost = GameApi.PlacementGhost(p);
                var plant = ghost != null ? ghost.GetComponent<Plant>() : null;
                if (plant != null && !GameApi.HaveGrowSpace(plant))
                    return Result.Fail("no_space", "too close to something that would stop it growing");
                // The last swing or placement is still cooling down.
                if (Time.time - GameApi.LastToolUseTime(p) <= p.m_placeDelay) return Result.Running;
                _toolUseBefore = GameApi.LastToolUseTime(p);
                Body.Place();
                _pressedAt = Elapsed;
                return Result.Running;
            }

            if (GameApi.LastToolUseTime(p) != _toolUseBefore) return Result.Success;
            if (Elapsed - _pressedAt > ConfirmSeconds)
            {
                var status = p.GetPlacementStatus();
                return status != Player.PlacementStatus.Valid ? Refused(status) : Result.Fail("refused", $"the game didn't place {_name}");
            }
            return Result.Running;
        }

        private static Result Refused(Player.PlacementStatus status)
        {
            switch (status)
            {
                case Player.PlacementStatus.NeedCultivated: return Result.Fail("not_cultivated", "the ground isn't cultivated");
                case Player.PlacementStatus.NoRayHits: return Result.Fail("too_far", "no ground within reach at the spot");
                case Player.PlacementStatus.NoBuildZone: return Result.Fail("no_build_zone", "the game forbids building here");
                case Player.PlacementStatus.PrivateZone: return Result.Fail("no_build_zone", "someone's ward protects this area");
                case Player.PlacementStatus.MoreSpace: return Result.Fail("no_space", "too close to another piece");
                default: return Result.Fail("refused", $"placement refused: {status}");
            }
        }

        public override string ToString() => $"place {_name}";
    }
}
