#!/usr/bin/env python3
"""
Sets up the video-narration-capture skill for Claude Code. Safe to rerun.

Run it from this folder, wherever it is (a clone of the repo, or an unzipped
release):

    python install.py             set up everything and run the self-test
    python install.py --no-test   set up only
    python install.py --remove    remove the skill

1. Installs the skill.
   - Puts this folder at one fixed place, ~/.claude/tools/video-narration-capture,
     so nothing else depends on where this folder is. In a git clone it links
     there (a directory junction on Windows, a symlink elsewhere), so a
     `git pull` updates the skill straight away. Anywhere else (an unzipped
     release) it copies this folder there, replacing an earlier copy, so this
     folder can be deleted afterwards. --copy or --link overrides the choice.
   - Links ~/.claude/skills/video-narration-capture to the skill folder in
     ~/.claude/tools/video-narration-capture, replacing a link or copy from an
     earlier install.
   - Saves what it installed to
     ~/.claude/tools/video-narration-capture.install.json.
2. Installs ffmpeg if it is missing (winget on Windows, Homebrew on macOS).
3. Installs the Python packages faster-whisper and Pillow.
4. Self-test: builds a pack from a generated 30-second video and
   runs transcription on it, which downloads the Whisper 'small' model
   (about 500 MB) so the first real recording doesn't have to.

--remove deletes what step 1 added (the skill link, ~/.claude/tools/
video-narration-capture and the record), even for an install made from
another folder. It leaves ffmpeg, the Python packages and the downloaded
model in place, since other programs may use them, and says how to remove
each.

Ends with "Setup complete" (or "Removed"), or exits with status 1 and a
FAILED message saying what to do.
"""
import argparse
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
from pathlib import Path

TOOL_NAME = "video-narration-capture"
SKILL_NAME = TOOL_NAME
TOOL_DIR = Path(__file__).resolve().parent
SKILL_DIR = TOOL_DIR / "skill"
CLAUDE_DIR = Path.home() / ".claude"
TOOLS_DIR = CLAUDE_DIR / "tools"
STABLE_DIR = TOOLS_DIR / TOOL_NAME
RECORD_FILE = TOOLS_DIR / f"{TOOL_NAME}.install.json"
RECORD_FORMAT = 1
TARGET_REL = f"skills/{SKILL_NAME}"
TARGET = CLAUDE_DIR / TARGET_REL
COPY_IGNORE = shutil.ignore_patterns(".git", "__pycache__", "*.pyc")
PACKAGES = ["faster-whisper", "pillow"]
MODEL_REPO = "Systran/faster-whisper-small"  # what faster-whisper downloads for --model small

# Text in an error that means a download couldn't reach the internet.
OFFLINE_SIGNS = ["connectionerror", "connection error", "connecterror", "max retries",
                 "httpsconnectionpool", "getaddrinfo", "failed to resolve", "name resolution",
                 "network is unreachable", "timed out", "localentrynotfounderror",
                 "locate the files on the hub"]


def step(msg):
    print(f"\n== {msg}", flush=True)


def fail(msg):
    print(f"\nFAILED: {msg}", file=sys.stderr)
    sys.exit(1)


def run(cmd, **kwargs):
    return subprocess.run([str(c) for c in cmd], capture_output=True, text=True, **kwargs)


def tail(result, n=1500):
    return (result.stderr or result.stdout or "").strip()[-n:]


def looks_offline(result):
    text = f"{result.stdout}\n{result.stderr}".lower()
    return any(sign in text for sign in OFFLINE_SIGNS)


# ---------- 1. skill ----------

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
        if os.name == "nt":
            # A junction needs no admin rights or Developer Mode.
            return run(["cmd", "/c", "mklink", "/J", dst, src]).returncode == 0
        dst.symlink_to(src, target_is_directory=True)
        return True
    except OSError:
        return False


def is_this_skill(path):
    skill_md = path / "SKILL.md"
    return skill_md.exists() and f"name: {SKILL_NAME}" in skill_md.read_text(encoding="utf-8")


def in_git_clone():
    return any((d / ".git").exists() for d in TOOL_DIR.parents)


def is_installed_copy(path):
    """True if path is a real folder holding a copy of this tool, made by an install."""
    script = path / "install.py"
    return (not is_link(path) and script.is_file()
            and f'TOOL_NAME = "{TOOL_NAME}"' in script.read_text(encoding="utf-8"))


