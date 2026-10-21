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
2. Commit and push: the package is built from a commit, and the store page's images load from GitHub.
3. `scripts/package.sh` makes `dist/AutoVerk-<version>.zip` from the current commit (or
   `scripts/package.sh <tag>`). It builds a clean copy, so uncommitted changes stay out, and runs the tests
   first. Set `VALHEIM_DIR` if the game isn't in the default Steam folder.
4. Upload it at [thunderstore.io/package/create](https://thunderstore.io/package/create/): community
   Valheim, categories like Tweaks and Client-side.

`thunderstore/README.md` is the mod's page on Thunderstore, for players; keep it in step with this README.
Thunderstore has no screenshot upload: images go in that README with full URLs, like
`https://raw.githubusercontent.com/knalx/AutoVerk/main/docs/images/plant-preview.jpg`, so put them in
`docs/images/` and push them. Keep them small (a JPEG about 1600 px wide).
