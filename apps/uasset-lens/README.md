# UAsset Lens

Version 1.0.0. See the [changes](#changes) at the end.

## What It Does

A Windows tool that Plastic (Unity Version Control) opens when you diff or resolve a UEFN `.uasset` or `.umap` file. Instead of "binary file", you see the actor's name from UEFN, its type in plain words, and the settings that changed. In a merge conflict you keep one whole side, and the window tells you exactly what the other side changed that you'd lose.

Plastic itself is unchanged. UAsset Lens is registered as Plastic's external diff and merge tool for `.uasset;.umap`, so check-in, update and merge work as they always have. For a trigger device that was moved, a diff reads like this (`--text` output; the window shows the same):

```text
Start Gate (Trigger)  [Trigger Device]
/mymap/__ExternalActors__/mymap/0/02/K3J8D2M5Q1W7E4R9T6Y0U2
1 settings changed
-- Position
* Location: X 828.357 · Y 686.81 · Z 478.025 -> X 828.357 · Y 430.811 · Z 478.025
Summary: Start Gate (Trigger): moved
```

## Requirements

- Windows 10 or 11, 64-bit. Tested on Windows 11. macOS and Linux aren't supported.
- Plastic SCM / Unity Version Control, opened and closed at least once, so its settings file `%LOCALAPPDATA%\plastic4\client.conf` exists. Tested with Plastic 11.0.16.
- UEFN files from Fortnite 42.30. Other releases may need reader updates (see [Troubleshooting](#troubleshooting)).
- Python 3.9 or newer, for the installer.
- To build the exe: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Not needed if you install the exe from a release instead (Install, step 2). The installed exe needs nothing else.
- Claude Code is only needed to install it by asking Claude. UAsset Lens itself doesn't use Claude.

## Quick Start

Close Plastic and Gluon, then from the root of the cloned repo (PowerShell):

```powershell
cd apps/uasset-lens
python install.py
```

It builds the exe, registers it with Plastic, and ends with **Setup complete**. Open Plastic and diff a `.uasset` file.

## Install with Claude Code

Say to Claude Code: *"Clone https://github.com/masfew/toolbox and install uasset-lens."* With a clone already there, open Claude Code in it and say *"Install uasset-lens."* Claude follows the repo's install rules: it does the steps under Claude does, and reads out the steps under You do one at a time, waiting for each.

**Claude does:**

1. Checks this is Windows, that Python 3.9 or newer is installed, and that `%LOCALAPPDATA%\plastic4\client.conf` exists. If it's missing, it asks you to open Plastic once and close it, which creates the file.
2. Checks for the .NET 10 SDK (`dotnet --list-sdks` lists a `10.` version). If it's missing, it asks whether to install it with `winget install Microsoft.DotNet.SDK.10` (about 250 MB), or to download `UAssetLens.exe` from the latest uasset-lens release on the [releases page](https://github.com/masfew/toolbox/releases) instead.
3. Tells you the install backs up Plastic's `client.conf` and adds UAsset Lens as the diff and merge tool for `.uasset` and `.umap`, and waits for your yes.
4. Waits for you to close Plastic and Gluon (You do, step 1), then runs `python install.py` from `apps/uasset-lens`, or `python install.py --exe <path to the downloaded exe>`, and checks it ends with **Setup complete**. Building takes a minute or two. If it says to close Plastic first, it asks you again and reruns.

**You do:**

1. Before Claude runs the installer: close Plastic and Gluon completely, including from the system tray.
2. Afterwards, open Plastic from the taskbar or the Start menu, not from a terminal, and diff any `.uasset` file to check UAsset Lens opens.

## Install

1. **Python 3.9 or newer.** Check with `python --version`. If it's missing, run `winget install Python.Python.3.13`, then open a new terminal.
2. **The exe, one of two ways:**
   - Build it: install the .NET 10 SDK with `winget install Microsoft.DotNet.SDK.10`, then open a new terminal. The installer builds the exe.
   - Or download `UAssetLens.exe` from the latest uasset-lens release on the [releases page](https://github.com/masfew/toolbox/releases), and pass it to the installer with `--exe`.
3. **Close Plastic and Gluon.** Plastic rewrites `client.conf` when it closes, which would undo the install. The installer refuses to run while either is open.
4. From this folder, run `python install.py` (or `python install.py --exe <path to UAssetLens.exe>`) in PowerShell. It:
   - builds one self-contained `UAssetLens.exe` into `%LOCALAPPDATA%\UAssetLens`, or copies the `--exe` file there
   - backs up `%LOCALAPPDATA%\plastic4\client.conf` (as `client.conf.bak-uassetlens-<date>`) and adds a diff entry and a merge entry for `.uasset;.umap` above Plastic's own, so Plastic uses UAsset Lens for those files and its own tools for everything else
   - puts the tool at `~/.claude/tools/uasset-lens` (a link to this folder in a git clone, a copy otherwise), so `--remove` also works from there
   - when the Claude desktop app runs the install, reruns itself outside the app's package, so the exe and the settings change land in your real AppData folder. It says so when it does.
   - saves what it installed to `~/.claude/tools/uasset-lens.install.json`
5. Open Plastic and diff a `.uasset` file.

The install is safe to rerun after pulling changes: it rebuilds the exe and replaces its own entries rather than adding more. `install.ps1` does the Plastic part on its own too, if you'd rather not use Python: `.\install.ps1` installs, `-SkipBuild` only rewrites the entries, `-ExePath <file>` installs a downloaded exe, and `-Uninstall` takes everything out.

## Configuration

There are no settings. The entries the install adds to `client.conf` are:

```text
diff  --before "@sourcefile" --after "@destinationfile" --before-name "@sourcesymbolic" --after-name "@destinationsymbolic"
merge --base "@basefile" --theirs "@sourcefile" --yours "@destinationfile" --out "@output" --base-name "@basesymbolic" --theirs-name "@sourcesymbolic" --yours-name "@destinationsymbolic"
```

## Usage

- **Diff:** in Pending Changes, a changeset or a branch diff, choose Diff on a `.uasset` or `.umap`. The window shows the name and type, then the changed settings grouped as device options, position, and everything else, with the stored path and the two versions in the footer. **Show unchanged** lists every setting. **Copy summary** puts a one-line description on the clipboard for the check-in comment. Diffs share one window: a diff opened while it's up replaces what it shows and brings it to the front. **Pin** keeps a window as it is, and the next diff opens a new one. Closing Plastic or Gluon closes the windows it opened, pinned ones too.
- **Conflict:** when a merge or update finds a `.uasset` changed on both sides, Plastic opens the conflict window with Base, Theirs and Yours. Each changed setting is tagged theirs, yours or both. The orange note says what **Keep yours** and **Keep theirs** would each drop. Keep copies that whole file back to Plastic, which marks the conflict resolved. **Cancel** (or closing the window, or closing Plastic) leaves the conflict unresolved, as before.
- After a merge that rewrote actor files, reopen the level in UEFN so it loads the merged versions.

Fields UEFN rewrites without you changing them are hidden and counted in the footer: cached draw and cull distances (recomputed whenever something moves or scales), navigation flags and collision channel lists (rewritten by UEFN updates), editor-only helper meshes, the level's saved camera views, and the Fortnite version stamp. They were found by comparing 152 changed actor files from a real project's Plastic history.

### Command Line

```text
UAssetLens diff  --before FILE --after FILE [--before-name NAME] [--after-name NAME]
UAssetLens merge --base FILE --theirs FILE --yours FILE --out FILE [--base-name NAME] [--theirs-name NAME] [--yours-name NAME]
UAssetLens show  FILE
```

Add `--text` to print the report instead of opening a window, and `--all` to include unchanged settings. `merge` exits 0 after Keep (the kept file is at `--out`) and 1 on Cancel. Every run is logged to `%LOCALAPPDATA%\UAssetLens\uassetlens.log`.

## How It Works

- `src/UAssetLens.Core` reads files with [UAssetAPI](https://github.com/atenfyr/UAssetAPI) 1.1.0 (MIT) and turns them into a name, a type and a flat list of readable settings (`SnapshotReader`), then compares two or three versions (`Compare`). `Reading/` holds the workarounds that let UAssetAPI 1.1.0 read Fortnite 42.30 saves; [CLAUDE.md](CLAUDE.md) explains them.
- `src/UAssetLens.App` holds the WPF diff and conflict windows and the command line, published as one self-contained exe. `DiffHost` passes later diff runs to the open diff window over a named pipe, `DiffWindows` picks the window (the last unpinned one, or a new one), and `Plastic` finds the `plastic.exe` or `gluon.exe` that started a run so its windows close with it.
- `tests/UAssetLens.Tests` holds xUnit tests of the naming and compare logic, plus tests that read real UEFN files from a folder you point them at (below).
- `install.py` checks the requirements and runs `install.ps1`, which builds the exe and edits `client.conf`. `build.ps1` builds the exe for a release.
- Reading about 14,000 actor files in one project takes about 13 seconds.

The install puts nothing under `~/.claude` except `~/.claude/tools/uasset-lens` (a link to this folder, or a copy) and its install record, which `--remove` reads.

## Building and Testing

```powershell
dotnet build UAssetLens.slnx
dotnet test tests/UAssetLens.Tests
```

CI runs the build, the tests and the publish on Windows whenever this folder changes (`.github/workflows/uasset-lens.yml` at the repo root).

### Testing with Your Own Files

No real UEFN files ship with the repo, so the tests that read them are skipped unless `UASSETLENS_FIXTURES` names a folder of your own. Put `.uasset` and `.umap` files from your project in a folder, then (PowerShell):

```powershell
$env:UASSETLENS_FIXTURES = "C:\path\to\my-fixtures"
$env:UPDATE_GOLDEN = "1"; dotnet test tests/UAssetLens.Tests; Remove-Item Env:UPDATE_GOLDEN
dotnet test tests/UAssetLens.Tests
```

The first run saves each file's report to `expected/` in that folder. Later runs check every actor file reads completely, every level reads its world, and every report still matches, so a Fortnite update that changes the file format shows up as a failing test. To test diffs and merges too, add a `cases.json` there:

```json
[
  {"diff": ["trigger_before.uasset", "trigger_after.uasset"], "expected": "trigger.diff"},
  {"merge": ["device_base.uasset", "device_theirs.uasset", "device_yours.uasset"], "expected": "device.merge"}
]
```

Earlier versions of a file come from Plastic with `cm cat "<path>#cs:<changeset>" --file=<output file>`.

## Updating

Read the [changes](#changes) first for anything that needs you to do something. Each version is also on the repo's [releases page](https://github.com/masfew/toolbox/releases), tagged `uasset-lens-v<version>`, with its notes, a zip of this folder and the built `UAssetLens.exe`.

Close Plastic and Gluon, then from the root of the cloned repo pull the new version and rerun the install (PowerShell):

```powershell
git pull
cd apps/uasset-lens
python install.py
```

To stay on a version, check out its tag instead, then rerun the install (PowerShell):

```powershell
git fetch --tags
git checkout uasset-lens-v1.0.0
cd apps/uasset-lens
python install.py
```

This moves the whole clone, so every other tool in it goes back to that point too. To return to the latest version, run `git checkout main` and `git pull`, then rerun the install. Or download that release's `UAssetLens.exe` and rerun `python install.py --exe <path to it>`.

## Uninstall

Close Plastic and Gluon, then from the tool's folder (PowerShell):

```powershell
python install.py --remove
```

If this folder is gone, run the same command from `~/.claude/tools/uasset-lens` instead. This takes UAsset Lens's entries out of `client.conf` (backing it up first), deletes `%LOCALAPPDATA%\UAssetLens`, and removes `~/.claude/tools/uasset-lens` and its install record. Plastic goes back to its own tools for `.uasset` and `.umap`. The `client.conf.bak-uassetlens-<date>` backups each install and removal made stay in `%LOCALAPPDATA%\plastic4`; delete them once you don't need them.

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| `Close Plastic first` | Plastic or Gluon is running, and would undo the change when it closes | Close both, including from the system tray, then rerun |
| Plastic says "Diff tools in client.conf are incorrectly defined" | The exe isn't where `client.conf` points, usually because an install run from the Claude desktop app landed in the app's private copy of AppData | Rerun `python install.py` (it reruns itself outside the app). Test from a Plastic opened from the taskbar, not one started from a terminal |
| Plastic still shows "binary file" | Plastic was open during the install and rewrote `client.conf` when it closed | Close Plastic and rerun the install |
| A file shows "couldn't read" lines | A Fortnite update changed the file format, or the file uses something UAsset Lens doesn't read yet (scene graph entities are listed as unread) | The rest of the file still shows. Check `%LOCALAPPDATA%\UAssetLens\uassetlens.log` |
| `Build failed` | A UAsset Lens window is open and holds the exe, or the .NET 10 SDK is missing | Close the windows and rerun; check `dotnet --list-sdks` |
| `FAILED: the .NET 10 SDK is needed` | The SDK isn't installed | Install it, or use `--exe` with an exe from a release |

## License

MIT. See the [LICENSE](https://github.com/masfew/toolbox/blob/main/LICENSE) file at the root of the repo. UAssetAPI is MIT-licensed too.

## Changes

- 1.0.0: First public version.
