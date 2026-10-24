# Bug report template

For a recording made to show one or more bugs, in a game (UEFN, Roblox or any
engine) or any other software. Copy this structure into `bug-report.md` in the
pack. If the project has its own bug card or issue format, map these fields
onto it instead.

Before writing, for every bug:

- Find the first frame where it is visible, and pull a dense range from a few
  seconds before to a few seconds after (`dense --start <t-3s> --end <t+3s>
  --fps 10`). Most bugs happen between key frames, and the moments before
  show the state that caused it.
- Build the steps to reproduce from what the narrator did before the bug, in
  order. Mark steps that happened before the recording started, or off
  screen, as **inferred**.
- Take the expected behavior from the narration ("this should have...").
  If the narrator never says it, write what you infer and mark it.
- Quote error messages and on-screen text exactly. Read them from full-size
  or dense frames, not contact sheets.

Fill only the fields the recording supports, and list what's missing under
"Still needed" rather than guessing. The games section applies only to games;
the UEFN lines only to UEFN.

```markdown
# Bug report: <product or island name>

Source: <video file>, <duration>, recorded <date if known>
Reported by: <narrator, if known>

| # | Bug | Severity | Seen | Frames |
|---|---|---|---|---|
| 1 | <short title> | <severity> | <times seen in the recording> | #0012, 01:04 |

## 1. <Short title: what goes wrong, and where>

- Severity: blocker (crash, can't progress, data or save loss, session won't
  start) / major (feature broken, exploit, wrong rewards) / minor (works with
  a workaround) / cosmetic
- Category: crash or freeze / logic / physics or collision / UI or HUD /
  visual / audio / networking or replication / performance / exploit /
  editor or tooling
- Frequency: every time / intermittent (N of M attempts in the recording) /
  seen once

### Steps to reproduce
Start state: <where it begins, and anything already set up>
1. <action> (observed, 00:42)
2. <action> (inferred: happened before the recording started)
3. ...

### Expected
<what should happen, and the source: narration at 01:02 / inferred>

### Actual
<what happens instead, step by step, with exact error text>

### Evidence
- First visible: #0012 (01:04); dense range `frames_dense/01m01s-01m07s/`
- What to look at in each frame: <the wrong value, the clipping, the frozen HUD>
- Timing, if relevant: <e.g. "player falls through the floor ~0.4s after landing">

### Environment
- Product and version or build
- Platform and OS; browser for web apps; input device (touch, gamepad,
  keyboard and mouse)

### Game context (games only)
- Engine or platform and version (UEFN, Roblox Studio, Unity, Unreal...)
- Session type: editor / local playtest / private or test server / live
- Players in the session, and which one saw the bug (host or server, other
  client); whether other players saw the same thing
- Game state just before the bug: round or phase, health, inventory or
  loadout, currency, position or area, recent actions
- Content involved: devices, scripts, entities, prefabs, assets
- UEFN: UEFN and Fortnite release (e.g. v32.10), edit session vs. launched
  session vs. published island (island code), device names and the settings
  that matter, Verse files or classes involved, any Verse runtime error

### Where the bug lives
- Ours: <our code, devices, content or configuration> / platform or engine:
  <report upstream, e.g. to Epic or Roblox>, with the reason (inferred)

### Suspected cause (inferred)
<best guess, and where to look first: a device setting, a Verse function,
an event that fires twice, a missing server check>

### Workaround
<anything the narrator did that avoided it, or "none seen">

### Verify the fix
<how to confirm it is fixed: the steps above plus the expected result, and
related cases to retest>

### Still needed
<what the recording can't show: logs (UEFN Output Log or Verse errors,
Roblox output, browser console, crash dump), other platforms, exact build,
whether it happens on a clean start>
```