def clear_stable_dir():
    if not exists(STABLE_DIR):
        return
    if is_link(STABLE_DIR):
        remove_link(STABLE_DIR)
    elif is_installed_copy(STABLE_DIR):
        shutil.rmtree(STABLE_DIR)
    else:
        fail(f"{STABLE_DIR} exists but isn't this tool. Move it elsewhere, then rerun.")


def install_stable_dir(mode):
    """Puts this folder at STABLE_DIR, as a link or a copy. Returns the mode it used."""
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
    else:
        print(f"left {STABLE_DIR} alone: it isn't this tool")


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


def write_record(mode, kind, dirs):
    record = {
        "format": RECORD_FORMAT,
        "tool": TOOL_NAME,
        "source": str(TOOL_DIR),
        "mode": mode,
        "files": [{"path": TARGET_REL, "from": "skill", "kind": kind, "dirs": dirs}],
        "hooks": [],
        "settings_created": False,
    }
    RECORD_FILE.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")


def install_skill(mode):
    """Installs the skill through STABLE_DIR. Returns the mode it used for STABLE_DIR."""
    step(f"Installing the skill ({mode}) at {TARGET}")
    if not (SKILL_DIR / "SKILL.md").exists():
        fail(f"{SKILL_DIR / 'SKILL.md'} is missing. Is this the complete {SKILL_NAME} folder?")
    if not is_link(STABLE_DIR) and same_path(os.path.realpath(STABLE_DIR), TOOL_DIR):
        fail(f"this is the installed copy at {STABLE_DIR}. Run install.py from the tool's own "
             "folder (a clone or an unzipped release) instead. To uninstall, run it here with --remove.")
    if exists(TARGET) and not is_link(TARGET) and not is_this_skill(TARGET):
        fail(f"{TARGET} exists but is not this skill. Move it elsewhere, then rerun.")
    old = read_record()
    if old and not same_path(old.get("source", ""), TOOL_DIR):
        print(f"moving the install from {old.get('source')} to {TOOL_DIR}")
    old_dirs = [d for f in (old or {}).get("files", []) for d in f.get("dirs", [])]

    mode = install_stable_dir(mode)
    src = STABLE_DIR / "skill"
    if links_to(TARGET, src):
        print(f"ok: {TARGET} already links to {src}")
        write_record(mode, "link", old_dirs)
        return mode
    dirs = sorted(set(make_parents(TARGET)) | set(old_dirs))
    if is_link(TARGET):
        remove_link(TARGET)  # an earlier install's link, such as 1.0.0's straight to a skill folder
    elif exists(TARGET):
        shutil.rmtree(TARGET)  # an earlier copy of this skill
    if make_link(TARGET, src):
        print(f"linked {TARGET} -> {src}")
        kind = "link"
    else:
        shutil.copytree(src, TARGET, ignore=COPY_IGNORE)
        print(f"copied {src} -> {TARGET}")
        print("  The system didn't allow a link, so rerun this script after each update.")
        kind = "copy"
    write_record(mode, kind, dirs)
    return mode


def remove_skill():
    step(f"Removing the skill at {TARGET}")
    os.chdir(Path.home())  # so this script's own folder can be deleted if it's the installed copy
    record = read_record()
    if is_link(TARGET):
        remove_link(TARGET)
        print(f"removed the link {TARGET} (the files it pointed to are untouched)")
    elif exists(TARGET):
        if is_this_skill(TARGET):
            shutil.rmtree(TARGET)
            print(f"removed {TARGET}")
        else:
            print(f"left {TARGET} alone: it is not this skill")
    else:
        print("ok: the skill isn't installed")
    remove_empty_dirs([d for f in (record or {}).get("files", []) for d in f.get("dirs", [])])
    remove_stable_dir()
    if RECORD_FILE.exists():
        RECORD_FILE.unlink()
    try:
        TOOLS_DIR.rmdir()  # only if no other tool is installed there
    except OSError:
        pass

    print("\nRemoved.")
    print("These stay, since other programs may use them. To remove them too:")
    print(f"  Python packages: {sys.executable} -m pip uninstall {' '.join(PACKAGES)}")
    print("    (faster-whisper also installed ctranslate2, onnxruntime, av and tokenizers,")
    print("    which pip leaves behind; uninstall them the same way if nothing else needs them)")
    cache = Path(os.environ.get("HF_HOME", Path.home() / ".cache" / "huggingface")) / "hub"
    model = cache / f"models--{MODEL_REPO.replace('/', '--')}"
    print(f"  Whisper model:   delete {model}")
    print("    (and any other models--Systran--faster-whisper-* folder there, if you used --model)")
    print("  ffmpeg:          winget uninstall Gyan.FFmpeg  (Windows)  or  brew uninstall ffmpeg  (macOS)")
    print("If Claude Code still lists the video-narration-capture skill, restart it.")


