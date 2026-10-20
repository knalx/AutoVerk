using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AutoVerk.Game
{
    /// <summary>
    /// Natural rocks a pickaxe can break: Destructible, MineRock and MineRock5 objects that pickaxe damage isn't
    /// immune to. Never a build piece, plant, crop or creature.
    /// </summary>
    public static class Rocks
    {
        private static readonly Collider[] Hits = new Collider[64];
        private static int _mask;

        /// <summary>The layers rocks stand on.</summary>
        private static int Mask => _mask != 0 ? _mask : _mask = LayerMask.GetMask("Default", "static_solid", "Default_small");

        /// <summary>The rock a collider belongs to, if a pickaxe can damage it, or null.</summary>
        public static Component Breakable(Collider col)
        {
            if (col.GetComponentInParent<Piece>() || col.GetComponentInParent<Plant>() ||
                col.GetComponentInParent<Pickable>() || col.GetComponentInParent<Character>()) return null;
            if (col.GetComponentInParent<Destructible>() is Destructible d && d.m_damages.m_pickaxe != HitData.DamageModifier.Immune &&
                d.m_damages.m_pickaxe != HitData.DamageModifier.Ignore) return d;
            if (col.GetComponentInParent<MineRock>() is MineRock m && m.m_damageModifiers.m_pickaxe != HitData.DamageModifier.Immune) return m;
            if (col.GetComponentInParent<MineRock5>() is MineRock5 m5 && m5.m_damageModifiers.m_pickaxe != HitData.DamageModifier.Immune) return m5;
            return null;
        }

        /// <summary>
        /// The name the game shows on hover ("Rock", "Copper deposit"), else the prefab's. A big rock's own object
        /// is named after its mesh: ___MineRock5.
        /// </summary>
        public static string Name(Component rock)
        {
            if (rock == null) return "the rock";
            var key = rock is MineRock5 m5 ? m5.m_name : rock is MineRock m ? m.m_name : null;
            if (!string.IsNullOrEmpty(key) && Localization.instance != null) return Localization.instance.Localize(key);
            var name = Utils.GetPrefabName(rock.transform.root.gameObject);
            return name.StartsWith("___") ? "Large rock" : name;
        }

        /// <summary>The lowest pickaxe tier that damages a rock.</summary>
        public static int Tier(Component rock) =>
            rock is Destructible d ? d.m_minToolTier : rock is MineRock m ? m.m_minToolTier : rock is MineRock5 m5 ? m5.m_minToolTier : 0;

        /// <summary>
        /// The breakable rock nearest to a point within some metres, in a column up to 1.5 m above it, and which of
        /// its colliders is nearest: a big rock breaks part by part. Null when there's none.
        /// </summary>
        /// <param name="skip">Parts to leave out, like ones that couldn't be hit.</param>
        public static Component Find(Vector3 pos, float radius, out Collider part, System.Func<Collider, bool> skip = null)
        {
            Component best = null;
            part = null;
            float bestDistance = float.MaxValue;
            int n = Physics.OverlapCapsuleNonAlloc(pos - Vector3.up * 0.3f, pos + Vector3.up * 1.5f, radius, Hits, Mask);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i];
                if (col.isTrigger || !col.enabled || (skip != null && skip(col))) continue;
                var rock = Breakable(col);
                if (rock == null) continue;
                float d = Senses.FlatDistance(pos, ClosestPoint(col, pos));
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = rock;
                part = col;
            }
            return best;
        }

        /// <summary>
        /// Closest point on a collider. Unity's ClosestPoint only works for primitives and convex meshes; for a
        /// non-convex mesh (most rocks) it returns the query point itself, so the bounds stand in.
        /// </summary>
        public static Vector3 ClosestPoint(Collider col, Vector3 pos) =>
            col is MeshCollider mc && !mc.convex ? col.bounds.ClosestPoint(pos) : col.ClosestPoint(pos);

        /// <summary>A rock's solid colliders, or just the one part.</summary>
        public static Collider[] Colliders(Component rock, Collider part = null) =>
            part != null ? new[] { part } : rock.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();

        /// <summary>
        /// The nearest point of the rock's surface to pos, measured flat. A non-convex mesh is probed with a fan of
        /// flat rays at several heights (Collider.Raycast works on any mesh); its bounds would put the surface too close.
        /// </summary>
        public static Vector3 NearestSurface(IEnumerable<Collider> colliders, Vector3 pos)
        {
            Vector3? best = null;
            float bestDistance = float.MaxValue;
            void Consider(Vector3 p)
            {
                float d = Senses.FlatDistance(p, pos);
                if (d >= bestDistance) return;
                bestDistance = d;
                best = p;
            }
            foreach (var c in colliders)
            {
                if (!(c is MeshCollider mc) || mc.convex)
                {
                    Consider(c.ClosestPoint(pos));
                    continue;
                }
                var b = c.bounds;
                var toward = b.center - pos;
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.0001f)
                {
                    Consider(b.ClosestPoint(pos));
                    continue;
                }
                bool hitAny = false;
                float lo = b.min.y + 0.05f, hi = Mathf.Min(b.max.y - 0.05f, pos.y + 2f);
                for (int i = 0; i <= 6; i++)
                {
                    var origin = new Vector3(pos.x, hi > lo ? Mathf.Lerp(lo, hi, i / 6f) : b.center.y, pos.z);
                    for (float angle = -60f; angle <= 60f; angle += 5f)
                    {
                        var dir = Quaternion.Euler(0f, angle, 0f) * toward.normalized;
                        if (!c.Raycast(new Ray(origin, dir), out var hit, 50f)) continue;
                        Consider(hit.point);
                        hitAny = true;
                    }
                }
                if (!hitAny) Consider(b.ClosestPoint(pos));
            }
            return best ?? pos;
        }
    }
}
