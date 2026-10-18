# AutoVerk

A Valheim mod that does chores for you. Simple skills like *walk there* or *pick that up* combine into
bigger ones like *gather the carrots* or *build me a farm*. Anyone can add their own.

AutoVerk plays by the game's rules: it presses the same controls you would. Press any key to take control
back.

> **Status:** rework in progress. These docs describe the design the code is being built to.

## Install

Needs [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
[Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Copy `AutoVerk.dll` to
`BepInEx/plugins/AutoVerk/`.

## Use

- **F7** opens the window: pick a skill, fill in its fields, press **Start**.
- Or in the console (F5): `autoverk run collect radius=10`, `autoverk stop`, `autoverk skills`. A run starts
  when you close the console.
- Any key, click or open menu pauses the run.

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
