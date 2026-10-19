#!/usr/bin/env python3
"""
Installs <tool-name> for Claude Code. Safe to rerun.

    python install.py            install, or update an earlier install
    python install.py --remove   undo everything the install added

Starting a new tool from this template: set TOOL_NAME, fill in FILES, HOOKS
and PROGRAMS (or leave them empty), and delete anything the tool doesn't need.

What it does:
1. Checks that Python and every program in PROGRAMS are installed, and says
   how to get any that are missing.
2. Installs each entry in FILES under ~/.claude. It links to this folder
   where the system allows it, so a `git pull` updates the tool straight
   away. Otherwise it copies, and the script has to be rerun after updates.
3. Adds each entry in HOOKS to ~/.claude/settings.json. It backs the file up
   first, never adds an entry twice, and leaves every other setting alone.
4. Creates the personal config file ~/.claude/<tool-name>.json from
   config.example.json if there isn't one yet.

--remove undoes 2 and 3. It leaves the personal config file in place, since
it holds the user's own values, and says where it is.

Ends with "Setup complete" (or "Removed"), or exits with status 1 and a
FAILED message saying what to do.
"""
import argparse
import filecmp
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

TOOL_NAME = "example-tool"  # TODO: the tool's folder name
MIN_PYTHON = (3, 9)

# Files and folders to install: path in this folder -> path under ~/.claude.
# Example: {"my-style.md": "output-styles/my-style.md", "skill": "skills/my-skill"}
FILES = {}

# Hooks to add to settings.json: (event, matcher or None, command).
# {tool} in the command becomes this folder's full path, so the hook works
# wherever the repo is cloned. Example: ("Stop", None, 'python "{tool}/hook.py"')
HOOKS = []

# Programs the tool needs: (command, how to install it).
# Example: ("gitleaks", "winget install Gitleaks.Gitleaks")
PROGRAMS = []

TOOL_DIR = Path(__file__).resolve().parent
CLAUDE_DIR = Path.home() / ".claude"
SETTINGS_FILE = CLAUDE_DIR / "settings.json"
CONFIG_EXAMPLE = TOOL_DIR / "config.example.json"
CONFIG_FILE = CLAUDE_DIR / f"{TOOL_NAME}.json"


def fail(msg):
    print(f"\nFAILED: {msg}", file=sys.stderr)
    sys.exit(1)


def backup(path):
    stamp = time.strftime('%Y%m%d-%H%M%S')
    copy, n = path.with_name(f"{path.name}.bak-{stamp}"), 1
    while copy.exists():  # never overwrite an earlier backup made in the same second
        n += 1
        copy = path.with_name(f"{path.name}.bak-{stamp}-{n}")
    if path.is_dir():
        shutil.copytree(path, copy)
    else:
        shutil.copy2(path, copy)
    print(f"backed up {path} -> {copy}")


# --- 1. Dependencies -------------------------------------------------------

def check_dependencies():
    if sys.version_info < MIN_PYTHON:
        need = ".".join(map(str, MIN_PYTHON))
        fail(f"Python {need} or newer is needed (this is {sys.version.split()[0]}). "
             "Get it from https://www.python.org/downloads/")
    missing = [(cmd, how) for cmd, how in PROGRAMS if not shutil.which(cmd)]
    if missing:
        fail("missing programs:\n" + "\n".join(f"  {cmd}: install with `{how}`, then open a new terminal"
                                                for cmd, how in missing))


# --- 2. Files --------------------------------------------------------------

def is_link(path):
    if path.is_symlink():
        return True
    # A Windows directory junction. Python before 3.12 can't detect one directly.
    return path.is_dir() and os.path.normcase(os.path.realpath(path)) != os.path.normcase(os.path.abspath(path))


def links_to(path, src):
    return is_link(path) and os.path.normcase(os.path.realpath(path)) == os.path.normcase(str(src))


def same_content(path, src):
    if src.is_dir():
        if not path.is_dir():
            return False
        cmp = filecmp.dircmp(src, path)
        return not (cmp.left_only or cmp.right_only or cmp.diff_files or cmp.funny_files) and all(
            same_content(path / d, src / d) for d in cmp.common_dirs)
    return path.is_file() and filecmp.cmp(path, src, shallow=False)


def remove_path(path):
    if is_link(path):
        # rmdir removes a junction or a directory symlink without touching what it points to.
        os.rmdir(path) if path.is_dir() and os.name == "nt" else path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)
    else:
        path.unlink()


def make_link(dst, src):
    try:
        if src.is_dir() and os.name == "nt":
            # A junction needs no admin rights or Developer Mode.
            r = subprocess.run(["cmd", "/c", "mklink", "/J", str(dst), str(src)], capture_output=True, text=True)
            return r.returncode == 0
        dst.symlink_to(src, target_is_directory=src.is_dir())
        return True
    except OSError:
        return False


