# Architecture

One BepInEx plugin on Jotunn. Everything runs on Unity's main thread.

```
src/AutoVerk/
  Engine/    Skill, Result, Runner, Registry   plain C#, no Unity, unit-tested
  Game/      Senses (reads the game), Body (presses the controls)
  Skills/    built-in skills and reflexes
  Ui/        window and console commands
tests/AutoVerk.Tests/
examples/HelloSkill/   a separate plugin that adds a skill
```

The UI only talks to the Runner and the Registry, so adding a skill never touches it.

## Skills

A skill does one job and ends **ok** or **failed** with a code (`not_found`) and a reason for the player
("no tree within 30 m").

- **Primitive:** works the game every frame through `Body`. Examples: `move_to`, `equip`, `interact`,
  `place`, `strike`, `rest`.
- **Composite:** one C# method that runs other skills in order. Examples: `pick`, `collect`, `farm.plant`,
  `farm.gather`, `farm.plant`.

Most new skills should be composites.

## A run

The Runner runs one skill tree. Only its deepest skill, a primitive, works the game. Each frame:

1. Clear last frame's controls.
2. **Guards:** your input or an open menu pauses the run; death or leaving the world stops it.
3. **Reflexes:** checks like *a monster is in reach*. One that fires runs its skill (`fight_back`) on top of
   the tree, then the interrupted primitive starts again.
4. Tick the working primitive. When it ends, its parent continues.

Resume after a pause restarts the interrupted primitive, which checks its target again.

## Senses and Body

- **Senses** reads the game: the player, inventory, nearest objects, tools. It changes nothing.
- **Body** presses the controls: walk, look, use, attack, place, equip. Each control lasts one frame, so
  nothing stays pressed after a pause or a crash.

Only primitives use Body.

## Rules for skills

1. **Play fair:** act only through Body. No spawning, teleporting or editing the world.
2. **Read numbers from the game** (reach, stamina, spacing), not from constants.
3. **Find things by what they do,** not by name, so other mods' items work.
4. **Check the target when starting.** It may be gone.
5. **Loop: look, then act.** Read Senses afresh each pass instead of planning everything up front.
6. **Always end:** a timeout, a try limit or a skip list.
