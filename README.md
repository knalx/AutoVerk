# AutoVerk

A Valheim mod that does chores for you. Simple skills like *walk there* or *pick that up* combine into
bigger ones like *gather the carrots* or *build me a farm*. Anyone can add their own.

AutoVerk plays by the game's rules: it presses the same controls you would. Press any key to take control
back.

> **Status:** early version: expect rough edges, and tell us about them in
> [issues](https://github.com/knalx/AutoVerk/issues).

## Install

With a mod manager (r2modman, Thunderstore Mod Manager), install AutoVerk from Thunderstore. By hand: needs [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
[Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Copy `AutoVerk.dll` to
`BepInEx/plugins/AutoVerk/`.

## Use

- **F7** opens the window (change it with `Key` under `[Window]` in `BepInEx/config/autoverk.cfg`, like
  `Key = F8` or `Key = K + LeftAlt`): pick a task, fill in its fields, press **Start**. While it runs, the
  window shows how far it got; when it's done, what happened, with **Fill the rest** to carry on later. The Settings tab
  turns interruptions on and off, like fighting back when a monster comes at you, and whether a run jumps
  when it's stuck on the way and stops when your pack gets too heavy.
- `gather` picks what's ripe around you: everything you can plant, everything wild (berries, mushrooms,
  thistle) or one kind, then what fell on the ground. **Also** picks up the stones and wood lying around.
  **Preview** puts poles on what it would take: green plants, yellow stones and branches.
- `plant` saves each new field in `BepInEx/config/AutoVerk/<world>.fields.txt`, so you can fill it up
  later. Checkboxes choose whether it picks up stones on the spots and breaks rocks in the way (needs a
  pickaxe). **Preview** puts poles on the spots first: green ready, yellow a stone or rock to clear, red to
  be skipped, blue planted already. **Hide** takes them away.
- Or in the console (F5): `autoverk run gather plants=Carrot`, `autoverk preview plant seed=CarrotSeeds`,
  `autoverk stop`, `autoverk skills`. A run starts
  when you close the console.
- Any key, click, gamepad button or open menu pauses the run. **Resume** in the window carries on.

## Add a skill

```csharp
public sealed class ChopNearestTree : Composite
{
    protected override IEnumerable<Step> Run()
    {
        var tree = Senses.Nearest<TreeBase>(radius: 30);
        if (tree == null) yield return Fail("not_found", "no tree within 30 m");
        yield return Do(new Equip("AxeStone"));
        yield return Do(new MoveTo(tree));
        yield return Do(new Strike(tree));
    }
}
```

See [writing skills](docs/writing-skills.md).

## Docs

- [Architecture](docs/architecture.md): how it works.
- [Writing skills](docs/writing-skills.md): add your own.
- [Game notes](docs/game-notes.md): Valheim facts skills rely on.
- [Contributing](CONTRIBUTING.md): build, test, pull requests.

## License

[MIT](LICENSE)