def install_file(rel_src, rel_dst):
    src, dst = TOOL_DIR / rel_src, CLAUDE_DIR / rel_dst
    if not src.exists():
        fail(f"{src} is missing. Is this the complete {TOOL_NAME} folder?")
    if links_to(dst, src):
        print(f"ok: {dst} already links to {src}")
        return
    if not is_link(dst) and same_content(dst, src):
        print(f"ok: {dst} is an up-to-date copy")
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists() or dst.is_symlink():
        if not is_link(dst) and not same_content(dst, src):
            backup(dst)
        remove_path(dst)
    if make_link(dst, src):
        print(f"linked {dst} -> {src}")
    else:
        if src.is_dir():
            shutil.copytree(src, dst)
        else:
            shutil.copy2(src, dst)
        print(f"copied {src} -> {dst}")
        print("  The system didn't allow a link, so rerun this script after each update.")


def remove_file(rel_src, rel_dst):
    src, dst = TOOL_DIR / rel_src, CLAUDE_DIR / rel_dst
    if not (dst.exists() or dst.is_symlink()):
        return
    if links_to(dst, src) or (not is_link(dst) and same_content(dst, src)):
        remove_path(dst)
        print(f"removed {dst}")
    else:
        print(f"left {dst} alone: it isn't this tool's copy")


# --- 3. Settings -----------------------------------------------------------

def hook_command(command):
    return command.replace("{tool}", TOOL_DIR.as_posix())


def load_settings():
    if not SETTINGS_FILE.exists():
        return {}
    try:
        return json.loads(SETTINGS_FILE.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        fail(f"{SETTINGS_FILE} isn't valid JSON ({e}). Fix it, then rerun.")


def save_settings(settings):
    if SETTINGS_FILE.exists():
        backup(SETTINGS_FILE)
    SETTINGS_FILE.parent.mkdir(parents=True, exist_ok=True)
    SETTINGS_FILE.write_text(json.dumps(settings, indent=2) + "\n", encoding="utf-8")


def has_command(groups, command):
    return any(h.get("command") == command for g in groups for h in g.get("hooks", []))


def add_hooks():
    if not HOOKS:
        return
    settings = load_settings()
    changed = False
    for event, matcher, command in HOOKS:
        command = hook_command(command)
        groups = settings.setdefault("hooks", {}).setdefault(event, [])
        if has_command(groups, command):
            print(f"ok: the {event} hook is already in settings.json")
            continue
        group = {"matcher": matcher} if matcher else {}
        group["hooks"] = [{"type": "command", "command": command}]
        groups.append(group)
        changed = True
        print(f"added the {event} hook to settings.json")
    if changed:
        save_settings(settings)


def remove_hooks():
    if not HOOKS or not SETTINGS_FILE.exists():
        return
    settings = load_settings()
    all_hooks = settings.get("hooks", {})
    changed = False
    for event, _matcher, command in HOOKS:
        command = hook_command(command)
        groups = all_hooks.get(event, [])
        if not has_command(groups, command):
            continue
        for g in groups:
            g["hooks"] = [h for h in g.get("hooks", []) if h.get("command") != command]
        all_hooks[event] = [g for g in groups if g["hooks"]]
        if not all_hooks[event]:
            del all_hooks[event]
        changed = True
        print(f"removed the {event} hook from settings.json")
    if changed:
        if not all_hooks:
            settings.pop("hooks", None)
        save_settings(settings)


# --- 4. Personal config ----------------------------------------------------

def create_config():
    if not CONFIG_EXAMPLE.exists():
        return
    if CONFIG_FILE.exists():
        print(f"ok: keeping your config at {CONFIG_FILE}")
        return
    CONFIG_FILE.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(CONFIG_EXAMPLE, CONFIG_FILE)
    print(f"created {CONFIG_FILE} from config.example.json")
    print("  Replace its placeholder values with your own before using the tool.")


def main():
    p = argparse.ArgumentParser(description=f"Install or remove {TOOL_NAME}.")
    p.add_argument("--remove", action="store_true", help="undo everything the install added")
    args = p.parse_args()

    if args.remove:
        for rel_src, rel_dst in FILES.items():
            remove_file(rel_src, rel_dst)
        remove_hooks()
        print(f"\nRemoved {TOOL_NAME}.")
        if CONFIG_FILE.exists():
            print(f"Your config is still at {CONFIG_FILE}. Delete it if you don't need it.")
        return

    check_dependencies()
    if HOOKS:
        load_settings()  # stop before changing anything if settings.json is broken
    for rel_src, rel_dst in FILES.items():
        install_file(rel_src, rel_dst)
    add_hooks()
    create_config()
    print(f"\nSetup complete. Start a new Claude Code session to use {TOOL_NAME}.")


if __name__ == "__main__":
    main()
