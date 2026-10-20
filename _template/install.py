#!/usr/bin/env python3
"""
Installs <tool-name> for Claude Code. Safe to rerun.

    python install.py            install, or update an earlier install
    python install.py --copy     install a copy instead of a link
    python install.py --link     install a link even outside a git clone
    python install.py --remove   undo everything the install added

Starting a new tool from this template: set TOOL_NAME, fill in FILES, HOOKS
and PROGRAMS (or leave them empty), and delete anything the tool doesn't need.

What it does:
1. Checks that Python and every program in PROGRAMS are installed, and says
   how to get any that are missing.
2. Puts the tool at one fixed place, ~/.claude/tools/<tool-name>, so nothing
   else depends on where this folder is.
   - In a git clone it links there (a directory junction on Windows, a
     symlink elsewhere), so a `git pull` updates the tool straight away.
   - Anywhere else (an unzipped release) it copies this folder there, so this
     folder can be deleted afterwards.
   --copy or --link overrides the choice.
3. Installs each entry in FILES under ~/.claude, as a link into
   ~/.claude/tools/<tool-name> where the system allows it, or a copy if not.
4. Adds each entry in HOOKS to ~/.claude/settings.json, with paths through
   ~/.claude/tools/<tool-name>. It backs the file up first, never adds an
   entry twice, and leaves every other setting alone.
5. Creates the personal config file ~/.claude/<tool-name>.json from
   config.example.json if there isn't one yet.
6. Saves what it installed to ~/.claude/tools/<tool-name>.install.json.

On a rerun it reads that record first and takes out whatever the earlier
install added that this one doesn't, so a rerun from a moved or renamed
folder leaves nothing stale behind.

--remove undoes 2, 3, 4 and 6, using the record (or, for an install made
before there was one, the hook commands and files this folder would add). It
leaves the personal config file in place, since it holds the user's own
values, and says where it is.

Ends with "Setup complete" (or "Removed"), or exits with status 1 and a
FAILED message saying what to do.
"""
import argparse
import filecmp
import json
import os
import shutil
import stat
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
# {tool} in the command becomes ~/.claude/tools/<tool-name> as a full path, so
# the hook keeps working wherever this folder is, even after it moves.
# Example: ("Stop", None, 'python "{tool}/hook.py"')
HOOKS = []

# Programs the tool needs: (command, how to install it).
# Example: ("gitleaks", "winget install Gitleaks.Gitleaks")
PROGRAMS = []

TOOL_DIR = Path(__file__).resolve().parent
CLAUDE_DIR = Path.home() / ".claude"
TOOLS_DIR = CLAUDE_DIR / "tools"
STABLE_DIR = TOOLS_DIR / TOOL_NAME
RECORD_FILE = TOOLS_DIR / f"{TOOL_NAME}.install.json"
RECORD_FORMAT = 1
SETTINGS_FILE = CLAUDE_DIR / "settings.json"
CONFIG_EXAMPLE = TOOL_DIR / "config.example.json"
CONFIG_FILE = CLAUDE_DIR / f"{TOOL_NAME}.json"
COPY_IGNORE = shutil.ignore_patterns(".git", "__pycache__", "*.pyc")


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


# --- Links -----------------------------------------------------------------

def exists(path):
    # True for a broken link too, such as one left pointing at a folder that moved.
    return os.path.lexists(path)


def is_link(path):
    if path.is_symlink():
        return True
    # A Windows directory junction. Python before 3.12 can't detect one directly.
    try:
        return getattr(os.lstat(path), "st_reparse_tag", 0) == stat.IO_REPARSE_TAG_MOUNT_POINT
    except OSError:
        return False


def same_path(a, b):
    return os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(b))


def links_to(path, src):
    """True if path is a link pointing straight at src (not through other links)."""
    if not is_link(path):
        return False
    try:
        target = os.readlink(path)
    except OSError:
        return False
    for prefix in ("\\\\?\\", "\\??\\"):  # how Windows marks a junction's target
        if target.startswith(prefix):
            target = target[len(prefix):]
    return same_path(os.path.join(os.path.dirname(path), target), src)


def remove_link(path):
    # Removes the link only: what it points to is untouched. Never shutil.rmtree a link.
    attrs = getattr(os.lstat(path), "st_file_attributes", 0)
    if os.name == "nt" and attrs & stat.FILE_ATTRIBUTE_DIRECTORY:
        os.rmdir(path)
    else:
        os.unlink(path)


def remove_path(path):
    if is_link(path):
        remove_link(path)
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