# ---------- 2. ffmpeg ----------

def find_ffmpeg():
    """True if ffmpeg and ffprobe are reachable. Right after a winget install,
    PATH only updates in new shells, so also look in winget's package folder
    and add it to this process's PATH."""
    if shutil.which("ffmpeg") and shutil.which("ffprobe"):
        return True
    if os.name == "nt":
        packages = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
        for exe in sorted(packages.glob("*FFmpeg*/*/bin/ffmpeg.exe"), reverse=True):
            os.environ["PATH"] = f"{exe.parent}{os.pathsep}{os.environ['PATH']}"
            return bool(shutil.which("ffprobe"))
    return False


MANUAL_FFMPEG = ("Install ffmpeg yourself, then rerun this script:\n"
                 "  Windows: winget install Gyan.FFmpeg  (or a build from https://ffmpeg.org/download.html)\n"
                 "  macOS:   install Homebrew (https://brew.sh), then brew install ffmpeg\n"
                 "  Linux:   sudo apt install ffmpeg  (or your distro's package manager)")


def ensure_ffmpeg():
    step("Checking ffmpeg")
    if find_ffmpeg():
        print(f"ok: {shutil.which('ffmpeg')}")
        return
    if os.name == "nt" and shutil.which("winget"):
        print("Installing ffmpeg with winget (Gyan.FFmpeg)...", flush=True)
        r = run(["winget", "install", "--id", "Gyan.FFmpeg", "-e", "--disable-interactivity",
                 "--accept-source-agreements", "--accept-package-agreements"])
        if r.returncode != 0 and not find_ffmpeg():
            fail(f"winget could not install ffmpeg:\n{tail(r)}\n\n{MANUAL_FFMPEG}")
    elif sys.platform == "darwin" and shutil.which("brew"):
        print("Installing ffmpeg with Homebrew...", flush=True)
        r = run(["brew", "install", "ffmpeg"])
        if r.returncode != 0:
            fail(f"brew could not install ffmpeg:\n{tail(r)}\n\n{MANUAL_FFMPEG}")
    else:
        fail(f"ffmpeg is not installed. {MANUAL_FFMPEG}")
    if not find_ffmpeg():
        fail("ffmpeg was installed but can't be found. Open a new terminal and rerun this script.")
    print(f"ok: {shutil.which('ffmpeg')}")


# ---------- 3. Python packages ----------

def ensure_packages():
    step(f"Installing Python packages ({', '.join(PACKAGES)}) for {sys.executable}")
    cmd = [sys.executable, "-m", "pip", "install", "--quiet",
           "--disable-pip-version-check", *PACKAGES]
    r = run(cmd)
    if r.returncode != 0 and "externally-managed-environment" in r.stderr:
        # Homebrew and many Linux Pythons block system-wide pip installs (PEP 668).
        # A per-user install leaves the system packages alone.
        print("This Python is managed by the OS or Homebrew; installing for this user instead.")
        r = run(cmd + ["--user", "--break-system-packages"])
    if r.returncode != 0:
        if "No module named pip" in r.stderr:
            fail(f"pip is missing. Run: {sys.executable} -m ensurepip --upgrade, then rerun.")
        if looks_offline(r):
            hint = ("pip couldn't reach the package index. Check your internet connection or "
                    "proxy, then rerun.")
        elif "no matching distribution" in r.stderr.lower():
            hint = ("pip found no package to install. Either it couldn't reach the package index "
                    "(check your internet connection or proxy), or there's no build yet for "
                    f"Python {sys.version.split()[0]}; then rerun this script with an older "
                    "Python, such as 3.12.")
        else:
            hint = (f"Install them yourself with: {sys.executable} -m pip install {' '.join(PACKAGES)}\n"
                    "If this Python won't allow it, make a virtual environment "
                    "(python -m venv), activate it, and rerun this script with its Python.")
        fail(f"pip install failed:\n{tail(r)}\n\n{hint}")
    print("ok")


