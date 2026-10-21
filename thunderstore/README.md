# AutoVerk

A Valheim mod that does chores for you. Simple skills like *walk there* or *pick that up* combine into
bigger ones like *gather the carrots* or *build me a farm*. Anyone can add their own.

AutoVerk plays by the game's rules: it presses the same controls you would. Press any key to take control
back.

![Plant a field: the preview marks each spot before planting](https://raw.githubusercontent.com/knalx/AutoVerk/main/docs/images/plant-preview.jpg)

Early version: expect rough edges, and tell us about them on
[GitHub](https://github.com/knalx/AutoVerk/issues).

## Skills

- **Gather plants**: pick what's ripe around you: everything you can plant, everything wild (berries,
  mushrooms, thistle) or one kind, then what fell on the ground. It can pick up the stones and wood lying
  around too. **Preview** marks what it would take.
- **Plant a field**: plant a field with the Cultivator. It picks up stones and, if you want, breaks rocks
  on the spots (needs a pickaxe), tills and sows; spots under water are skipped. A new field is laid out in
  front of you and saved, so you can fill it up later with **Fill the rest**. **Preview** marks the spots
  in the world first: green ready, yellow a stone or rock to clear, red to be skipped, blue planted already.

While a skill runs, AutoVerk fights back when a monster comes at you, jumps when it's stuck on the way, and
stops when your pack gets too heavy. Turn each off in the Settings tab.

## Use

- **F7** opens the window (another key in the config, below): pick a task, fill in its fields, press **Start**. While it runs, the window
  shows what it's doing; when it's done, what happened.
- Or in the console (F5): `autoverk run gather plants=Carrot`, `autoverk preview plant seed=CarrotSeeds`,
  `autoverk stop`, `autoverk skills`. A run starts when you close the console.
- Any key, click, gamepad button or open menu pauses the run. **Resume** in the window carries on.

Client-side only: other players don't need it, and it works on any server.

## Install

With a mod manager (r2modman, Thunderstore Mod Manager), install AutoVerk; it brings BepInEx and Jotunn.

By hand: install [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
[Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), then copy `AutoVerk.dll` to
`BepInEx/plugins/AutoVerk/`.

## Config

`BepInEx/config/autoverk.cfg`: the window key, the window's position, each interruption on or off,
`JumpWhenStuck` under `[Movement]` and `StopWhenOverweight` under `[Run]`.
The key is `Key` under `[Window]`: a key name, then any modifiers, like `Key = F8` or `Key = K + LeftAlt`.
Mod managers can edit the file for you, or change it in game with
[Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/).
Saved fields are in `BepInEx/config/AutoVerk/<world>.fields.txt`.

## Add a skill

Skills are small C# classes. See [writing skills](https://github.com/knalx/AutoVerk/blob/main/docs/writing-skills.md).
