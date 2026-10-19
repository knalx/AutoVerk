# Writing skills

Write a **composite** if the job is "do A, then B" with skills that exist. Write a **primitive** only if
it needs a control no skill has. Write a **reflex** if it should step in during any run.

## Composite

```csharp
public sealed class PickStones : Composite
{
    private readonly float _radius;
    private readonly HashSet<Pickable> _skipped = new HashSet<Pickable>();

    public PickStones(float radius) { _radius = radius; }

    protected override IEnumerable<Step> Run()
    {
        while (true)
        {
            var stone = Senses.Nearest<Pickable>(_radius, p => p.m_itemPrefab?.name == "Stone" && !p.GetPicked() && !_skipped.Contains(p));
            if (stone == null) break;
            yield return Try(new MoveTo(stone));
            if (Last.Ok) yield return Try(new Interact(stone));
            if (!Last.Ok) _skipped.Add(stone);
        }
        yield return Do(new Collect());
    }
}
```

| Step | Does |
|---|---|
| `Do(skill)` | Runs it; if it fails, this skill fails too |
| `Try(skill)` | Runs it; carry on either way, result in `Last` |
| `Wait(seconds)`, `WaitUntil(condition)` | Waits |
| `Fail(code, reason)` | Ends this skill, failed |
| `Pause(reason)` | Pauses the run for the player to decide |

Returning from `Run` ends the skill **ok**. Set `Status` to show what it's doing in the window.

Composites can run composites, so big skills stay short:

```csharp
yield return Do(new ClearArea(field));
yield return Do(new PlantField(field));
yield return Try(new BuildFence(field));
```

## Primitive

```csharp
public sealed class Rest : Primitive
{
    protected override Result Tick()
    {
        var p = Senses.Player;
        if (p.GetStamina() >= p.GetMaxStamina()) return Result.Success;
        if (Elapsed > 60) return Result.Fail("timeout", "stamina not full after 60 s");
        return Result.Running;
    }
}
```

`Begin()` runs at the start (and after a pause), `Tick()` every frame, and `End()` once at the end. Set
`Body` controls in every `Tick`: they last one frame.

## Reflex

```csharp
public sealed class HealWhenLow : Reflex
{
    public override string Description => "Drink a healing mead when health is low";

    public override Skill Check() =>
        Senses.Player.GetHealthPercentage() < 0.3f && Senses.FindItem("MeadHealthMinor") is { } mead ? new Consume(mead) : null;
}
```

`Check()` runs every frame, so keep it cheap. Return `null` when there's nothing to do. The interrupted
primitive starts again afterwards, so put back anything you changed, like the item in hand: see
[`FightBack`](../src/AutoVerk/Skills/FightBack.cs). The window's Settings tab lists each reflex by its
`Description`, and the player turns it on or off there.

## Register

```csharp
Registry.Register(new SkillInfo
{
    Id = "mymod.pick_stones",
    Description = "Pick up every loose stone around you.",
    Params = { Param.Number("radius", "metres", 30) },
    Create = args => new PickStones(args.Number("radius")),
});
Registry.Register(new HealWhenLow());
```

Registered skills appear in the window and the console. Prefix ids with your mod's name. Do this from
your own BepInEx plugin with `[BepInDependency(AutoVerk.Plugin.Guid)]`.

- A building block that's no job on its own, like `move_to`, gets `ForDevelopers = true`: it stays out of
  the window and runs from the console for testing (`autoverk skills all` lists it).
- To offer values in a dropdown, add `.WithSuggestions(() => seeds, "anything")` to a parameter; the second
  argument names the empty value, if it means something.

## Failure codes

Reuse these when one fits: `not_found`, `gone`, `too_far`, `stuck`, `water_in_path`, `no_tool`,
`no_stamina`, `no_materials`, `inventory_full`, `overweight`, `no_space`, `not_cultivated`, `no_build_zone`,
`refused` (the game said no), `timeout`.

## Names

Don't name a class after one of the game's global classes, such as `Attack`, `World` or `Skills`: in other
mods' code the game's class wins. That's why the swing skill is `Strike`.