# ---------- 4. self-test ----------

def self_test():
    step("Self-test (the first run downloads the Whisper 'small' model, about 500 MB)")
    script = TARGET / "scripts" / "process_video.py"
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        video, pack = tmp / "test.mp4", tmp / "pack"
        # Three distinct scenes (cuts at 8s and 20s) and a silent audio track.
        r = run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
                 "-f", "lavfi", "-i", "testsrc=d=8:s=1280x720:r=30",
                 "-f", "lavfi", "-i", "smptebars=d=12:s=1280x720:r=30",
                 "-f", "lavfi", "-i", "color=c=navy:d=10:s=1280x720:r=30",
                 "-f", "lavfi", "-t", "30", "-i", "anullsrc=r=16000:cl=mono",
                 "-filter_complex", "[0:v][1:v][2:v]concat=n=3:v=1:a=0[v]",
                 "-map", "[v]", "-map", "3:a", "-pix_fmt", "yuv420p", "-c:a", "aac", video])
        if r.returncode != 0:
            fail(f"ffmpeg could not generate the test video:\n{tail(r)}")

        print("Building a test pack, including transcription...", flush=True)
        r = run([sys.executable, script, "process", video, "--out", pack, "--language", "en"])
        if r.returncode != 0:
            if looks_offline(r):
                last = tail(r).splitlines()[-1]
                fail(f"the Whisper model download failed:\n{last}\n\n"
                     "The model comes from huggingface.co, so it needs internet access. "
                     "Check your connection or proxy, then rerun. To finish setup without it, "
                     "rerun with --no-test; the model then downloads on the first recording.")
            fail(f"process_video.py process failed:\n{tail(r)}")
        frames = sorted(p.name for p in (pack / "frames").glob("*.jpg"))
        expected = ["0001_00m00s.jpg", "0002_00m08s.jpg", "0003_00m18s.jpg", "0004_00m20s.jpg"]
        if frames != expected:
            fail(f"expected frames {expected}, got {frames}")
        if not list((pack / "sheets").glob("*.jpg")):
            fail("no contact sheet was made (is Pillow installed for this Python?)")
        if not (pack / "timeline.md").exists():
            fail("timeline.md was not written")
        print("ok: frames, contact sheet, timeline, transcription")

        r = run([sys.executable, script, "dense", video, "--out", pack,
                 "--start", "0:07", "--end", "0:09", "--fps", "5"])
        dense = list((pack / "frames_dense").glob("*/*.jpg"))
        if r.returncode != 0 or len(dense) != 10:
            fail(f"dense extraction failed (got {len(dense)} of 10 frames):\n{tail(r)}")
        print("ok: dense extraction")


def main():
    p = argparse.ArgumentParser(description="Set up or remove the video-narration-capture skill.")
    mode = p.add_mutually_exclusive_group()
    mode.add_argument("--copy", action="store_const", const="copy", dest="mode",
                      help="copy this folder into ~/.claude/tools instead of linking it")
    mode.add_argument("--link", action="store_const", const="link", dest="mode",
                      help="link ~/.claude/tools to this folder, even outside a git clone")
    mode.add_argument("--remove", action="store_const", const="remove", dest="mode",
                      help="remove the installed skill")
    p.add_argument("--no-test", action="store_true", help="skip the self-test")
    a = p.parse_args()

    if a.mode == "remove":
        remove_skill()
        return
    if sys.version_info < (3, 9):
        fail(f"Python 3.9 or newer is needed; this is {sys.version.split()[0]}. "
             "Get it from https://www.python.org/downloads/")
    mode = install_skill(a.mode or ("link" if in_git_clone() else "copy"))
    ensure_ffmpeg()
    ensure_packages()
    if not a.no_test:
        self_test()

    print("\nSetup complete.")
    print(f"  Skill:  {TARGET}")
    print(f"  Python: {sys.executable}  (run process_video.py with this one)")
    print(f"  ffmpeg: {shutil.which('ffmpeg')}")
    if mode == "copy":
        print(f"  The tool was copied to {STABLE_DIR}, so this folder ({TOOL_DIR}) can be deleted.")
    print("If Claude Code doesn't list the video-narration-capture skill yet, restart it.")


if __name__ == "__main__":
    main()
