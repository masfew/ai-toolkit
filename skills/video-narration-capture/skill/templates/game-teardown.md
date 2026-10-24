# Game teardown template

For a recording of someone playing a game, as a reference for building one.
Copy this structure into `teardown.md` in the pack. Drop sections the recording
does not cover. Cite frames (`#0042`) or timestamps (`03:15`) throughout, and
mark each rule or number as **observed** or **inferred**.

```markdown
# <Game name> teardown

Source: <video file>, <duration>, recorded <date if known>
Focus of this session: <what the user set out to show>

## Summary
Three to five sentences: what the game is, what the session covered, and the
ideas most worth borrowing.

## Core loop
The repeating cycle in one line (do X -> earn Y -> spend on Z -> X gets better),
then each step:
- What the player does, and how long it takes
- What they get, and how it is shown to them
- What pulls them into the next step

## Session flow and pacing
What happens in the first 30 seconds, first 5 minutes, and beyond. When each
new mechanic, currency or area is introduced. Where the game slows the player
down (timers, gates, costs) and where it speeds them up.

## Systems
One subsection per system (inventory, pets, combat, trading, quests, rebirth...).

### <System name>
- Purpose: what it adds to the loop
- Rules: how it behaves, step by step (observed / inferred)
- Numbers: costs, rates, caps, timers (observed / inferred)
- Connections: which other systems it reads from or feeds into
- Evidence: frames and timestamps

## Economy and progression
Currencies and how each is earned and spent. Price and reward curves, with the
actual data points seen. Gates and unlock conditions. Monetization touchpoints
(passes, one-off purchases, subscriptions, ads) and where they are surfaced.

## UI
### Screen inventory
| Screen | Opened from | Purpose | Frames |
|---|---|---|---|

### <Screen name>
- Layout: regions and what sits in each, roughly in screen fractions
- Hierarchy: what draws the eye first, second, third
- Components: buttons, lists, cards, tabs, and their states
- Style: palette, corner radius, stroke, type scale, iconography
- Behavior: open/close transitions, hover and press feedback, scrolling
- Mobile vs. desktop differences, if visible

### HUD
What is always on screen, where, and how it updates.

## Level design
- Layout: sketch of the space as a list of areas and how they connect
- Scale: sizes in the engine's units, using the player character as a ruler
  (for example, a Roblox character is about 5 studs tall)
- Flow: the path a new player takes, sightlines, landmarks used for orientation
- Density: where interactables, rewards and hazards are placed, and how far apart
- Gating: how areas are locked and revealed

## Feel
Feedback on actions: animation, particles, sound, camera shake, number popups.
Timing measured from dense frames where available (for example "button scales
to 1.1x over ~0.15s"). What makes the important moments land.

## Worth borrowing
Specific ideas to adapt, each with why it works and how it might change in our
game.

## Do differently
Weak spots, friction, or things that do not fit our game.

## Open questions
What the recording did not show, where narration and frames disagree, and
inferences that need checking.
```
