# expected-behavior

Version 1.0.0. See the [changes](#changes) at the end.

## What It Does

A Claude Code skill that writes the Expected Behavior list for a feature or fix you just built: a short walkthrough of how it's supposed to work, for a QA tester who didn't see the work and doesn't read code. It replies with only the list, so it pastes straight into a ticket, a test plan or a card's checklist.

Items follow the player's path through the feature, as "When X, Y should happen", plus "If at any point…" items for actions such as leaving or cancelling. The list gives QA the context to run through the feature rather than every edge case, uses the names the player sees (no code names, logs or millisecond timings) and is usually 4 to 7 items. For example, for a boss countdown that now pauses the wave:

```markdown
- When a player rings the Boss Bell, the wave should pause, meaning enemies should stop moving and no new enemies should spawn, and the Boss Warning countdown should start.
- When the Boss Warning countdown ends, the boss should appear and the wave should resume, with new enemies spawning depending on the difficulty the player has chosen.
- When the boss is defeated and the rewards screen is showing, the wave should pause again.
- When the rewards screen closes, the wave should resume as normal.
- If at any point the player leaves the match, the wave should end as expected.
- If at any point the player rings the Boss Bell again while the countdown is running, the countdown should end immediately and the boss should appear.
```

It never puts a guess on the list: when it isn't sure an item is right or belongs, it asks you first.

## Requirements

- Claude Code (the desktop app or the terminal CLI)
- Python 3.9 or newer, for the installer
- Tested on Windows 11. macOS and Linux: untested.

## Quick Start

From the root of the cloned repo (PowerShell, or bash with `python3` on macOS and Linux):

```bash
cd skills/expected-behavior
python install.py
```

It ends with **Setup complete**. Start a new Claude Code session.

## Install with Claude Code

Say to Claude Code: *"Clone https://github.com/masfew/toolbox and install expected-behavior."* With a clone already there, open Claude Code in it and say *"Install expected-behavior."* Claude follows the repo's install rules: it does the steps under Claude does, and reads out the steps under You do one at a time, waiting for each.

**Claude does:**

1. Checks Python 3.9 or newer is installed (`python --version`, or `python3 --version` on macOS and Linux), and asks before installing it if it's missing, with the command from [Install](#install) step 1.
2. Runs `python install.py` from `skills/expected-behavior` (`python3` on macOS and Linux), and checks it ends with **Setup complete** and that `~/.claude/skills/expected-behavior/SKILL.md` now exists.

**You do:**

1. Start a new Claude Code session, so it loads the skill.

## Install

1. **Python 3.9 or newer.** Check with `python --version` (on macOS and Linux, `python3 --version`). If it's missing, on Windows run `winget install Python.Python.3.13` and open a new terminal; on macOS, `brew install python`.
2. From this folder, run `python install.py` (PowerShell or bash). It:
   - puts the tool at `~/.claude/tools/expected-behavior`. In a git clone that's a link to this folder (a directory junction on Windows, which needs no admin rights; a symlink on macOS and Linux), so a `git pull` updates the tool. From an unzipped release it's a copy, and the unzipped folder can be deleted afterwards. `--copy` or `--link` overrides the choice.
   - links `~/.claude/skills/expected-behavior` to the `skill` folder in `~/.claude/tools/expected-behavior`
   - saves what it installed to `~/.claude/tools/expected-behavior.install.json`
3. Start a new Claude Code session.

The install is safe to rerun. It never adds an entry twice and changes nothing else under `~/.claude`.

## Configuration

There are no settings. To add rules of your own, such as a QA tag at the end of every item, see [Adding Your Own Rules](#adding-your-own-rules).

## Usage

After Claude has finished building a feature or fix in the same conversation, say:

> Write up the expected behavior.

Claude works from the conversation and the changed code, and replies with only the bullet list. Other ways to ask that load the skill:

- *"What should QA test for the new pause menu?"* after building it.
- *"Write acceptance criteria for the achievements popup I just built, it's going on a Jira ticket."*
- In a new conversation, name the feature: *"Write up the expected behavior for the respawn timer fix."* Claude reads the recent changes and checks with you which feature you mean.
- To have Claude add the items to a card or ticket itself, ask for that and name the card. It only writes to a board when you ask.

In many board tools, pasting several lines into a checklist's add-item box makes one item per line, so the whole list goes in at once.

## How It Works

- `skill/SKILL.md` holds the rules: where the items come from (the conversation and the code, never test results), the player's-path order, how each item is worded, what to leave out, and the output format. It works out the engine from the project (`.verse` files mean UEFN; a Rojo project or `.luau` files mean Roblox) and refers to things the way the player sees them there.
- `skill/references/examples.md` holds three worked examples, each with what the first draft got wrong and what the review changed. Claude reads it for lists with the same shape: one rule for several phases, a prompt plus a setting that persists, and a toggle with a reset.
- `skill/evals/triggers.json` holds 20 test prompts, 10 that should load the skill and 10 near-misses that shouldn't, for checking the description after a change.

Everything the install adds points at `~/.claude/tools/expected-behavior`, never at the folder you installed from: the skill link under `~/.claude/skills`. Only `~/.claude/tools/expected-behavior` itself points at that folder (or holds a copy of it). The install also keeps a record of what it added, in `~/.claude/tools/expected-behavior.install.json`. A rerun reads it first and takes out anything the earlier install added that the new one doesn't, and `--remove` reads it to undo everything.

## Adding Your Own Rules

The skill follows your project's own rules on top of its own, so you don't need to edit it for a team convention. Put the rule where Claude reads it for that project, such as the project's `CLAUDE.md` or a skill of your own for your board, for example:

```markdown
## Expected Behavior
Every Expected Behavior item ends with one QA tag: [auto] when an automated test fully proves it, [manual] when only a person can check it.
```

The skill then writes its usual list with your tag on each item. Rules that hold for every project, such as a better way to word an item, belong in `skill/SKILL.md` itself.

## Updating

Read the [changes](#changes) first for anything that needs you to do something. Each version is also on the repo's [releases page](https://github.com/masfew/toolbox/releases), tagged `expected-behavior-v<version>`, with its notes and a zip of this folder.

From the root of the cloned repo, pull the new version, then rerun the install (PowerShell or bash):

```bash
git pull
cd skills/expected-behavior
python install.py
```

To stay on a version, check out its tag instead, then rerun the install (PowerShell or bash):

```bash
git fetch --tags
git checkout expected-behavior-v1.0.0
cd skills/expected-behavior
python install.py
```

This moves the whole clone, so every other tool in it goes back to that point too. To return to the latest version, run `git checkout main` and `git pull`, then rerun the install. To keep this tool on one version without moving the others, download its zip from the release instead, unzip it, and run `python install.py` from the `expected-behavior` folder inside it. The install copies the tool to `~/.claude/tools/expected-behavior`, so the unzipped folder can be deleted afterwards.

If the tool's folder moves or is renamed (for example when the repo is reorganised), the install keeps working as it is. Rerunning `python install.py` from the new folder points `~/.claude/tools/expected-behavior` there and takes out anything stale.

## Uninstall

From the tool's folder (PowerShell or bash):

```bash
python install.py --remove
```

If you installed from a release zip and deleted the unzipped folder, run the same command from `~/.claude/tools/expected-behavior` instead.

This removes everything the install added: the skill link, `~/.claude/tools/expected-behavior` and its install record, even if the install was made from a different folder.

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| Claude doesn't list the `expected-behavior` skill | Claude Code reads skills when a session starts | Start a new session, or restart Claude Code |
| The list has items about how the code works, or every edge case | The skill didn't load, so Claude wrote a generic test list | Ask again with "write up the expected behavior", or say "use the expected-behavior skill" |
| Claude asks you questions before writing the list | It wasn't sure an item was right, and it never guesses on a QA list | Answer them; the list follows |
| Your QA tags or house wording are missing | The rule isn't anywhere Claude reads for that project | Add it to the project's `CLAUDE.md`, as [Adding Your Own Rules](#adding-your-own-rules) shows |
| `FAILED: ~/.claude/tools/expected-behavior exists but isn't this tool` | Something else is in that folder | Move it elsewhere, then rerun |

## License

MIT. See the [LICENSE](https://github.com/masfew/toolbox/blob/main/LICENSE) file at the root of the repo.

## Changes

- 1.0.0: First public version.
