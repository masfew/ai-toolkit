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

Pull the latest version, then rerun the install of each tool you use (PowerShell or bash):

```bash
git pull
python install.py
```

Check the tool's README for anything that changed before you update.

## Repo Layout

| Path | What it holds |
|---|---|
| `<tool>/` | One folder per tool, with its own README, `install.py` and `config.example.json` where it needs one |
| `_template/` | The starting point for a new tool |
| `.github/workflows/secret-scan.yml` | Scans every push for secrets with [gitleaks](https://github.com/gitleaks/gitleaks) |

## Personal Settings

No tool stores your personal values in this repo. A tool that needs settings (a token, a topic, a path) reads them from a config file in `~/.claude/`, and ships a `config.example.json` with placeholder values to copy from.

## Contributing

Issues and pull requests are turned off, since this is a personal toolkit. You're welcome to fork it and change anything under the license.

## License

[MIT](LICENSE)