def same_content(path, src):
    if src.is_dir():
        if not path.is_dir():
            return False
        cmp = filecmp.dircmp(src, path)
        return not (cmp.left_only or cmp.right_only or cmp.diff_files or cmp.funny_files) and all(
            same_content(path / d, src / d) for d in cmp.common_dirs)
    return path.is_file() and filecmp.cmp(path, src, shallow=False)


# --- 2. The tool's fixed place ---------------------------------------------

def in_git_clone():
    return any((d / ".git").exists() for d in TOOL_DIR.parents)


def is_installed_copy(path):
    """True if path is a real folder holding a copy of this tool, made by an install."""
    script = path / "install.py"
    return (not is_link(path) and script.is_file()
            and f'TOOL_NAME = "{TOOL_NAME}"' in script.read_text(encoding="utf-8"))


def is_empty_dir(path):
    """True if path is a real, empty folder, such as one left when --remove couldn't delete it."""
    return not is_link(path) and path.is_dir() and not any(path.iterdir())


def clear_stable_dir():
    if not exists(STABLE_DIR):
        return
    if is_link(STABLE_DIR):
        remove_link(STABLE_DIR)
    elif is_installed_copy(STABLE_DIR):
        shutil.rmtree(STABLE_DIR)
    elif is_empty_dir(STABLE_DIR):
        try:
            os.rmdir(STABLE_DIR)
        except OSError as e:
            fail(f"couldn't delete the empty folder {STABLE_DIR} ({e}). Close anything using it, "
                 "such as a terminal open in it, then rerun.")
    else:
        fail(f"{STABLE_DIR} exists but isn't this tool. Move it elsewhere, then rerun.")


def install_stable_dir(mode):
    """Puts the tool at STABLE_DIR, as a link or a copy. Returns the mode it used."""
    if mode == "link" and links_to(STABLE_DIR, TOOL_DIR):
        print(f"ok: {STABLE_DIR} already links to {TOOL_DIR}")
        return "link"
    TOOLS_DIR.mkdir(parents=True, exist_ok=True)
    if mode == "link":
        clear_stable_dir()
        if make_link(STABLE_DIR, TOOL_DIR):
            print(f"linked {STABLE_DIR} -> {TOOL_DIR}")
            return "link"
        print("The system didn't allow a link, so the tool is copied instead. "
              "Rerun this script after each update.")
    # Copy next to it first, so a failed copy leaves the earlier install working.
    new = STABLE_DIR.with_name(f"{STABLE_DIR.name}.new")
    if exists(new):
        remove_path(new)  # left by an interrupted install
    shutil.copytree(TOOL_DIR, new, ignore=COPY_IGNORE)
    clear_stable_dir()
    new.rename(STABLE_DIR)
    print(f"copied {TOOL_DIR} -> {STABLE_DIR}")
    return "copy"


def remove_stable_dir():
    if not exists(STABLE_DIR):
        return
    if is_link(STABLE_DIR):
        remove_link(STABLE_DIR)
        print(f"removed the link {STABLE_DIR} (the folder it pointed to is untouched)")
    elif is_installed_copy(STABLE_DIR):
        try:
            shutil.rmtree(STABLE_DIR)
            print(f"removed {STABLE_DIR}")
        except OSError as e:
            print(f"couldn't delete {STABLE_DIR} ({e}). Close anything using it, such as a "
                  "terminal open in it, then delete it yourself.")
    elif is_empty_dir(STABLE_DIR):
        try:
            os.rmdir(STABLE_DIR)
            print(f"removed the empty folder {STABLE_DIR}")
        except OSError as e:
            print(f"couldn't delete the empty folder {STABLE_DIR} ({e}). Close anything using it, "
                  "such as a terminal open in it, then delete it yourself.")
    else:
        print(f"left {STABLE_DIR} alone: it isn't this tool")


# --- 3. Files --------------------------------------------------------------

def make_parents(path):
    """Creates path's missing parent folders. Returns the ones it created, relative to ~/.claude."""
    made = [p for p in path.parents if p != CLAUDE_DIR and CLAUDE_DIR in p.parents and not exists(p)]
    path.parent.mkdir(parents=True, exist_ok=True)
    return [p.relative_to(CLAUDE_DIR).as_posix() for p in made]


def remove_empty_dirs(dirs):
    for rel in sorted(dirs, key=lambda d: d.count("/"), reverse=True):  # deepest first
        try:
            (CLAUDE_DIR / rel).rmdir()  # only if empty
        except OSError:
            pass


