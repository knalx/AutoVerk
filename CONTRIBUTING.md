# Contributing

```sh
dotnet build src/AutoVerk -c Release   # add -p:ValheimDir="/path/to/Valheim" if needed
dotnet test tests/AutoVerk.Tests
```

- One skill or fix per pull request. Say how you tested it in game.
- Follow the [rules for skills](docs/architecture.md#rules-for-skills).
- Engine changes need tests.
- Learned how something in the game works? Add it to [game notes](docs/game-notes.md).
