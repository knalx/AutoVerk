using System.Collections.Generic;
using AutoVerk.Engine;
using AutoVerk.Game;
using HarmonyLib;
using UnityEngine;

namespace AutoVerk.Skills.Basic
{
    /// <summary>
    /// Break a rock, or one part of a big one, with the pickaxe in hand. Never moves the character: fails with
    /// too_far when it's out of reach.
    ///
    /// Every swing has to land on the rock: a melee swing is a fan of sphere casts that each stop at the first
    /// thing they touch, so one that reaches the ground first digs a hole. Before each swing it replays that fan
    /// for a range of view pitches and aims where the most casts reach the rock and the fewest touch the ground or
    /// anything else. A swing that doesn't hit the rock stops it, so it never digs blindly.
    /// </summary>
    public sealed class Mine : Primitive
    {
        private const float StartTimeout = 1f;
        private const float SwingTimeout = 4f;
        private const int MaxRefusedFrames = 60;
        /// <summary>Turn to within this many degrees of the rock before working out the aim.</summary>
        private const float FacingSlack = 5f;

        private static int _swingMask;

        private readonly Component _rock;
        private readonly GameObject _go;
        private readonly Collider _part;
        private readonly int _maxSwings;
        private readonly string _name;

        private int _swings;
        private int _refused;
        private int _hitsBefore;
        private bool _swinging;
        private bool _sawSwing;
        private double _swingAt;
        private float _pitch;

        /// <param name="part">One collider of the rock: done when it breaks. Null to break the whole rock.</param>
        public Mine(Component rock, Collider part = null, int maxSwings = 40)
        {
            _rock = rock;
            _go = rock != null ? rock.gameObject : null;
            _part = part;
            _maxSwings = maxSwings;
            _name = rock != null ? Utils.GetPrefabName(rock.gameObject) : "the rock";
        }

        /// <summary>Unity's == treats a destroyed object as null. A broken part of a big rock is switched off.</summary>
        private bool Down => _rock == null || (!ReferenceEquals(_part, null) && (_part == null || !_part.gameObject.activeInHierarchy));

        private Vector3 AimPoint => _part != null ? _part.bounds.center : Senses.Center(_rock);

        protected override Result Begin()
        {
            if (ReferenceEquals(_rock, null)) return Result.Fail("not_found", "nothing to mine");
            if (Down) return Result.Fail("gone", $"{_name} is gone");
            var tool = Senses.InHand();
            if (!Senses.IsPickaxe(tool)) return Result.Fail("no_tool", "no pickaxe in hand");
            int tier = Rocks.Tier(_rock);
            if (tool.m_shared.m_toolTier < tier)
                return Result.Fail("no_tool", $"the {Senses.ItemId(tool)} can't break {_name}, it needs tier {tier}");
            // After a pause the swing count carries on, so the try limit holds.
            HitCounter.Start(_go);
            _swinging = false;
            _refused = 0;
            return Result.Running;
        }

        protected override Result Tick()
        {
            var p = Senses.Player;
            if (_swinging)
            {
                if (!Down) Body.LookAt(AimPoint, _pitch);
                // The swing starts a few frames after the press: wait for it to start, then to end.
                bool inSwing = p.InAttack();
                if (inSwing) _sawSwing = true;
                double t = Elapsed - _swingAt;
                if ((!_sawSwing && t < StartTimeout) || (inSwing && t < SwingTimeout)) return Result.Running;
                _swinging = false;
                if (!Down && HitCounter.Hits == _hitsBefore)
                    return Result.Fail("missed", $"swing {_swings} didn't hit {_name}; stopped so it doesn't dig");
            }

            if (Down)
            {
                Report = $"{_swings} swing(s)";
                return Result.Success;
            }
            var tool = Senses.InHand();
            if (!Senses.IsPickaxe(tool)) return Result.Fail("no_tool", "the pickaxe is no longer in hand");
            var attack = tool.m_shared.m_attack;
            if (_swings >= _maxSwings) return Result.Fail("timeout", $"{_name} still stands after {_swings} swings");
            if (!p.HaveStamina(attack.m_attackStamina))
                return Result.Fail("no_stamina", $"stamina {p.GetStamina():0}, a swing needs {attack.m_attackStamina:0}");

            // Turn first: the aim is worked out from where the body faces.
            var target = AimPoint;
            var to = target - p.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(p.transform.forward, to) > FacingSlack)
            {
                if (++_refused > MaxRefusedFrames) return Result.Fail("refused", $"couldn't turn to {_name}");
                Body.LookAt(target);
                Status = $"turning to {_name}";
                return Result.Running;
            }

            var aim = BestAim(p, attack);
            if (aim.RockRays == 0)
            {
                float distance = Senses.FlatDistance(p.transform.position, Rocks.NearestSurface(Rocks.Colliders(_rock, _part), p.transform.position));
                return distance > attack.m_attackRange
                    ? Result.Fail("too_far", $"{_name} is {distance:0.0} m away, the swing reaches {attack.m_attackRange:0.0} m")
                    : Result.Fail("missed", $"no swing from here reaches {_name}, {aim.Blocker ?? "the ground"} is in the way");
            }
            _pitch = aim.Pitch;
            Body.LookAt(target, _pitch);
            Status = $"mining {_name} (swing {_swings + 1})";
            if (!Body.Attack())
            {
                if (++_refused > MaxRefusedFrames) return Result.Fail("refused", "the game wouldn't start a swing");
                return Result.Running;
            }
            _refused = 0;
            _swings++;
            _swinging = true;
            _sawSwing = false;
            _swingAt = Elapsed;
            _hitsBefore = HitCounter.Hits;
            return Result.Running;
        }

