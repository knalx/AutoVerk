using AutoVerk.Engine;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Basic
{
    /// <summary>
    /// Swing the melee weapon in hand at a target until it dies or breaks: a monster, a tree, a rock. Aims the
    /// view at its middle through the whole swing, since the hit lands partway through and the target may move.
    /// Never moves the character: fails with too_far when it's out of reach. Not named Attack: the game has a
    /// global class of that name, which would hide this one.
    /// </summary>
    public sealed class Strike : Primitive
    {
        private const float StartTimeout = 1f;
        private const float SwingTimeout = 4f;
        private const int MaxRefusedFrames = 60;
        /// <summary>A swing reaches this much past the weapon's range, from the body to the target's surface.</summary>
        private const float Slack = 0.5f;

        private readonly Component _target;
        private readonly int _maxSwings;
        private readonly string _name;

        private int _swings;
        private int _refused;
        private bool _swinging;
        private bool _sawSwing;
        private double _swingAt;

        public Strike(Component target, int maxSwings = 30)
        {
            _target = target;
            _maxSwings = maxSwings;
            _name = target != null ? Utils.GetPrefabName(target.gameObject) : "the target";
        }

        /// <summary>Unity's == treats a destroyed object as null: a felled tree or a broken rock.</summary>
        private bool Down => _target == null || (_target is Character c && c.IsDead());

        protected override Result Begin()
        {
            if (ReferenceEquals(_target, null)) return Result.Fail("not_found", "nothing to strike");
            if (Down) return Result.Fail("gone", $"{_name} is gone");
            if (!Senses.IsMeleeWeapon(Senses.InHand())) return Result.Fail("no_tool", "no melee weapon in hand");
            // After a pause the swing count carries on, so the try limit holds.
            _swinging = false;
            _refused = 0;
            return Result.Running;
        }

        protected override Result Tick()
        {
            var p = Senses.Player;
            if (_swinging)
            {
                if (!Down) Body.LookAt(Senses.Center(_target), tilt: true);
                // The swing starts a few frames after the press: wait for it to start, then to end.
                bool inSwing = p.InAttack();
                if (inSwing) _sawSwing = true;
                double t = Elapsed - _swingAt;
                if ((!_sawSwing && t < StartTimeout) || (inSwing && t < SwingTimeout)) return Result.Running;
                _swinging = false;
            }

            if (Down)
            {
                Report = $"{_swings} swing(s)";
                return Result.Success;
            }
            var weapon = Senses.InHand();
            if (!Senses.IsMeleeWeapon(weapon)) return Result.Fail("no_tool", "the weapon is no longer in hand");
            var attack = weapon.m_shared.m_attack;
            var surface = Senses.NearestPoint(_target, p.transform.position);
            float distance = Senses.FlatDistance(p.transform.position, surface);
            if (distance > attack.m_attackRange + Slack)
                return Result.Fail("too_far", $"{_name} is {distance:0.0} m away, the swing reaches {attack.m_attackRange:0.0} m");
            if (_swings >= _maxSwings) return Result.Fail("timeout", $"{_name} still stands after {_swings} swings");
            if (!p.HaveStamina(attack.m_attackStamina))
                return Result.Fail("no_stamina", $"stamina {p.GetStamina():0}, a swing needs {attack.m_attackStamina:0}");

            Body.LookAt(Senses.Center(_target), tilt: true);
            Status = $"striking {_name} (swing {_swings + 1})";
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
            return Result.Running;
        }

        public override string ToString() => $"strike {_name}";
    }
}
