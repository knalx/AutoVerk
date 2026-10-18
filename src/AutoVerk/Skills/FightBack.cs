using System.Collections.Generic;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;

namespace AutoVerk.Skills
{
    /// <summary>
    /// Steps in when a monster that's after you comes within reach of your best weapon. Deer and other animals
    /// that only flee are left alone.
    /// </summary>
    public sealed class FightBack : Reflex
    {
        public override string Description => "Fight back when a monster comes at you";

        public override Skill Check()
        {
            var player = Senses.Player;
            var weapon = Senses.BestWeapon();
            if (weapon == null) return null;
            float reach = weapon.m_shared.m_attack.m_attackRange;
            foreach (var ai in BaseAI.GetAllInstances())
            {
                if (!(ai is MonsterAI monster)) continue;
                if (!monster.IsAlerted() && monster.GetTargetCreature() != player) continue;
                var c = monster.GetComponent<Character>();
                if (!Senses.IsEnemy(c)) continue;
                if (Senses.FlatDistance(player.transform.position, Senses.NearestPoint(c, player.transform.position)) <= reach)
                    return new Fight(c, weapon);
            }
            return null;
        }
    }

    /// <summary>Take out the weapon, strike the monster until it falls or gets away, then put back what was in hand.</summary>
    public sealed class Fight : Composite
    {
        private readonly Character _enemy;
        private readonly ItemDrop.ItemData _weapon;

        public Fight(Character enemy, ItemDrop.ItemData weapon)
        {
            _enemy = enemy;
            _weapon = weapon;
        }

        protected override IEnumerable<Step> Run()
        {
            var before = Senses.InHand();
            yield return Do(new Equip(_weapon));
            yield return Try(new Strike(_enemy));
            var outcome = Last;
            if (before != null && before != _weapon) yield return Try(new Equip(before));
            Report = outcome.Ok ? "it fell" : outcome.ToString();
        }

        public override string ToString() => $"fight {(_enemy != null ? Utils.GetPrefabName(_enemy.gameObject) : "the monster")}";
    }
}