        protected override void End() => HitCounter.Stop(_go);

        public override string ToString() => $"mine {_name}";

        // ---- Aiming: a replay of Attack.GetMeleeAttackDir and Attack.DoMeleeAttack ----

        private sealed class Aim
        {
            public float Pitch;
            public int RockRays, GroundRays, OtherRays;
            public string Blocker;
            public float Score => RockRays - GroundRays - 2f * OtherRays;
        }

        private Aim BestAim(Player player, Attack attack)
        {
            Aim best = null;
            for (float pitch = -40f; pitch <= 70f; pitch += 5f)
            {
                var aim = Simulate(player, attack, pitch);
                if (best == null || (aim.RockRays > 0 && (best.RockRays == 0 || aim.Score > best.Score))) best = aim;
            }
            return best;
        }

        /// <summary>What each cast of a swing with the view at this pitch would hit first.</summary>
        private Aim Simulate(Player player, Attack attack, float pitch)
        {
            var body = player.transform;
            var forward = body.forward;
            // GetMeleeAttackDir: the view gives the vertical angle, the body the horizontal, limited by m_maxYAngle.
            var eyeDir = Quaternion.AngleAxis(pitch, body.right) * forward;
            var aimDir = new Vector3(forward.x, eyeDir.y, forward.z).normalized;
            var attackDir = Vector3.RotateTowards(forward, aimDir, Mathf.Deg2Rad * attack.m_maxYAngle, 10f);

            var joint = string.IsNullOrEmpty(attack.m_attackOriginJoint) ? body
                : Utils.FindChild(player.GetVisual().transform, attack.m_attackOriginJoint) ?? body;
            var origin = joint.position + Vector3.up * attack.m_attackHeight + body.right * attack.m_attackOffset;
            var local = body.InverseTransformDirection(attackDir);

            var aim = new Aim { Pitch = pitch };
            var others = new Dictionary<string, int>();
            float half = attack.m_attackAngle / 2f;
            for (float a = -half; a <= half; a += 4f)
            {
                var q = attack.m_attackType == Attack.AttackType.Vertical ? Quaternion.Euler(a, 0f, 0f)
                    : attack.m_attackType == Attack.AttackType.Horizontal ? Quaternion.Euler(0f, -a, 0f) : Quaternion.identity;
                var hit = FirstHit(player, origin, body.TransformDirection(q * local), attack, attackDir);
                if (hit == null) continue;
                var col = hit.Value.collider;
                var obj = Projectile.FindHitObject(col);
                bool onRock = _part != null ? col == _part : obj == _go || (obj != null && obj.transform.IsChildOf(_go.transform));
                if (onRock) aim.RockRays++;
                else if (col.GetComponent<Heightmap>() != null) aim.GroundRays++;
                else
                {
                    aim.OtherRays++;
                    var name = obj != null ? Utils.GetPrefabName(obj) : col.name;
                    others[name] = others.TryGetValue(name, out var n) ? n + 1 : 1;
                }
            }
            foreach (var kv in others)
                if (aim.Blocker == null || kv.Value > others[aim.Blocker]) aim.Blocker = kv.Key;
            return aim;
        }

        private static RaycastHit? FirstHit(Player player, Vector3 origin, Vector3 dir, Attack attack, Vector3 attackDir)
        {
            // The game's m_attackMaskTerrain without the character layers: this swings only at rocks.
            if (_swingMask == 0)
                _swingMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");
            float range = attack.m_attackRange;
            var hits = attack.m_attackRayWidth > 0f
                ? Physics.SphereCastAll(origin, attack.m_attackRayWidth, dir, Mathf.Max(0f, range - attack.m_attackRayWidth), _swingMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(origin, dir, range, _swingMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(player.transform)) continue;
                // Same fallback as DoMeleeAttack for casts that start inside a collider.
                var point = h.point == Vector3.zero
                    ? (h.collider is MeshCollider ? origin + dir * range : h.collider.ClosestPoint(origin))
                    : h.point;
                if (attack.m_attackAngle < 180f && Vector3.Dot(point - origin, attackDir) <= 0f) continue;
                return h;
            }
            return null;
        }
    }

    /// <summary>
    /// Counts the player's hits that land on the rock being mined, at the rock's own Damage(): a swing that hit
    /// nothing shows as no new hit.
    /// </summary>
    [HarmonyPatch]
    internal static class HitCounter
    {
        private static GameObject _target;

        public static int Hits { get; private set; }

        public static void Start(GameObject target)
        {
            _target = target;
            Hits = 0;
        }

        /// <summary>Stops counting, unless counting has started on another rock since.</summary>
        public static void Stop(GameObject target)
        {
            if (ReferenceEquals(_target, target)) _target = null;
        }

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Destructible), nameof(Destructible.Damage));
            yield return AccessTools.Method(typeof(MineRock), nameof(MineRock.Damage));
            yield return AccessTools.Method(typeof(MineRock5), nameof(MineRock5.Damage));
        }

        private static void Prefix(MonoBehaviour __instance, HitData hit)
        {
            if (_target == null || __instance.gameObject != _target) return;
            if (hit != null && hit.GetAttacker() == Player.m_localPlayer) Hits++;
        }
    }
}
