# AI Toolkit

A personal, unofficial collection of tools for [Claude Code](https://claude.com/claude-code): hooks, skills, output styles and helper scripts. It isn't made, endorsed or supported by Anthropic.

Each tool lives in its own folder, works on its own, and has a README that covers what it does, how to install it, how to configure it and how to remove it.

## Tools

The tools are grouped by kind: skills, hooks, output styles and so on.

### Skills

| Tool | What it does | Tested on |
|---|---|---|
| <a id="video-narration-capture"></a>[video-narration-capture](video-narration-capture/) | Skill that turns a narrated screen or gameplay recording into key frames, a timestamped transcript and a written breakdown (game teardown, software walkthrough or bug report) that agents can work from | Windows 11 (macOS and Linux untested) |

## Linking to a Tool

To share a tool, link to its row in the Tools tables above: `https://github.com/masfew/ai-toolkit#<tool>`, for example [`https://github.com/masfew/ai-toolkit#video-narration-capture`](https://github.com/masfew/ai-toolkit#video-narration-capture). To share a version you can download, link to the [releases page](https://github.com/masfew/ai-toolkit/releases).

Don't link to a tool's folder. The folders may move into category folders later, and a folder link would then break. The row link and the release names stay the same wherever the folder goes.

## Installing a Tool

You need [Claude Code](https://claude.com/claude-code), [Git](https://git-scm.com/) and [Python](https://www.python.org/downloads/) 3.9 or newer. Some tools need more, and their README says so.

1. Clone the repo. This works the same in PowerShell and bash:

   ```bash
   git clone https://github.com/masfew/ai-toolkit.git
   ```

2. Open the tool's folder and follow its README. Most tools install with one command, run from inside the tool's folder (PowerShell or bash):

   ```bash
   python install.py
   ```

   The install is safe to rerun. It backs up any file it changes, such as `~/.claude/settings.json`, and only adds its own entries.

3. To remove a tool, run the same script with `--remove` (PowerShell or bash):

   ```bash
   python install.py --remove
   ```

## Updating

Before you update, see what changed. Each tool's README ends with a Changes list, and each release on the [releases page](https://github.com/masfew/ai-toolkit/releases) has that version's notes. When the first number of a version goes up, its notes say what you need to do.

Pull the latest version, then rerun the install of each tool you use, from inside the tool's folder (PowerShell or bash):

```bash
git pull
python install.py
```

### Staying on a Version

To keep a tool on a version you know works, check out that release's tag, then rerun the install from inside the tool's folder (PowerShell or bash):

```bash
git fetch --tags
git checkout example-tool-v1.2.0
python install.py
```

A tag holds the whole repo as it was at that release, so every other tool in the clone goes back to that point too. To return to the latest versions, run `git checkout main` and `git pull`, then rerun the installs. (`git pull` on its own doesn't work while a tag is checked out.)

To keep different tools on different versions, download a tool's zip from its release instead of checking out the tag. Unzip it and run `python install.py` from the tool's folder inside it. The install copies the tool to `~/.claude/tools/<tool>`, so the unzipped folder can be deleted afterwards.

## Versions and Releases

Each tool has its own version number, since the tools change at different times. Versions follow [semantic versioning](https://semver.org/), `MAJOR.MINOR.PATCH`:

| Change | Example | When |
|---|---|---|
| Patch | 1.2.0 to 1.2.1 | Fixes. Nothing for you to do. |
| Minor | 1.2.1 to 1.3.0 | New features that work without any change on your side. |
| Major | 1.3.0 to 2.0.0 | Changes that need you to do something when you update, such as a new setting or a renamed command. The Changes entry says what. |

A tool's version is in the version line at the top of its README, and each version has an entry at the end, under Changes, written as `- 1.2.0: what changed`. If a tool is packaged as a Claude Code plugin, its `plugin.json` carries the same version.

Each release has a git tag named `<tool>-v<version>`, for example `example-tool-v1.2.0`, and a [GitHub release](https://github.com/masfew/ai-toolkit/releases) with that version's Changes entry as its notes and a zip of the tool's folder.

### Making a Release

1. Update the tool's version line and add its Changes entry, in the same commit as the change or after it, and push that to `main`.
2. Check the release from the root of the repo (PowerShell or bash). It prints the notes the release will have, or says what's missing:

   ```bash
   python .github/scripts/release_notes.py example-tool-v1.2.0
   ```

3. Tag that commit and push the tag (PowerShell or bash):

   ```bash
   git tag example-tool-v1.2.0
   git push origin example-tool-v1.2.0
   ```

The release workflow then checks the tag against the README again, and makes the GitHub release with the notes and the zip. If it fails, its log says why. Delete the tag with `git push origin --delete example-tool-v1.2.0` and `git tag -d example-tool-v1.2.0`, fix the README, and tag again.

## Repo Layout

| Path | What it holds |
|---|---|
| `<tool>/` | One folder per tool, with its own README, `install.py` and `config.example.json` where it needs one. The tool folders are at the root today and may move into category folders later, so link to a tool as [Linking to a Tool](#linking-to-a-tool) says |
| `_template/` | The starting point for a new tool |
| `.github/workflows/secret-scan.yml` | Scans every push for secrets with [gitleaks](https://github.com/gitleaks/gitleaks), and checks that no two tool folders share a name |
| `.github/workflows/release.yml` | Turns each pushed `<tool>-v<version>` tag into a GitHub release |
| `.github/scripts/release_notes.py` | Finds a release tag's tool folder at any depth, checks the tag against the tool's README, and writes the release's notes and zip. `--check-names` checks that tool folder names are unique |
| `scripts/pre_push_check.py` | Checks a push for secrets, personal identifiers, non-noreply commit emails and clashing tool folder names (see [Before You Push](#before-you-push)) |
| `scripts/public-repo-identifiers.example.txt` | A starting point for the identifier list the check reads |
| `.githooks/pre-push` | The git hook that runs the check before every push, once turned on |
| `.githooks/pre-commit` | The git hook that runs the check on the working tree before every commit, once turned on |

## Personal Settings

No tool stores your personal values in this repo. A tool that needs settings (a token, a topic, a path) reads them from a config file in `~/.claude/`, and ships a `config.example.json` with placeholder values to copy from.

## Contributing

Issues and pull requests are turned off, since this is a personal toolkit. You're welcome to fork it and change anything under the license.

### Before You Push

If you push from your own clone or fork, you can turn on the checks this repo uses. Before every `git commit`, the pre-commit hook checks the working tree and stops the commit if it finds a problem, so nothing has to be rewritten later. Before every `git push`, the pre-push hook stops the push if the commits being pushed hold a secret (found by [gitleaks](https://github.com/gitleaks/gitleaks)), one of your own identifiers, or an author or committer email that isn't a [GitHub noreply address](https://docs.github.com/en/account-and-profile/setting-up-and-managing-your-personal-account-on-github/managing-email-preferences/setting-your-commit-email-address). It's optional.

1. Install gitleaks, for example with `winget install Gitleaks.Gitleaks` on Windows or `brew install gitleaks` on macOS.
2. Copy `scripts/public-repo-identifiers.example.txt` to `~/.claude/public-repo-identifiers.txt` and replace its examples with your own: your username, real name, email addresses, private project names and anything else that mustn't be published. Keep it outside the repo.
3. Turn on both hooks once in your clone, from its root (PowerShell or bash):

   ```bash
   git config core.hooksPath .githooks
   ```

To run the same check by hand on the working tree, from the root of the repo (PowerShell or bash):

```bash
python scripts/pre_push_check.py
```

The check stops the commit or push, rather than letting it through, if gitleaks or the identifier list is missing. It only finds the identifiers on your list, so it doesn't replace reading your own diff before you push.

## License

[MIT](LICENSE)
