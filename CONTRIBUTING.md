# Contributing

```sh
dotnet build src/AutoVerk -c Release   # add -p:ValheimDir="/path/to/Valheim" if needed
dotnet test tests/AutoVerk.Tests
```

- One skill or fix per pull request. Say how you tested it in game.
- Follow the [rules for skills](docs/architecture.md#rules-for-skills).
- Engine changes need tests.
- Learned how something in the game works? Add it to [game notes](docs/game-notes.md).

## Release

1. Bump `Plugin.Version` and `version_number` in `thunderstore/manifest.json`, and add the version to
   `thunderstore/CHANGELOG.md`. Raise the dependency versions in the manifest if the mod needs newer ones.
2. `dotnet build src/AutoVerk -c Release -t:Package` makes `dist/AutoVerk-<version>.zip`.
3. Upload it at [thunderstore.io/c/valheim/create](https://thunderstore.io/c/valheim/create/).

`thunderstore/README.md` is the mod's page on Thunderstore, for players; keep it in step with this README.