def install_file(rel_src, rel_dst):
    """Links (or copies) STABLE_DIR/rel_src to ~/.claude/rel_dst.
    Returns "link" or "copy", and the parent folders it had to create."""
    src, dst = STABLE_DIR / rel_src, CLAUDE_DIR / rel_dst
    if not (TOOL_DIR / rel_src).exists():
        fail(f"{TOOL_DIR / rel_src} is missing. Is this the complete {TOOL_NAME} folder?")
    if links_to(dst, src):
        print(f"ok: {dst} already links to {src}")
        return "link", []
    made = make_parents(dst)
    if exists(dst):
        if not is_link(dst) and not same_content(dst, src):
            backup(dst)
        remove_path(dst)
    if make_link(dst, src):
        print(f"linked {dst} -> {src}")
        return "link", made
    if src.is_dir():
        shutil.copytree(src, dst, ignore=COPY_IGNORE)
    else:
        shutil.copy2(src, dst)
    print(f"copied {src} -> {dst}")
    print("  The system didn't allow a link, so rerun this script after each update.")
    return "copy", made


def remove_file(rel_dst, rel_src, old_source=None, made_dirs=()):
    """Removes ~/.claude/rel_dst if it's this tool's link or copy of rel_src,
    then the parent folders the install created for it, if they're empty."""
    dst = CLAUDE_DIR / rel_dst
    if not exists(dst):
        remove_empty_dirs(made_dirs)
        return
    sources = [STABLE_DIR / rel_src, TOOL_DIR / rel_src]
    if old_source:
        sources.append(Path(old_source) / rel_src)
    if any(links_to(dst, s) for s in sources) or (
            not is_link(dst) and any(s.exists() and same_content(dst, s) for s in sources)):
        remove_path(dst)
        print(f"removed {dst}")
        remove_empty_dirs(made_dirs)
    else:
        print(f"left {dst} alone: it isn't this tool's link or copy")


# --- 4. Settings -----------------------------------------------------------

def hook_entries():
    """HOOKS as this install adds them, with {tool} pointing at STABLE_DIR."""
    return [{"event": event, "matcher": matcher, "command": command.replace("{tool}", STABLE_DIR.as_posix())}
            for event, matcher, command in HOOKS]


def legacy_hooks():
    """HOOKS as an install from before the install record added them, with {tool} pointing at this folder."""
    return [(event, command.replace("{tool}", TOOL_DIR.as_posix())) for event, _matcher, command in HOOKS]


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


def drop_hook(settings, event, command):
    """Takes every hook running exactly this command out of the event. Returns True if any was there."""
    all_hooks = settings.get("hooks", {})
    groups = all_hooks.get(event, [])
    if not has_command(groups, command):
        return False
    for g in groups:
        g["hooks"] = [h for h in g.get("hooks", []) if h.get("command") != command]
    all_hooks[event] = [g for g in groups if g["hooks"]]
    if not all_hooks[event]:
        del all_hooks[event]
    if not all_hooks:
        settings.pop("hooks", None)
    print(f"removed the {event} hook from settings.json: {command}")
    return True


def update_hooks(old_record):
    """Takes out the earlier install's hooks that this one doesn't add, then adds HOOKS.
    Returns whether this tool's installs created settings.json."""
    created = bool(old_record and old_record.get("settings_created"))
    new = hook_entries()
    keep = {(h["event"], h["command"]) for h in new}
    if old_record:
        stale = [(h["event"], h["command"]) for h in old_record.get("hooks", [])]
    else:
        stale = legacy_hooks()
    stale = [s for s in stale if s not in keep]
    if not new and not stale:
        return created
    settings = load_settings()
    changed = False
    for event, command in stale:
        changed |= drop_hook(settings, event, command)
    for h in new:
        groups = settings.setdefault("hooks", {}).setdefault(h["event"], [])
        if has_command(groups, h["command"]):
            print(f"ok: the {h['event']} hook is already in settings.json")
            continue
        group = {"matcher": h["matcher"]} if h["matcher"] else {}
        group["hooks"] = [{"type": "command", "command": h["command"]}]
        groups.append(group)
        changed = True
        print(f"added the {h['event']} hook to settings.json")
    if changed:
        created = created or not SETTINGS_FILE.exists()
        save_settings(settings)
    return created


def remove_hooks(commands, settings_created):
    if not commands or not SETTINGS_FILE.exists():
        return
    settings = load_settings()
    changed = False
    for event, command in commands:
        changed |= drop_hook(settings, event, command)
    if not changed:
        return
    if settings_created and not settings:
        # The install made settings.json and nothing else is in it now.
        SETTINGS_FILE.unlink()
        print(f"removed {SETTINGS_FILE}: the install created it, and it's empty now")
    else:
        save_settings(settings)


