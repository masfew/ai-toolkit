<!--
Starting a new tool:
1. Copy this folder to a new folder named after the tool (lowercase, hyphens).
2. Replace every <placeholder> and the guidance under each heading. Keep every heading.
3. Set TOOL_NAME, FILES, HOOKS and PROGRAMS in install.py, and delete config.example.json if the tool has no settings.
4. Add the tool's row to the table in the root README.
5. Delete this comment.
-->
# <Tool Name>

Version <0.1.0>. See the [changes](#changes) at the end.

## What It Does

Two sentences: what the tool does and who it's for. Then a screenshot, a GIF or a short before-and-after example, with nothing personal in it.

## Requirements

- Claude Code (<the desktop app, the terminal CLI, or both>)
- Python 3.9 or newer
- <Any other program, with the version it needs, and any account, for example a phone app>
- Tested on Windows 11. <macOS and Linux: untested.>

## Quick Start

The shortest path to a working install, from the root of the cloned repo (PowerShell or bash):

```bash
cd <tool-name>
python install.py
```

## Install

1. <Install anything from Requirements that's missing, and how.>
2. From the tool's folder, run `python install.py` (PowerShell or bash). It:
   - puts the tool at `~/.claude/tools/<tool-name>`. In a git clone that's a link to this folder (a directory junction on Windows, which needs no admin rights; a symlink on macOS and Linux), so a `git pull` updates the tool. From an unzipped release it's a copy, and the unzipped folder can be deleted afterwards. `--copy` or `--link` overrides the choice.
   - <links which files under `~/.claude/tools/<tool-name>` to which places under `~/.claude`>
   - <adds which entries to `~/.claude/settings.json`, after backing it up>
   - creates `~/.claude/<tool-name>.json` from `config.example.json` if you don't have one yet
   - saves what it installed to `~/.claude/tools/<tool-name>.install.json`
3. <Edit the config file, if the tool has one.>
4. Start a new Claude Code session.

The install is safe to rerun. It never adds an entry twice, and it leaves your other settings alone.

## Configuration

Settings are read from `~/.claude/<tool-name>.json`, which the install creates from `config.example.json`.

| Setting | What it does | Default | Example |
|---|---|---|---|
| `<name>` | <what it changes> | <default> | <example> |

## Usage

Real examples of using the tool and what to expect from each.

## How It Works

Which files it reads and changes, and what runs when. For example: which hook runs on which event, and what it does.

Everything the install adds points at `~/.claude/tools/<tool-name>`, never at the folder you installed from: the hook commands in `settings.json` and the links under `~/.claude`. Only `~/.claude/tools/<tool-name>` itself points at that folder (or holds a copy of it). The install also keeps a record of what it added, in `~/.claude/tools/<tool-name>.install.json`. A rerun reads it first and takes out anything the earlier install added that the new one doesn't, and `--remove` reads it to undo everything.

## Updating

Read the [changes](#changes) first for anything that needs you to do something. Each version is also on the repo's [releases page](https://github.com/masfew/ai-toolkit/releases), tagged `<tool-name>-v<version>`, with its notes and a zip of this folder.

From the root of the cloned repo, pull the new version, then rerun the install (PowerShell or bash):

```bash
git pull
cd <tool-name>
python install.py
```

To stay on a version, check out its tag instead, then rerun the install (PowerShell or bash):

```bash
git fetch --tags
git checkout <tool-name>-v<0.1.0>
cd <tool-name>
python install.py
```

This moves the whole clone, so every other tool in it goes back to that point too. To return to the latest version, run `git checkout main` and `git pull`, then rerun the install. To keep this tool on one version without moving the others, download its zip from the release instead, unzip it, and run `python install.py` from the `<tool-name>` folder inside it. The install copies the tool to `~/.claude/tools/<tool-name>`, so the unzipped folder can be deleted afterwards.

If the tool's folder moves or is renamed (for example when the repo is reorganised), the install keeps working as it is. Rerunning `python install.py` from the new folder points `~/.claude/tools/<tool-name>` there and takes out anything stale.

## Uninstall

From the tool's folder (PowerShell or bash):

```bash
python install.py --remove
```

If you installed from a release zip and deleted the unzipped folder, run the same command from `~/.claude/tools/<tool-name>` instead.

This removes everything the install added, including `~/.claude/tools/<tool-name>` and its install record, even if the install was made from a different folder. It leaves your config file, `~/.claude/<tool-name>.json`, in place. Delete it yourself if you don't need it.

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| <the error or what you see> | <why it happens> | <what to do> |

## License

MIT. See the [LICENSE](../LICENSE) file at the root of the repo.

## Changes

- <0.1.0>: <First version.>
