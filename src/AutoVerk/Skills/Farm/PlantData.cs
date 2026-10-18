using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// What a seed grows, read from game data: the sapling piece that uses the seed, what it grows into, and how
    /// close two can stand. Nothing is matched by a hard-coded name, so other mods' crops work.
    /// </summary>
    public sealed class PlantData
    {
        public string Seed { get; private set; }
        /// <summary>The sapling piece's prefab name: sapling_carrot.</summary>
        public string Sapling { get; private set; }
        /// <summary>The item the grown crop gives: Carrot.</summary>
        public string Crop { get; private set; }
        public float GrowRadius { get; private set; }
        /// <summary>
        /// The closest two plants can stand: the game's grow-space check fails on a grown crop or a healthy plant
        /// within the grow radius, so it's the radius plus the neighbour's reach, rounded up to 5 cm.
        /// </summary>
        public float MinSpacing { get; private set; }
        /// <summary>Prefab names the sapling grows into: Pickable_Carrot.</summary>
        public HashSet<string> Grown { get; private set; }

        private static readonly Dictionary<string, PlantData> Cache = new Dictionary<string, PlantData>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _cropPrefabs;
        private static ZNetScene _cachedFor;

        /// <summary>The plant for a seed item, or null if nothing plantable uses it. Needs a loaded world.</summary>
        public static PlantData ForSeed(string seed)
        {
            if (string.IsNullOrEmpty(seed) || !Fresh()) return null;
            if (Cache.TryGetValue(seed, out var cached)) return cached;
            PlantData data = null;
            foreach (var go in ZNetScene.instance.m_prefabs)
            {
                var plant = go != null ? go.GetComponent<Plant>() : null;
                var piece = go != null ? go.GetComponent<Piece>() : null;
                if (plant == null || piece == null) continue;
                var uses = piece.m_resources.FirstOrDefault(r => r.m_resItem != null &&
                    string.Equals(r.m_resItem.gameObject.name, seed, StringComparison.OrdinalIgnoreCase));
                if (uses == null) continue;

                var grown = plant.m_grownPrefabs.Where(g => g != null).ToArray();
                var crop = grown.Select(g => g.GetComponent<Pickable>()).FirstOrDefault(p => p != null && p.m_itemPrefab != null);
                float reach = Mathf.Max(Reach(go), grown.Select(Reach).DefaultIfEmpty(0f).Max());
                data = new PlantData
                {
                    Seed = uses.m_resItem.gameObject.name,
                    Sapling = go.name,
                    Crop = crop != null ? crop.m_itemPrefab.name : uses.m_resItem.gameObject.name,
                    GrowRadius = plant.m_growRadius,
                    MinSpacing = Mathf.Ceil((plant.m_growRadius + reach) * 20f) / 20f,
                    Grown = new HashSet<string>(grown.Select(g => g.name)),
                };
                break;
            }
            Cache[seed] = data;
            return data;
        }

        /// <summary>The seeds you carry that something plantable uses.</summary>
        public static IEnumerable<string> SeedsCarried()
        {
            if (Senses.Player == null) return Enumerable.Empty<string>();
            return Senses.Player.GetInventory().GetAllItems().Select(Senses.ItemId)
                .Where(id => id != null).Distinct().Where(id => ForSeed(id) != null).OrderBy(id => id).ToList();
        }

        /// <summary>A pickable that some plant grows into (a carrot, barley), not a stone or a mushroom.</summary>
        public static bool IsCrop(string prefab)
        {
            if (prefab == null || !Fresh()) return false;
            if (_cropPrefabs == null)
            {
                _cropPrefabs = new HashSet<string>();
                foreach (var go in ZNetScene.instance.m_prefabs)
                {
                    var plant = go != null ? go.GetComponent<Plant>() : null;
                    if (plant == null) continue;
                    foreach (var g in plant.m_grownPrefabs)
                        if (g != null && g.GetComponent<Pickable>() != null) _cropPrefabs.Add(g.name);
                }
            }
            return _cropPrefabs.Contains(prefab);
        }

        /// <summary>The prefabs come with the world: forget what was read for the last one.</summary>
        private static bool Fresh()
        {
            if (ZNetScene.instance == null) return false;
            if (_cachedFor == ZNetScene.instance) return true;
            Cache.Clear();
            _cropPrefabs = null;
            _cachedFor = ZNetScene.instance;
            return true;
        }

        /// <summary>Flat distance from the prefab's origin to the far edge of its solid colliders.</summary>
        private static float Reach(GameObject prefab)
        {
            float reach = 0f;
            foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                var s = c.transform.lossyScale;
                float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
                float r;
                Vector3 center;
                switch (c)
                {
                    case SphereCollider sc: r = sc.radius * scale; center = sc.center; break;
                    case CapsuleCollider cc: r = cc.radius * scale; center = cc.center; break;
                    case BoxCollider bc: r = new Vector2(bc.size.x * Mathf.Abs(s.x), bc.size.z * Mathf.Abs(s.z)).magnitude / 2f; center = bc.center; break;
                    case MeshCollider mc when mc.sharedMesh != null:
                        var e = mc.sharedMesh.bounds.extents;
                        r = new Vector2(e.x * Mathf.Abs(s.x), e.z * Mathf.Abs(s.z)).magnitude;
                        center = mc.sharedMesh.bounds.center;
                        break;
                    default: continue;
                }
                var offset = c.transform.position - prefab.transform.position;
                var worldCenter = offset + c.transform.rotation * Vector3.Scale(center, s);
                reach = Mathf.Max(reach, new Vector2(worldCenter.x, worldCenter.z).magnitude + r);
            }
            return reach;
        }
    }
}
