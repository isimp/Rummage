# Releasing

Pushing builds the mod. Releasing is a manual workflow that creates a GitHub release and then publishes to [Hexium](https://valheim.hexium.gg/).

Rummage is published on Hexium only. AzuCraftyBoxes, which it needs, is kept up to date on Hexium; its Thunderstore page is deprecated and stops at an older version.

## One-time setup

The icon lives at `packaging/icon.png`: a 256x256 PNG, committed to the repository. The release workflow stops if it is missing or the wrong size.

The README shows `docs/images/screenshot.webp` through its raw GitHub URL, since relative links do not work on the store pages. The release workflow stops if the file is missing.

Hexium needs a token stored as the repository secret `HEXIUM_TOKEN`, created in the Hexium team settings (the settings icon next to your username, then the bottom of the page). The workflow checks for it before building.

## Cutting a release

1. Set the new version in `Rummage.csproj` (`<Version>`), `src/Plugin.cs` (`public const string Version`) and `packaging/manifest.json` (`version_number`). The workflow checks that all three agree.
2. Add a CHANGELOG entry. It ships in the package and appears on the mod page.
3. Push, and wait for the Build workflow to pass.
4. Run the Release workflow from the Actions tab. It asks for the version (without `v`), optional release notes, the draft flag, the team and the categories.

The workflow then runs these steps in order:

| Step | |
|---|---|
| Version check | All three version sources match the input. |
| Icon check | `packaging/icon.png` exists. |
| Screenshot check | `docs/images/screenshot.webp` exists. |
| Token check | `HEXIUM_TOKEN` is set. |
| Build | Against `lib/`, so no game install is needed. |
| Tests | Everything in `tests/Rummage.Tests`. |
| Package | A zip with the manifest, icon, README, CHANGELOG, LICENSE and `Rummage.dll`. The self-test plugin is never packaged. |
| Package check | The zip against Hexium's rules: required files at the root, icon exactly 256x256, name of letters, digits and underscores, description of at most 256 characters. |
| GitHub release | Tags `vX.Y.Z` and attaches the zip and the DLL. |
| Hexium | Uploads and submits the version. |

If publishing fails, fix the cause and run the workflow again with the same version. The GitHub release is updated rather than duplicated, and Hexium refuses a version it already has.

## Categories

Hexium takes category names; Rummage uses Storage, Quality of Life, Crafting, Building, User Interface, Open Source and Valheim 1.0. The current list is at `https://hexium.gg/api/experimental/community/valheim/category/`.

## Install location

Rummage is client-side. Servers do not need it. To use chests another player's game holds, MultiUserChest must be on the server and every client, as MultiUserChest itself requires.

## How the publish works

`tools/publish.py` uses the standard library only:

1. `POST /api/experimental/usermedia/initiate-upload/` returns an upload UUID and a presigned URL per part.
2. `PUT` each part, keeping the returned `ETag`.
3. `POST /api/experimental/usermedia/{uuid}/finish-upload/` with those ETags.
4. `POST /api/experimental/submission/submit/` with `author_name` set to the team, `communities` set to `["valheim"]` and the categories.

For authorization the script tries `Bearer` first and falls back to `Token`, printing which one the server accepted.

It can also be run by hand:

```bash
HEXIUM_TOKEN=... python3 tools/publish.py dist/hexium/Rummage-0.1.0.zip --store hexium --team isimp --categories "Storage,Quality of Life,Crafting,Building,User Interface,Open Source,Valheim 1.0"
python3 tools/publish.py dist/hexium/Rummage-0.1.0.zip --check-only   # validate only
```

## Before a release

Run the tests (`dotnet test tests/Rummage.Tests` and `dotnet test tests/Rummage.Standards`) and the in-game self-test (`/rummage selftest`, see [technical.md](technical.md#self-test)) against the mod versions the manifest names, ideally next to another player so its last part runs too. Rummage reaches AzuCraftyBoxes, MultiUserChest, ExtraSlots and AzuAntiArthriticCrafting by name at run time, so an update to one of them can change what it finds; the log line at startup says what was found.

## After a game update

Regenerate the reference stubs, rebuild and commit `lib/`:

```powershell
pwsh tools/strip-references.ps1
```

The script reads the installed Valheim and BepInEx, replaces every method body with `throw null` and writes the result to `lib/`. The stubs can be compiled against but not run. If you add a reference to `GameReferences.props`, add it to the list in the script as well.
