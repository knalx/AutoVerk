using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    public enum SpotState
    {
        /// <summary>Bare ground.</summary>
        Empty,
        /// <summary>Cultivated ground, ready for a seed.</summary>
        Cultivated,
        /// <summary>This field's sapling, or its crop grown.</summary>
        Planted,
        /// <summary>A stone or a branch to pick up first.</summary>
        Loose,
        /// <summary>Someone else's plant or crop.</summary>
        Taken,
        /// <summary>A rock, a stump, a building.</summary>
        Blocked,
    }

    /// <summary>What stands on one field spot, looked at fresh.</summary>
    public static class SpotProbe
    {
        private static readonly Collider[] Hits = new Collider[64];
        private static int _solid;
        private static int _withItems;

        /// <summary>Rocks, trees, stumps, plants and pieces: what stands on the ground.</summary>
        private static int Solid => _solid != 0 ? _solid
            : _solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");

        // Loose stones and branches may sit on the item layer.
        private static int WithItems => _withItems != 0 ? _withItems : _withItems = Solid | LayerMask.GetMask("item");

        /// <param name="what">What's on it: the prefab name of the plant, pickable or blocker.</param>
        public static SpotState Probe(Vector3 pos, float spacing, PlantData plant, out Component on, out string what)
        {
            float radius = Mathf.Min(0.4f, spacing * 0.45f);
            Plant onPlant = null;
            Pickable onPickable = null;
            string blocker = null;
            // A column, not a ball on the ground: cultivating levels the ground to the player's feet, so a plant
            // placed before can float up to a metre above it.
            int n = Physics.OverlapCapsuleNonAlloc(pos - Vector3.up * 0.3f, pos + Vector3.up * 1.5f, radius, Hits, WithItems);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i];
                if (col.GetComponentInParent<Character>() != null || col.GetComponentInParent<ItemDrop>() != null) continue;
                var pickable = col.GetComponentInParent<Pickable>();
                if (pickable != null)
                {
                    if (!pickable.GetPicked()) onPickable = onPickable ?? pickable;
                    continue;
                }
                var p = col.GetComponentInParent<Plant>();
                if (p != null) onPlant = onPlant ?? p;
                else if (((1 << col.gameObject.layer) & Solid) != 0) blocker = blocker ?? Utils.GetPrefabName(col.transform.root.gameObject);
            }

            if (onPickable != null)
            {
                on = onPickable;
                what = Utils.GetPrefabName(onPickable.gameObject);
                return plant.Grown.Contains(what) ? SpotState.Planted : PlantData.IsCrop(what) ? SpotState.Taken : SpotState.Loose;
            }
            if (onPlant != null)
            {
                on = onPlant;
                what = Utils.GetPrefabName(onPlant.gameObject);
                return what == plant.Sapling ? SpotState.Planted : SpotState.Taken;
            }
            on = null;
            what = blocker;
            if (blocker != null) return SpotState.Blocked;
            var ground = Heightmap.FindHeightmap(pos);
            return ground != null && ground.IsCultivated(pos) ? SpotState.Cultivated : SpotState.Empty;
        }

        /// <summary>The Cultivator in the inventory: the tool whose pieces paint cultivated ground. Null without one.</summary>
        public static ItemDrop.ItemData FindCultivator()
        {
            foreach (var item in Senses.Player.GetInventory().GetAllItems())
                if (CultivatePiece(item?.m_shared?.m_buildPieces) != null && !(item.m_shared.m_useDurability && item.m_durability <= 0))
                    return item;
            return null;
        }

        /// <summary>The tool's piece whose terrain op paints Cultivate (cultivate_v2 in 1.0.17), or null.</summary>
        public static Piece CultivatePiece(PieceTable table)
        {
            if (table == null) return null;
            foreach (var go in table.m_pieces)
            {
                var piece = go != null ? go.GetComponent<Piece>() : null;
                var op = piece != null ? piece.GetComponent<TerrainOp>() : null;
                if (op != null && op.m_settings.m_paintType == TerrainModifier.PaintType.Cultivate) return piece;
            }
            return null;
        }

        /// <summary>The tool's sapling piece for a plant, or null.</summary>
        public static Piece SaplingPiece(PieceTable table, PlantData plant)
        {
            if (table == null) return null;
            foreach (var go in table.m_pieces)
                if (go != null && go.name == plant.Sapling) return go.GetComponent<Piece>();
            return null;
        }
    }
}
