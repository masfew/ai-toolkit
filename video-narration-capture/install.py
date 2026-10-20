#!/usr/bin/env python3
"""
Sets up the video-narration-capture skill for Claude Code. Safe to rerun.

Run it from this folder, wherever it is (a clone of the repo, or an unzipped
release):

    python install.py             set up everything and run the self-test
    python install.py --no-test   set up only
    python install.py --remove    remove the skill

1. Installs the skill into ~/.claude/skills/video-narration-capture.
   - In a git clone it links there (a directory junction on Windows, a
     symlink elsewhere), so a `git pull` updates the skill straight away.
   - Anywhere else (an unzipped release) it copies the skill there, replacing
     an earlier copy, so this folder can be deleted afterwards.
   --copy or --link overrides the choice.
2. Installs ffmpeg if it is missing (winget on Windows, Homebrew on macOS).
3. Installs the Python packages faster-whisper and Pillow.
4. Self-test: builds a pack from a generated 30-second video and
   runs transcription on it, which downloads the Whisper 'small' model
   (about 500 MB) so the first real recording doesn't have to.

--remove deletes the installed skill (the link or the copy). It leaves ffmpeg,
the Python packages and the downloaded model in place, since other programs
may use them, and says how to remove each.

Ends with "Setup complete" (or "Removed"), or exits with status 1 and a
FAILED message saying what to do.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

SKILL_NAME = "video-narration-capture"
TOOL_DIR = Path(__file__).resolve().parent
SKILL_DIR = TOOL_DIR / "skill"
TARGET = Path.home() / ".claude" / "skills" / SKILL_NAME
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

def is_link(path):
    if path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction()):
        return True
    # Python < 3.12 can't detect junctions directly.
    return path.exists() and os.path.normcase(os.path.realpath(path)) != os.path.normcase(
        os.path.abspath(path))


def remove_link(path):
    # rmdir removes a junction or directory symlink without touching its target.
    os.rmdir(path) if os.name == "nt" else os.unlink(path)


def is_this_skill(path):
    skill_md = path / "SKILL.md"
    return skill_md.exists() and f"name: {SKILL_NAME}" in skill_md.read_text(encoding="utf-8")


def install_skill(mode):
    step(f"Installing the skill ({mode}) at {TARGET}")
    if not (SKILL_DIR / "SKILL.md").exists():
        fail(f"{SKILL_DIR / 'SKILL.md'} is missing. Is this the complete {SKILL_NAME} folder?")
    TARGET.parent.mkdir(parents=True, exist_ok=True)

    if is_link(TARGET):
        same = os.path.normcase(os.path.realpath(TARGET)) == os.path.normcase(str(SKILL_DIR))
        if mode == "link" and same:
            print(f"ok: already linked to {SKILL_DIR}")
            return
        remove_link(TARGET)
    elif TARGET.exists():
        if not is_this_skill(TARGET):
            fail(f"{TARGET} exists but is not this skill. Move it elsewhere, then rerun.")
        shutil.rmtree(TARGET)  # an earlier copy of this skill

    if mode == "copy":
        shutil.copytree(SKILL_DIR, TARGET,
                        ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        print(f"copied {SKILL_DIR} -> {TARGET}")
    elif os.name == "nt":
        r = run(["cmd", "/c", "mklink", "/J", TARGET, SKILL_DIR])
        if r.returncode != 0:
            fail(f"could not create the junction:\n{tail(r)}\n"
                 "Rerun with --copy to copy the skill instead.")
        print(f"linked {TARGET} -> {SKILL_DIR}")
    else:
        TARGET.symlink_to(SKILL_DIR, target_is_directory=True)
        print(f"linked {TARGET} -> {SKILL_DIR}")


def remove_skill():
    step(f"Removing the skill at {TARGET}")
    if is_link(TARGET):
        remove_link(TARGET)
        print(f"removed the link {TARGET} (the files it pointed to are untouched)")
    elif TARGET.exists():
        if not is_this_skill(TARGET):
            fail(f"{TARGET} is not this skill, so it was left alone.")
        shutil.rmtree(TARGET)
        print(f"removed {TARGET}")
    else:
        print("ok: the skill isn't installed")

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
                      help="copy the skill into ~/.claude/skills")
    mode.add_argument("--link", action="store_const", const="link", dest="mode",
                      help="link ~/.claude/skills to this folder")
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
    in_repo = (TOOL_DIR.parent / ".git").exists()
    mode = a.mode or ("link" if in_repo else "copy")
    install_skill(mode)
    ensure_ffmpeg()
    ensure_packages()
    if not a.no_test:
        self_test()

    print("\nSetup complete.")
    print(f"  Skill:  {TARGET}")
    print(f"  Python: {sys.executable}  (run process_video.py with this one)")
    print(f"  ffmpeg: {shutil.which('ffmpeg')}")
    if mode == "copy":
        print(f"  The skill was copied, so this folder ({TOOL_DIR}) can be deleted.")
    print("If Claude Code doesn't list the video-narration-capture skill yet, restart it.")


if __name__ == "__main__":
    main()
