#!/usr/bin/env python3
"""Turn a narrated screen or gameplay recording into a pack an agent can read.

Subcommands
  process  Extract key frames, transcribe the narration, and align the two.
  dense    Re-extract a short time range at a high frame rate (for motion).

Requires: ffmpeg + ffprobe on PATH.
Optional: faster-whisper (transcription), Pillow (contact sheets).
"""
import argparse
import bisect
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path


# ---------- helpers ----------

def die(msg):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def require_ffmpeg():
    # Right after `winget install`, PATH only updates in new shells, so also
    # look in winget's package folder.
    if not shutil.which("ffmpeg") and os.name == "nt":
        packages = Path(os.environ.get("LOCALAPPDATA", "")) / "Microsoft" / "WinGet" / "Packages"
        for exe in sorted(packages.glob("*FFmpeg*/*/bin/ffmpeg.exe"), reverse=True):
            os.environ["PATH"] = f"{exe.parent}{os.pathsep}{os.environ['PATH']}"
            break
    for tool in ("ffmpeg", "ffprobe"):
        if not shutil.which(tool):
            die(f"{tool} not found on PATH. Install ffmpeg "
                "(Windows: winget install Gyan.FFmpeg | macOS: brew install ffmpeg).")


def probe(video):
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-print_format", "json",
         "-show_format", "-show_streams", str(video)],
        capture_output=True, text=True)
    if out.returncode != 0:
        die(f"ffprobe could not read {video}: {out.stderr.strip()}")
    info = json.loads(out.stdout)
    kinds = {s.get("codec_type") for s in info.get("streams", [])}
    if "video" not in kinds:
        die(f"{video} has no video stream.")
    return float(info["format"].get("duration", 0)), "audio" in kinds


def clock(sec):
    """12.3 -> '00:12', 3723 -> '1:02:03'"""
    sec = int(sec)
    h, m, s = sec // 3600, (sec % 3600) // 60, sec % 60
    return f"{h}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"


def file_clock(sec):
    """Filename-safe timestamp: '00m12s' or '1h02m03s'"""
    sec = int(sec)
    h, m, s = sec // 3600, (sec % 3600) // 60, sec % 60
    return f"{h}h{m:02d}m{s:02d}s" if h else f"{m:02d}m{s:02d}s"


def parse_time(value):
    """Accept seconds ('95', '95.5') or clock ('1:35', '0:01:35')."""
    parts = [float(p) for p in str(value).split(":")]
    total = 0.0
    for p in parts:
        total = total * 60 + p
    return total


def ffmpeg_frames(args_before_mode, args_after_mode):
    """Run ffmpeg with variable-frame-rate output. Newer builds want
    -fps_mode, older ones only know -vsync, so try both."""
    last = None
    for mode in (["-fps_mode", "vfr"], ["-vsync", "vfr"]):
        last = subprocess.run(args_before_mode + mode + args_after_mode,
                              capture_output=True, text=True)
        if last.returncode == 0:
            return last
        if "Unrecognized option" not in last.stderr:
            break
    die("ffmpeg failed:\n" + last.stderr[-2000:])


# ---------- frames ----------

