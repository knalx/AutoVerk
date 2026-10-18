using AutoVerk.Engine;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Basic
{
    /// <summary>
    /// Press Use on an object within reach: pick a crop, pick up a drop, open a door. For pickables and drops it
    /// waits until the game confirms, which takes a moment in multiplayer.
    /// </summary>
    public sealed class Interact : Primitive
    {
        private const float ConfirmSeconds = 2f;

        private readonly Component _target;
        private readonly string _name;
        private bool _pressed;

        public Interact(Component target)
        {
            _target = target;
            _name = target != null ? Utils.GetPrefabName(target.gameObject) : "the target";
        }

        /// <summary>Unity's == treats a destroyed object as null; a picked crop stays but says it's picked.</summary>
        private bool Taken => _target == null || (_target is Pickable p && p.GetPicked());

        /// <summary>Whether the game shows that it worked: the crop is picked, the drop is gone.</summary>
        private bool Confirms => _target is Pickable || _target is ItemDrop;

        protected override Result Begin()
        {
            _pressed = false;
            if (Taken) return Result.Fail("gone", $"{_name} is gone");
            var p = Senses.Player;
            float distance = Senses.EyeDistance(Senses.NearestPoint(_target, p.m_eye.position));
            if (distance > p.m_maxInteractDistance)
                return Result.Fail("too_far", $"{_name} is {distance:0.0} m away, reach {p.m_maxInteractDistance:0.0} m");
            if (_target is Pickable pickable && pickable.m_itemPrefab != null &&
                !p.GetInventory().CanAddItem(pickable.m_itemPrefab, pickable.m_amount))
                return Result.Fail("inventory_full", $"no room for {pickable.m_itemPrefab.name}");
            if (_target is ItemDrop drop && !p.GetInventory().CanAddItem(drop.m_itemData))
                return Result.Fail("inventory_full", $"no room for {_name}");
            return Result.Running;
        }

        protected override Result Tick()
        {
            if (!_pressed)
            {
                Body.LookAt(Senses.Center(_target));
                Body.Use(_target.gameObject);
                _pressed = true;
                return Confirms ? Result.Running : Result.Success;
            }
            if (Taken) return Result.Success;
            if (Elapsed > ConfirmSeconds) return Result.Fail("refused", $"the game didn't take {_name}");
            return Result.Running;
        }

        public override string ToString() => $"interact {_name}";
    }
}
