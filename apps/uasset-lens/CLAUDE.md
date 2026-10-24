# UAsset Lens

Read README.md first. Rules and traps for working on this tool:

## Hard Limits

- Never edit `%LOCALAPPDATA%\plastic4\client.conf` while Plastic or Gluon is open: Plastic rewrites it on close and drops the change. `install.ps1` refuses to run then; keep that check.
- Keep the installers' check for the Claude desktop app's package. Anything the app starts has its AppData writes redirected into the app's private copy (`%LOCALAPPDATA%\Packages\Claude_*\LocalCache`), so an exe installed from there is invisible to Plastic, which then reports "Diff tools in client.conf are incorrectly defined". `install.py` reruns itself outside the package (`NEEDS_REAL_APPDATA`), and `install.ps1` does the same when run on its own: it writes a probe file to `%LOCALAPPDATA%` and reruns itself through WMI if the probe lands in a package's `LocalCache`. Those processes don't report a package identity, so `GetCurrentPackageFullName` can't detect this.
- Test the Plastic hookup only in a Plastic the user opened from the taskbar. A Plastic, `cm` or exe started from a Claude session sees the app's private copy and passes even when the real install is missing. For the same reason, a session sees its own `uassetlens.log`, not the one Plastic's launches write.
- Never test merges in a real project's workspace or one with pending changes. Use a throwaway workspace (`cm workspace create`) of a test repository with test branches, then delete the workspace.
- Don't remove the reader workarounds in `src/UAssetLens.Core/Reading` unless a newer UAssetAPI release reads every actor file of a large project without them (below).

## Reading UEFN Files

- UAssetAPI 1.1.0 (NuGet) can't read Fortnite 42.30 editor saves as they are. `Reading/` works around it without forking the library:
  - `AssetLoader.SkipUnneededSections` blanks three package summary entries (gatherable text, editor metadata, import type hierarchies) in the in-memory bytes so UAssetAPI skips them. It finds each by its values plus a neighbouring field and only blanks on a single match. None of them hold anything the viewer shows.
  - `TolerantProperties.cs` replaces UAssetAPI's Text, Array, Set and Map readers. UAssetAPI registers every `PropertyData` subclass in assemblies that reference it, and ours register after its own, so they win. They always finish at the end the property tag gives; without that, one misread value (UEFN text values carry 4 bytes UAssetAPI doesn't expect) loses every property after it.
- Check any reader change against a whole project, not just the test files: before the workarounds, 90 of about 14,000 actor files in one project failed and every Button actor lost its settings. A throwaway console app that runs `SnapshotReader.Read` over `__ExternalActors__` is enough; it takes about 13 seconds.
- UEFN keeps the open level's files locked. Read them with `FileShare.ReadWrite | FileShare.Delete` (`AssetLoader.ReadShared`); a plain `File.Open` fails with "used by another process".
- Device options live twice: as name/value text in `ToyOptionsComponent.PlayerOptionData.PropertyOverrides` (what UEFN's details panel shows) and as properties of the actor. Show the options list and skip the actor's copies.
- Verse devices: the actor's `Script` points at the object holding the @editable fields, `ScriptClassPath` names the Verse class, and `ScriptPropertyOverrides` is a binary copy of the same values (2 KB of bytes that showed up as 934 "changes"). It stays hidden.
- Unreal doesn't save values left at their defaults, so a setting missing from one version shows as "Default" there.
- Fields UEFN rewrites on its own are hidden by `Compare.NoiseProperties` and `Compare.NoiseFragments`. Add to them only with evidence from Plastic history (pull before/after pairs with `cm cat "path#cs:N"` and tally what changes), and note it in the README.

## Plastic

- Plastic 11.0.16 passes temp copies for every version except your workspace file, plus names in two formats: diffs `Plugins/.../X.uasset#cs:979`, merges `MyProject\...\X.uasset` followed by the changeset and the branch, each after an at sign. `VersionNames` handles both.
- In a merge, Plastic's `@sourcefile` is theirs (the branch being merged in) and `@destinationfile` is your workspace file. Exit 0 after copying the kept file to `@output` marks the conflict resolved; any other exit leaves it unresolved. All three were checked with real `cm merge` runs on test branches.
- A diff run that hands off to the open window (`DiffHost`) exits only after that window has read the files, since Plastic may delete its temp copies once the process it started exits. Keep the open window's reply after the read. Merges never hand off: each conflict needs its own process and exit code.
- Windows close when the `plastic.exe` or `gluon.exe` above the run exits (`Plastic.Launcher`, which logs the parent chain). Only those names count: watching the direct parent would close windows at once whenever Plastic starts tools through a short-lived helper. Without Plastic, test with a copy of `powershell.exe` renamed `plastic.exe` that starts the exe as its child.

## Tests

- The tests that read real UEFN files run only when `UASSETLENS_FIXTURES` names a folder of them (README, "Testing with Your Own Files"); no real files are in the repo. Expected output lives in that folder's `expected/`. After a deliberate output change, rerun with `UPDATE_GOLDEN=1` and review the diff.
- The windows have no automated UI tests. To check them, run the exe with real files and screenshot the window, or drive `cm merge` against test branches and press the buttons through UI Automation.
