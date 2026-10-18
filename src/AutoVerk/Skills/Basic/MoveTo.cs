using AutoVerk.Engine;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Basic
{
    /// <summary>Walk or run in a straight line to a point or an object. Jumps when stuck, gives up after that.</summary>
    public sealed class MoveTo : Primitive
    {
        private const float StuckSeconds = 1.5f;
        private const float Timeout = 30f;
        private const int MaxJumps = 2;

        private readonly Vector3 _point;
        private readonly Component _target;
        private readonly float _stopAt;
        private readonly bool _run;
        private readonly string _name;

        private float _closest;
        private double _closestAt;
        private int _jumps;

        public MoveTo(Vector3 point, float stopAt = 0.5f, bool run = false)
        {
            _point = point;
            _stopAt = stopAt;
            _run = run;
            _name = "the point";
        }

        public MoveTo(Component target, float stopAt = 1.5f, bool run = false)
        {
            _target = target;
            _stopAt = stopAt;
            _run = run;
            _name = target != null ? Utils.GetPrefabName(target.gameObject) : "the target";
        }

        private bool Gone => !ReferenceEquals(_target, null) && _target == null;
        private Vector3 Destination => _target != null ? _target.transform.position : _point;

        protected override Result Begin()
        {
            if (Gone) return Result.Fail("gone", $"{_name} is gone");
            _closest = float.MaxValue;
            _closestAt = Elapsed;
            _jumps = 0;
            return Result.Running;
        }

        protected override Result Tick()
        {
            if (Gone) return Result.Fail("gone", $"{_name} is gone");
            var player = Senses.Player;
            float distance = Senses.FlatDistance(player.transform.position, Destination);
            if (distance <= _stopAt) return Result.Success;
            if (player.IsSwimming()) return Result.Fail("water_in_path", $"started swimming, {distance:0} m from {_name}");
            if (Elapsed > Timeout) return Result.Fail("timeout", $"not there after {Timeout:0} s, {distance:0} m from {_name}");

            if (distance < _closest - 0.1f)
            {
                _closest = distance;
                _closestAt = Elapsed;
            }
            else if (Elapsed - _closestAt > StuckSeconds)
            {
                if (_jumps >= MaxJumps) return Result.Fail("stuck", $"no closer for {StuckSeconds} s, {distance:0.0} m from {_name}");
                _jumps++;
                _closestAt = Elapsed;
                Body.Jump();
            }

            Body.WalkToward(Destination, _run);
            Status = $"walking to {_name} ({distance:0} m)";
            return Result.Running;
        }

        public override string ToString() => $"move_to {_name}";
    }
}
