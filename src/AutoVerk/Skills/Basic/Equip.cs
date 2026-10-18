using AutoVerk.Engine;
using AutoVerk.Game;

namespace AutoVerk.Skills.Basic
{
    /// <summary>Equip an inventory item by id. Waits for a swing or dodge to finish, since the game won't switch then.</summary>
    public sealed class Equip : Primitive
    {
        private const float Timeout = 3f;

        private readonly string _id;
        private ItemDrop.ItemData _item;

        public Equip(string itemId) { _id = itemId; }

        public Equip(ItemDrop.ItemData item)
        {
            _item = item;
            _id = Senses.ItemId(item);
        }

        protected override Result Begin()
        {
            // The item may have moved, broken or been dropped since the skill was made.
            var p = Senses.Player;
            if (_item == null || !p.GetInventory().ContainsItem(_item)) _item = Senses.FindItem(_id);
            if (_item == null) return Result.Fail("not_found", $"no {_id} in the inventory");
            if (!_item.IsEquipable()) return Result.Fail("refused", $"{_id} can't be equipped");
            return p.IsItemEquiped(_item) ? Result.Success : Result.Running;
        }

        protected override Result Tick()
        {
            var p = Senses.Player;
            if (p.IsItemEquiped(_item)) return Result.Success;
            if (Elapsed > Timeout) return Result.Fail("timeout", $"still busy after {Timeout:0} s, {_id} not equipped");
            if (p.InAttack() || p.InDodge())
            {
                Status = $"waiting to equip {_id}";
                return Result.Running;
            }
            return Body.Equip(_item) ? Result.Running : Result.Fail("refused", $"the game wouldn't equip {_id}");
        }

        public override string ToString() => $"equip {_id}";
    }
}
