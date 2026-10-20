---
name: video-narration-capture
description: Turn a narrated screen or gameplay recording (mp4, mov, mkv, webm) into a pack agents can work from - key frames, a timestamped transcript of the narration, a timeline that pairs each frame with what was said - and then a written breakdown, such as a game teardown (UI, level design, systems, loops), a software walkthrough (flows, screens, behavior), or a bug report (steps to reproduce, expected vs. actual, evidence, game and UEFN context). Use this whenever the user points at a video file and wants it analyzed, broken down, documented, transcribed, turned into screenshots, a spec or a bug report, or used as a reference or inspiration for something they are building - gameplay sessions, app or website demos, bug reproductions, product tours, tutorials - even if they only say "look at this recording" or "process this video". Also use it when asked to go back into an existing pack for more detail on a moment or an animation.
---

# Video narration capture

Claude cannot watch video, so a recording has to become still frames plus text
before it is useful. Someone records their screen and talks through what they
are doing; this skill converts that recording into a **pack** and then into a
written **breakdown** that agents can work from without ever seeing the
footage.

The narration matters as much as the frames. The narrator is telling you what
they think is important, what happens behind what is visible, and how things
feel. Treat it as the primary source and the frames as evidence for it.

## Workflow

Paths in this file (`scripts/`, `templates/`) are relative to this skill's
folder, the one holding this SKILL.md. Run the script from there, or give its
full path.

### 1. Check dependencies

- `ffmpeg` and `ffprobe` on PATH (Windows: `winget install Gyan.FFmpeg`, macOS: `brew install ffmpeg`)
- `pip install faster-whisper pillow`

These are normally set up by the tool's `install.py`, which installs the
Python packages into the Python that ran it. Use `python` (or `python3` on
macOS and Linux). If the script reports `faster-whisper is not installed`
although setup ran, you are probably using a different Python than the
installer did; try `python3`, `py`, or the Python the installer printed, and
use that one for the rest of the session.

The first transcription downloads a Whisper model (about 500 MB for `small`),
so tell the user if that is about to happen. Ask before installing anything.

### 2. Pick the breakdown

Decide from what the user said what the recording is and what the breakdown
is for, and pick a template from `templates/`:

| Recording | Template | Writes |
|---|---|---|
| Someone playing a game, as a reference for building one | `templates/game-teardown.md` | `teardown.md` |
| An app, website or tool: a demo, a flow, a competitor's product, a tutorial | `templates/software-walkthrough.md` | `walkthrough.md` |
| A recording made to show bugs, in a game (UEFN, Roblox, any engine) or any other software | `templates/bug-report.md` | `bug-report.md` |
| Anything else (a talk, a review, a meeting) | none | `notes.md`: summary, sections in order with timestamps, key points with evidence, open questions |

If the purpose is unclear (reference for building, documentation, or a bug
report), ask; it changes which sections matter.

### 3. Build the pack

```bash
python scripts/process_video.py process "<video>" --out "<pack folder>"
```

Put packs where the project keeps references, one folder per subject and
session, for example `references/<subject>/<session-name>/`. If no convention
exists, the default (next to the video) is fine.

Useful options:

| Option | When |
|---|---|
| `--scene 0.25` | Lower (0.15) for slow, subtle screen changes such as app UIs; higher (0.4) if fast gameplay floods the pack |
| `--interval 10` | Guaranteed frame every N seconds, even with no visual change |
| `--min-gap 2` | Raise to thin out bursts during fast camera motion or scrolling |
| `--model small` | `medium` if the transcript mangles names and jargon; `base` for speed |
| `--language en` | Skips language detection |
| `--device cuda` | Only with an NVIDIA GPU and cuDNN installed; the default CPU mode works everywhere |
| `--transcript-json f.json` | Use an existing transcript: a list of `{start, end, text}` in seconds |

The pack contains:

```
timeline.md      frames paired with the narration spoken while each was on screen
transcript.md    narration alone, timestamped
transcript.json  the same, with word timings
frames/          0001_00m00s.jpg ... full-size key frames
sheets/          contact sheets: labelled thumbnail grids, 9 frames each
manifest.json    everything above as data
frames_dense/    added later by the dense subcommand (step 5)
```

### 4. Read it in the right order

Full-size frames are expensive to read and a session can have hundreds, so do
not open them all. Work from cheap to expensive:

1. Read `timeline.md` end to end. This gives the structure of the session.
2. View the contact sheets to see what each stretch looks like.
3. Open full-size frames only where detail matters: screens, layouts, error
   messages, anything the narration singles out. Thumbnails are too small to
   read UI text or judge spacing.

For long sessions, split the timeline into sections (by area, flow or topic)
and write up each section before moving to the next, so findings are saved to
the breakdown rather than held in context. If subagents are available, give
each one a section.

### 5. Go back for motion

Key frames lose animation, transition timing, camera behavior, loading states
and anything else that happens between frames. When the narration flags
movement ("watch this", "see how it bounces", "notice the flicker") or the
breakdown needs timing, pull that range at a higher frame rate:

```bash
python scripts/process_video.py dense "<video>" --out "<pack folder>" --start 1:20 --end 1:24 --fps 8
```

Frames land in `frames_dense/<range>/` with the timestamp in each filename, so
durations can be measured by counting frames. Keep ranges short (a few seconds).

### 6. Write the breakdown

Read the template picked in step 2 and write its output file into the pack.
Fill only the sections the recording actually covers; a session about one
settings screen does not need a section on the whole app.

Three things make a breakdown trustworthy for the agents that work from it:

- **Cite evidence.** Reference frame numbers or timestamps (`#0042`, `03:15`)
  so a claim can be checked against the pack.
- **Separate what was observed from what is inferred.** "Price shown as 500,
  then 750, then 1,125" is observed. "Prices scale 1.5x per tier" is inferred.
  Server-side logic is never visible, so most rules are inferences; label them
  as such.
- **Describe, don't copy, when it's someone else's work.** For another game or
  product, capture how it behaves and why it works - layout, pacing, rules,
  feel - rather than reproducing its art, text or assets. The breakdown is a
  reference for building something original. For the user's own software,
  quoting exact UI text and error messages is fine and often needed.

When the narration and the frames disagree, or something is unclear, list it
under open questions rather than guessing. Ask the user about anything that
blocks the breakdown.

### 7. Report back

Tell the user where the pack is, how many frames and how much narration it
captured, which sections of the breakdown are filled, and what the recording
did not cover. If the transcript got names or jargon wrong, say so and suggest
`--model medium` or a quick correction pass.

## Troubleshooting

- **Too many frames**: raise `--scene` or `--min-gap`. Fast third-person
  gameplay and long scrolls change almost every frame.
- **Too few frames / missed a screen**: lower `--scene`, lower `--interval`, or
  use `dense` on the range. Small UI changes (a toggle, a toast) often fall
  under the scene threshold.
- **No transcript**: check the recording has an audio track with the microphone
  on it. Screen recorders often capture system audio only by default.
- **Background audio drowns the narration**: ask the user to lower game or
  system volume, or record the microphone on a separate track next time.
