# Claude Code Toolkit

A personal, unofficial collection of tools for [Claude Code](https://claude.com/claude-code): hooks, skills, output styles and helper scripts. It isn't made, endorsed or supported by Anthropic.

Each tool lives in its own folder, works on its own, and has a README that covers what it does, how to install it, how to configure it and how to remove it.

## Tools

| Tool | What it does | Tested on |
|---|---|---|
| _None yet_ | | |

## Installing a Tool

You need [Claude Code](https://claude.com/claude-code), [Git](https://git-scm.com/) and [Python](https://www.python.org/downloads/) 3.9 or newer. Some tools need more, and their README says so.

1. Clone the repo. This works the same in PowerShell and bash:

   ```bash
   git clone https://github.com/masfew/claude-code-toolkit.git
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

Before you update, see what changed. Each tool's README ends with a Changes list, and each release on the [releases page](https://github.com/masfew/claude-code-toolkit/releases) has that version's notes. When the first number of a version goes up, its notes say what you need to do.

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

To keep different tools on different versions, download a tool's zip from its release instead of checking out the tag. Unzip it somewhere it can stay, since the install links to that folder, and run `python install.py` from the tool's folder inside it.

## Versions and Releases

Each tool has its own version number, since the tools change at different times. Versions follow [semantic versioning](https://semver.org/), `MAJOR.MINOR.PATCH`:

| Change | Example | When |
|---|---|---|
| Patch | 1.2.0 to 1.2.1 | Fixes. Nothing for you to do. |
| Minor | 1.2.1 to 1.3.0 | New features that work without any change on your side. |
| Major | 1.3.0 to 2.0.0 | Changes that need you to do something when you update, such as a new setting or a renamed command. The Changes entry says what. |

A tool's version is in the version line at the top of its README, and each version has an entry at the end, under Changes, written as `- 1.2.0: what changed`. If a tool is packaged as a Claude Code plugin, its `plugin.json` carries the same version.

Each release has a git tag named `<tool>-v<version>`, for example `example-tool-v1.2.0`, and a [GitHub release](https://github.com/masfew/claude-code-toolkit/releases) with that version's Changes entry as its notes and a zip of the tool's folder.

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
| `<tool>/` | One folder per tool, with its own README, `install.py` and `config.example.json` where it needs one |
| `_template/` | The starting point for a new tool |
| `.github/workflows/secret-scan.yml` | Scans every push for secrets with [gitleaks](https://github.com/gitleaks/gitleaks) |
| `.github/workflows/release.yml` | Turns each pushed `<tool>-v<version>` tag into a GitHub release |
| `.github/scripts/release_notes.py` | Checks a release tag against the tool's README, and writes the release's notes and zip |

## Personal Settings

No tool stores your personal values in this repo. A tool that needs settings (a token, a topic, a path) reads them from a config file in `~/.claude/`, and ships a `config.example.json` with placeholder values to copy from.

## Contributing

Issues and pull requests are turned off, since this is a personal toolkit. You're welcome to fork it and change anything under the license.

## License

[MIT](LICENSE)