def extract_frames(video, frames_dir, scene, interval, min_gap, width):
    """Keep a frame when the picture changes noticeably (scene score), and
    also at least once every `interval` seconds so long static stretches of
    narration still get a visual. `min_gap` stops fast camera motion from
    producing a burst of near-identical frames."""
    frames_dir.mkdir(parents=True, exist_ok=True)
    for old in frames_dir.glob("*.jpg"):
        old.unlink()

    select = (
        "select='isnan(prev_selected_t)"
        f"+gte(t-prev_selected_t,{interval})"
        f"+gt(scene,{scene})*gte(t-prev_selected_t,{min_gap})'"
    )
    vf = f"{select},showinfo,scale='min({width},iw)':-2"
    result = ffmpeg_frames(
        ["ffmpeg", "-hide_banner", "-nostdin", "-nostats", "-y",
         "-i", str(video), "-an", "-vf", vf],
        ["-q:v", "3", str(frames_dir / "tmp_%05d.jpg")])

    # showinfo logs one line per kept frame, in output order.
    times = [float(t) for t in re.findall(r"pts_time:\s*([0-9.]+)", result.stderr)]
    tmp_files = sorted(frames_dir.glob("tmp_*.jpg"))
    if len(times) != len(tmp_files):
        die(f"frame/timestamp mismatch ({len(tmp_files)} files, {len(times)} timestamps)")

    frames = []
    for i, (path, t) in enumerate(zip(tmp_files, times), start=1):
        final = frames_dir / f"{i:04d}_{file_clock(t)}.jpg"
        path.rename(final)
        frames.append({"index": i, "time": round(t, 2), "clock": clock(t),
                       "file": f"frames/{final.name}"})
    return frames


def contact_sheets(frames, pack, cols=3, rows=3, tile_w=520):
    """Low-cost overview images: a grid of labelled thumbnails so the agent
    can skim the whole session before opening full-size frames."""
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        print("note: Pillow not installed, skipping contact sheets "
              "(pip install pillow)", file=sys.stderr)
        return []
    try:
        font = ImageFont.load_default(size=20)
    except TypeError:  # older Pillow
        font = ImageFont.load_default()

    sheets_dir = pack / "sheets"
    sheets_dir.mkdir(exist_ok=True)
    for old in sheets_dir.glob("*.jpg"):
        old.unlink()

    label_h, per = 28, cols * rows
    sheets = []
    for n, start in enumerate(range(0, len(frames), per), start=1):
        chunk = frames[start:start + per]
        thumbs = []
        for f in chunk:
            im = Image.open(pack / f["file"]).convert("RGB")
            im.thumbnail((tile_w, tile_w))
            thumbs.append(im)
        tile_h = max(t.height for t in thumbs) + label_h
        used_rows = -(-len(chunk) // cols)
        sheet = Image.new("RGB", (cols * tile_w, used_rows * tile_h), (18, 18, 18))
        draw = ImageDraw.Draw(sheet)
        for k, (f, im) in enumerate(zip(chunk, thumbs)):
            x, y = (k % cols) * tile_w, (k // cols) * tile_h
            draw.text((x + 6, y + 3), f"#{f['index']:04d}  {f['clock']}",
                      fill=(255, 255, 255), font=font)
            sheet.paste(im, (x, y + label_h))
        name = f"sheet_{n:02d}_frames_{chunk[0]['index']:04d}-{chunk[-1]['index']:04d}.jpg"
        sheet.save(sheets_dir / name, quality=85)
        sheets.append({"file": f"sheets/{name}",
                       "first_frame": chunk[0]["index"], "last_frame": chunk[-1]["index"],
                       "from": chunk[0]["clock"], "to": chunk[-1]["clock"]})
    return sheets


# ---------- transcript ----------

def transcribe(video, pack, model_name, language, device):
    try:
        from faster_whisper import WhisperModel
    except ImportError:
        die("faster-whisper is not installed. Run: pip install faster-whisper\n"
            "(or pass --skip-transcript / --transcript-json)")

    import numpy as np

    # Decode with ffmpeg and pass Whisper a float32 array, rather than a file
    # path. faster-whisper's own decoder uses PyAV, and some faster-whisper /
    # PyAV version pairs are incompatible (av 17+ dropped `metadata_errors`).
    r = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostdin", "-nostats", "-i", str(video),
         "-vn", "-ac", "1", "-ar", "16000", "-f", "f32le", "-"],
        capture_output=True)
    if r.returncode != 0:
        die("audio extraction failed:\n" + r.stderr.decode(errors="replace")[-1500:])
    audio = np.frombuffer(r.stdout, dtype=np.float32)

    compute = "int8" if device == "cpu" else "float16"
    model = WhisperModel(model_name, device=device, compute_type=compute)
    seg_iter, _ = model.transcribe(audio, language=language,
                                   word_timestamps=True, vad_filter=True)
    segments = []
    for s in seg_iter:
        segments.append({
            "start": round(s.start, 2), "end": round(s.end, 2),
            "text": s.text.strip(),
            "words": [{"start": round(w.start, 2), "end": round(w.end, 2),
                       "word": w.word} for w in (s.words or [])],
        })
    return segments


