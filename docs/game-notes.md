# Game notes

Valheim facts skills rely on (game 1.0.20). Add what you learn, so the next person doesn't dig it up again.

## API

| Concept | API |
|---|---|
| Local player | `Player.m_localPlayer` |
| Stamina, weight | `GetStamina()`, `GetMaxStamina()`, `IsEncumbered()` |
| Inventory | `GetInventory().GetAllItems()`; item id = `ItemData.m_dropPrefab.name` |
| Equip | `Humanoid.EquipItem(ItemData)` |
| Movement | `Player.SetControls(...)` from `PlayerController.FixedUpdate` |
| Use key | private `Player.Interact(GameObject, hold, alt)`; reach 3.5 m from the eyes |
| Ripe crop | `Pickable`; grown from a `Plant` (`m_grownPrefabs`) |
| Monsters | `MonsterAI`: `IsAlerted()`, `GetTargetCreature()` |

## Placing (build, cultivate, plant)

- Aim by patching private `Player.PieceRayTest`; place with one place press, handled by `Player.UpdatePlacement`.
- Placing costs the tool's attack stamina and has a 0.4 s cooldown.
- No-build zones (the spawn area) refuse all placement.

## Farming

- Find the cultivate piece by data: its `TerrainOp` paints `Cultivate`. Cultivating levels the ground.
- A sapling's `m_resources` names its seed; match by data, not names.
- `Plant.HaveGrowSpace()` fails on any solid collider within `m_growRadius` (a rock, a piece), not just
  another plant.
- `Plant.HaveGrowSpace()` needs about 0.7 m between plants. Call it on the placement ghost to check a spot
  without using a seed.
- Plants need open sky to grow; placing doesn't check that.
- Nothing can be planted under water. `Floating.GetLiquidLevel(point)` is the water (or tar) surface there,
  -10000 with none: a spot is wet when it's above the ground.
- Cultivating levels the patch to the ground under the player, so a sapling placed before can float up to a
  metre above the ground: look for plants in a column, not just on the ground.
- A grown crop of another field within the grow radius makes a spot `NoSpace`.

## Gathering

- Wild plants (berries, mushrooms, thistle) are `Pickable`s that grow back (`m_respawnTimeMinutes > 0`), but
  so are branches, flint and loose stones. What tells them apart is what they give: food, or a light herb
  (0.1), against wood, flint and stone (1 or more).
- `Pickable.CanBePicked()` is ripe; a picked berry bush stays, with its berries hidden.

## Picking up

- Walking within about 2 m of a drop picks it up by itself (auto-pickup), so a drop can vanish before Use.
- Use on a chest or a workbench opens a menu, and an open menu pauses the run.

## Mining and fighting

- `Humanoid.StartAttack(null, false)` is one swing. It starts a few frames later and hits partway through
  (`InAttack()` is true meanwhile), so keep aiming until it ends; the game won't switch items mid-swing.
- A melee swing aims with the view's pitch (private `m_lookPitch`), not just the facing.
- A pickaxe swing destroys saplings nearby: clear rocks before planting.
- A swing is a fan of sphere casts (`Attack.DoMeleeAttack`), each stopping at the first thing it touches: a
  pickaxe cast that reaches the ground first digs a hole. `Mine` replays the fan to pick a pitch.
- Rocks are `Destructible`, `MineRock` or `MineRock5`; a pickaxe breaks them unless their pickaxe damage
  modifier is `Immune`, and only from `m_minToolTier` up. A `MineRock5` breaks part by part: a broken part's
  collider is switched off, and the rock is destroyed with its last part.
- Deer (`AnimalAI`) count as enemies but only flee; only `MonsterAI` attacks.

## Input

- The game uses Unity's new Input System; match keys with `ZInput`, not by name.
