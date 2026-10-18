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
            var stone = Senses.Nearest<Pickable>(_radius, p => Senses.DropOf(p) == "Stone" && !_skipped.Contains(p));
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
    public override Skill Check() =>
        Senses.Player.GetHealthPercentage() < 0.3f && Senses.FindMead() is { } mead ? new Consume(mead) : null;
}
```

`Check()` runs every frame, so keep it cheap. Return `null` when there's nothing to do.

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
your own BepInEx plugin with `[BepInDependency(AutoVerk.Plugin.Guid)]`; see
[`examples/HelloSkill`](../examples/HelloSkill).

## Failure codes

Reuse these when one fits: `not_found`, `gone`, `too_far`, `stuck`, `no_tool`, `no_stamina`,
`inventory_full`, `overweight`, `refused` (the game said no), `timeout`.
