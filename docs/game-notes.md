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
- `Plant.HaveGrowSpace()` needs about 0.7 m between plants. Call it on the placement ghost to check a spot
  without using a seed.
- Plants need open sky to grow; placing doesn't check that.

## Picking up

- Walking within about 2 m of a drop picks it up by itself (auto-pickup), so a drop can vanish before Use.
- Use on a chest or a workbench opens a menu, and an open menu pauses the run.

## Mining and fighting

- `Humanoid.StartAttack(null, false)` is one swing. It starts a few frames later and hits partway through
  (`InAttack()` is true meanwhile), so keep aiming until it ends; the game won't switch items mid-swing.
- A melee swing aims with the view's pitch (private `m_lookPitch`), not just the facing.
- A pickaxe swing destroys saplings nearby: clear rocks before planting.
- Deer (`AnimalAI`) count as enemies but only flee; only `MonsterAI` attacks.

## Input

- The game uses Unity's new Input System; match keys with `ZInput`, not by name.