def align(frames, segments):
    """Attach narration to the frame that was on screen when it was spoken.
    Uses word timing when available, so a long sentence that spans several
    frames is split across them rather than dumped on the first."""
    frame_times = [f["time"] for f in frames]
    buckets = [[] for _ in frames]
    for seg in segments:
        units = seg.get("words") or [{"start": seg["start"], "word": " " + seg["text"]}]
        for u in units:
            i = max(bisect.bisect_right(frame_times, u["start"] + 0.05) - 1, 0)
            buckets[i].append(u["word"])
    for f, words in zip(frames, buckets):
        f["narration"] = re.sub(r"\s+", " ", "".join(words)).strip()


# ---------- output ----------

def write_pack(pack, video, duration, frames, sheets, segments, settings):
    if segments:
        lines = [f"# Transcript: {video.name}", ""]
        lines += [f"[{clock(s['start'])}] {s['text']}" for s in segments]
        (pack / "transcript.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
        (pack / "transcript.json").write_text(
            json.dumps(segments, indent=1), encoding="utf-8")

    tl = [f"# Timeline: {video.name}", "",
          f"Duration {clock(duration)} | {len(frames)} frames | "
          f"{'narrated' if segments else 'no transcript'}", ""]
    if sheets:
        tl += ["## Contact sheets (skim these first)", ""]
        tl += [f"- `{s['file']}` covers {s['from']} to {s['to']}" for s in sheets]
        tl.append("")
    tl += ["## Frames with narration", ""]
    for i, f in enumerate(frames):
        until = frames[i + 1]["clock"] if i + 1 < len(frames) else clock(duration)
        tl.append(f"### #{f['index']:04d}  {f['clock']} to {until}  `{f['file']}`")
        tl.append(f.get("narration") or "_(no narration)_")
        tl.append("")
    (pack / "timeline.md").write_text("\n".join(tl), encoding="utf-8")

    (pack / "manifest.json").write_text(json.dumps({
        "video": video.name, "duration_seconds": round(duration, 2),
        "settings": settings, "frames": frames, "sheets": sheets,
        "has_transcript": bool(segments),
    }, indent=1), encoding="utf-8")


# ---------- commands ----------

def cmd_process(a):
    require_ffmpeg()
    video = Path(a.video).expanduser().resolve()
    if not video.exists():
        die(f"video not found: {video}")
    pack = Path(a.out).expanduser().resolve() if a.out else video.parent / f"{video.stem}_pack"
    pack.mkdir(parents=True, exist_ok=True)
    duration, has_audio = probe(video)

    print(f"Extracting frames from {video.name} ({clock(duration)})...")
    frames = extract_frames(video, pack / "frames", a.scene, a.interval, a.min_gap, a.width)

    segments = []
    if a.transcript_json:
        segments = json.loads(Path(a.transcript_json).read_text(encoding="utf-8"))
    elif a.skip_transcript:
        pass
    elif not has_audio:
        print("note: no audio track found, skipping transcription", file=sys.stderr)
    else:
        print(f"Transcribing narration (model: {a.model}, device: {a.device})...")
        segments = transcribe(video, pack, a.model, a.language, a.device)
    align(frames, segments)

    sheets = [] if a.no_sheets else contact_sheets(frames, pack)
    settings = {"scene": a.scene, "interval": a.interval, "min_gap": a.min_gap,
                "width": a.width, "model": a.model}
    write_pack(pack, video, duration, frames, sheets, segments, settings)

    print(f"\nReference pack: {pack}")
    print(f"  frames:     {len(frames)}  (frames/)")
    print(f"  sheets:     {len(sheets)}  (sheets/)")
    print(f"  transcript: {len(segments)} segments")
    print("  start with: timeline.md")
    if len(frames) > a.warn_frames:
        print(f"\nnote: {len(frames)} frames is a lot to read. To thin them out, "
              f"re-run with a higher --scene (now {a.scene}) or --min-gap (now {a.min_gap}).")


def cmd_dense(a):
    require_ffmpeg()
    video = Path(a.video).expanduser().resolve()
    if not video.exists():
        die(f"video not found: {video}")
    start, end = parse_time(a.start), parse_time(a.end)
    if end <= start:
        die("--end must be after --start")
    pack = Path(a.out).expanduser().resolve()
    out_dir = pack / "frames_dense" / f"{file_clock(start)}-{file_clock(end)}"
    out_dir.mkdir(parents=True, exist_ok=True)
    for old in out_dir.glob("*.jpg"):
        old.unlink()

    r = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostdin", "-nostats", "-y",
         "-ss", str(start), "-to", str(end), "-i", str(video), "-an",
         "-vf", f"fps={a.fps},scale='min({a.width},iw)':-2",
         "-q:v", "3", str(out_dir / "tmp_%04d.jpg")],
        capture_output=True, text=True)
    if r.returncode != 0:
        die("ffmpeg failed:\n" + r.stderr[-2000:])

    files = sorted(out_dir.glob("tmp_*.jpg"))
    for i, path in enumerate(files):
        t = start + i / a.fps
        path.rename(out_dir / f"{i + 1:04d}_{t:08.2f}s.jpg")
    print(f"{len(files)} frames at {a.fps} fps -> {out_dir}")


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    pr = sub.add_parser("process", help="frames + transcript + timeline")
    pr.add_argument("video")
    pr.add_argument("--out", help="pack folder (default: <video name>_pack next to the video)")
    pr.add_argument("--scene", type=float, default=0.25,
                    help="scene-change sensitivity 0-1; lower keeps more frames (default 0.25)")
    pr.add_argument("--interval", type=float, default=10,
                    help="always keep a frame at least this often, in seconds (default 10)")
    pr.add_argument("--min-gap", type=float, default=2,
                    help="minimum seconds between kept frames (default 2)")
    pr.add_argument("--width", type=int, default=1280, help="max frame width in px (default 1280)")
    pr.add_argument("--model", default="small", help="Whisper model size (default small)")
    pr.add_argument("--language", default=None, help="narration language code, e.g. en (default: detect)")
    pr.add_argument("--device", default="cpu", choices=["cpu", "cuda"],
                    help="cpu works everywhere; cuda needs an NVIDIA GPU with cuDNN")
    pr.add_argument("--skip-transcript", action="store_true")
    pr.add_argument("--transcript-json", help="use an existing transcript instead of Whisper: "
                    "a JSON list of {start, end, text} in seconds")
    pr.add_argument("--no-sheets", action="store_true", help="skip contact sheets")
    pr.add_argument("--warn-frames", type=int, default=250, help=argparse.SUPPRESS)
    pr.set_defaults(func=cmd_process)

    de = sub.add_parser("dense", help="high-frame-rate extraction of a short range")
    de.add_argument("video")
    de.add_argument("--out", required=True, help="existing pack folder")
    de.add_argument("--start", required=True, help="seconds or mm:ss")
    de.add_argument("--end", required=True, help="seconds or mm:ss")
    de.add_argument("--fps", type=float, default=5)
    de.add_argument("--width", type=int, default=960)
    de.set_defaults(func=cmd_dense)

    a = p.parse_args()
    a.func(a)


if __name__ == "__main__":
    main()