# --- 5. Personal config ----------------------------------------------------

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


# --- 6. Install record -----------------------------------------------------

def read_record():
    if not RECORD_FILE.exists():
        return None
    try:
        record = json.loads(RECORD_FILE.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        fail(f"{RECORD_FILE} isn't valid JSON ({e}). Delete it, then rerun.")
    if record.get("format", 0) > RECORD_FORMAT:
        fail(f"{RECORD_FILE} was written by a newer version of this installer. "
             "Run install.py from that version instead.")
    return record


def write_record(mode, files, settings_created):
    record = {
        "format": RECORD_FORMAT,
        "tool": TOOL_NAME,
        "source": str(TOOL_DIR),
        "mode": mode,
        "files": files,
        "hooks": hook_entries(),
        "settings_created": settings_created,
    }
    RECORD_FILE.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")


# --- Install and remove ----------------------------------------------------

def install(mode):
    if not is_link(STABLE_DIR) and same_path(os.path.realpath(STABLE_DIR), TOOL_DIR):
        fail(f"this is the installed copy at {STABLE_DIR}. Run install.py from the tool's own "
             "folder (a clone or an unzipped release) instead. To uninstall, run it here with --remove.")
    check_dependencies()
    old = read_record()
    if HOOKS or (old and old.get("hooks")):
        load_settings()  # stop before changing anything if settings.json is broken
    if old and not same_path(old.get("source", ""), TOOL_DIR):
        print(f"moving the install from {old.get('source')} to {TOOL_DIR}")

    mode = install_stable_dir(mode)
    old_files = {f["path"]: f for f in (old or {}).get("files", [])}
    files = []
    for rel_src, rel_dst in FILES.items():
        path = Path(rel_dst).as_posix()
        kind, made = install_file(rel_src, rel_dst)
        # Keep the folders an earlier install created, so --remove still takes them out.
        made = sorted(set(made) | set(old_files.get(path, {}).get("dirs", [])))
        files.append({"path": path, "from": Path(rel_src).as_posix(), "kind": kind, "dirs": made})
    installed = {f["path"] for f in files}
    for path, old_file in old_files.items():
        if path not in installed:
            remove_file(path, old_file["from"], old.get("source"), old_file.get("dirs", []))
    settings_created = update_hooks(old)
    create_config()
    write_record(mode, files, settings_created)
    print(f"\nSetup complete. Start a new Claude Code session to use {TOOL_NAME}.")
    if mode == "copy":
        print(f"The tool was copied to {STABLE_DIR}, so this folder can be deleted.")


def remove():
    os.chdir(Path.home())  # so this script's own folder can be deleted if it's the installed copy
    record = read_record()
    if record:
        for f in record.get("files", []):
            remove_file(f["path"], f["from"], record.get("source"), f.get("dirs", []))
        remove_hooks([(h["event"], h["command"]) for h in record.get("hooks", [])],
                     record.get("settings_created"))
    else:
        # An install from before the install record.
        for rel_src, rel_dst in FILES.items():
            remove_file(rel_dst, rel_src)
        remove_hooks(legacy_hooks() + [(h["event"], h["command"]) for h in hook_entries()], False)
    remove_stable_dir()
    if RECORD_FILE.exists():
        RECORD_FILE.unlink()
    try:
        TOOLS_DIR.rmdir()  # only if no other tool is installed there
    except OSError:
        pass
    print(f"\nRemoved {TOOL_NAME}.")
    if CONFIG_FILE.exists():
        print(f"Your config is still at {CONFIG_FILE}. Delete it if you don't need it.")


def main():
    p = argparse.ArgumentParser(description=f"Install or remove {TOOL_NAME}.")
    action = p.add_mutually_exclusive_group()
    action.add_argument("--copy", action="store_const", const="copy", dest="action",
                        help="copy the tool into ~/.claude/tools instead of linking it")
    action.add_argument("--link", action="store_const", const="link", dest="action",
                        help="link ~/.claude/tools to this folder, even outside a git clone")
    action.add_argument("--remove", action="store_const", const="remove", dest="action",
                        help="undo everything the install added")
    args = p.parse_args()

    if args.action == "remove":
        remove()
    else:
        install(args.action or ("link" if in_git_clone() else "copy"))


if __name__ == "__main__":
    main()
