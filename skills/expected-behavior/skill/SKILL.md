---
name: expected-behavior
description: "Writes the Expected Behavior list for a feature or fix that was just built: a short, paste-ready walkthrough of how it should work (\"When X, Y should happen\") that a human QA tester runs through, for a ticket, a card on a board or a test checklist. Works for games (UEFN/Fortnite, Roblox and others) and apps. Use it whenever the user says \"write up the expected behavior\", asks for QA test items, acceptance criteria, a test checklist or what QA or the testers should check after implementing something, even if they don't name the format. Not for automated tests, unit tests or test harnesses, bug reports, or release notes."
---

# Expected behavior

The list goes onto a ticket, a card on a board or a test plan, and is handed to QA testers.
The testers did not see this conversation and don't read code: they play the game or use the
app, and tick items.

The list's job is to give QA the context to run through how the feature is **supposed to
work**. It is a walkthrough of the feature, not test coverage: it does not try to list every
edge case or everything that could go wrong. Add an edge case only when it stands out as
extra important to check.

The examples here are games, so they say "player". For an app, read "player" as "user".

## 1. Gather what was built

Work from this conversation first: what the user asked for, what was decided, and what was
built or fixed. Read the changed code when you need to know what the player sees or does,
such as the names shown on screen or which button triggers what.

If nothing in the conversation says what was built, ask the user which feature, or look at the
recent changes and confirm with them. Don't invent behavior the work doesn't have.

**Never put an assumption on the list.** Every item has to be definitive and accurate. QA passes
or fails the feature against these items, so one wrong item can pass a feature that doesn't do
what was intended. If you are unsure whether an item is accurate (the intended behavior, a value,
what the player sees), or unsure whether it belongs on the list at all, ask the user before
writing the list, and leave it out until they answer. Never include it with a caveat, and never
note the assumption after the list instead of asking.

Automated test results are not a source of items. A test suite checks dozens of situations;
QA needs the handful that explain the feature. Don't turn test scenarios into list items, and
don't let a test's checks leak into the wording either: details like "with the tool in their
hands" or the exact moment an item is granted are things you verified, not things QA needs told.

Work out the engine from the project: `.verse` files or a `.uefnproject` mean UEFN; a Rojo
project (`default.project.json`), `.luau` files or a `wally.toml` mean Roblox.

## 2. Follow the player's path

Order the items the way a player meets the feature: what starts it, each change of state, and
how it ends. Then add an item for each action the player can take **at any point** that
changes the flow, such as leaving or cancelling.

For a fix, include the situation that used to go wrong, with the correct result.

A typical list is 4 to 7 items. A small change can have 2 or 3.

## 3. Write each item

- **Format:** `When <what the player does or what happens>, <what> should <happen>.` For actions
  that can happen during any phase, use `If at any point <the player does X>, <what> should
  <happen>.` Phrase it naturally; "then" is optional.
- **Name the player's real action:** "rings the Boss Bell again", not "calls the boss early";
  "walks over the loot", not "picks up the item".
- **Define a state once, then reuse the word.** When several results describe one state, name
  it the first time and spell out what it means ("the wave should pause, meaning enemies stop
  moving and no new enemies spawn"). Later items can just say "the wave should pause again" or
  "resume as normal".
- **One item per state or phase change.** An item can hold several results when together they
  are one state. Split only when two results belong to different moments.
- **Consider one item per rule when phases differ only slightly.** If every phase does the same
  small thing with a different value (a different tool per stage), one item that lists the values
  by phase usually reads better than near-identical items per phase ("a Grapple Hook for the
  first stage and a Jetpack for the second and third stages"). This is a judgment call, not a
  rule: when the phases differ in more than a value, or merging would make one item too big to
  verify in a single check, keep them as separate items.
- **Make clear what changes and what stays the same.** In a combined item, say which part is
  the same in every phase, so it can't be read as part of the sequence: "a Grapple Hook for the
  first stage and a Jetpack for the second and third, with the Checkpoint Flag alongside the tool
  in every stage", not "replaced with the Checkpoint Flag, a Grapple Hook for the first stage
  and…", which reads like three things given one after another.
- **Use the exact word for what happens to items or state.** "Replaced with" when everything is
  removed and then granted, "kept" only when it's left untouched, "weapons" when only weapon slots
  change, "inventory" only when all of it does. A tester who sees their materials still there after
  "inventory should be cleared" will fail a feature that works.
- **Lean on existing behavior.** If something should work the way it already did, say so
  ("the wave should end as expected") instead of describing it again. The same goes for how the
  player gets somewhere through an existing flow: "if a player leaves the Training Course early",
  not "leaves the stage area and confirms on the exit screen". Name the outcome, not the route to
  it: "completes the Training Course", not "the run ends and the player is sent back to the
  lobby".
- **Say each thing once.** When an earlier item has set something up (which menu opens, where a
  prompt appears, what a setting is called), later items use the short name and don't explain it
  again: "the Map should open", not "the Map should open, the same Map the Compass opens". The
  same goes for what an action does: once items have said what a button does, a later item says
  "the Show Net button should still work", not "should still raise and lower the net".
- **Say what happens, not why or what it means.** Leave out clauses that give the purpose of a
  result or explain what a visual change stands for: "the net around the range should rise up",
  not "should rise up, so balls don't fly off into the course"; "the button should turn off", not
  "the button should dim to show it's off".
- **Give each toggle item its own starting state.** When the feature is a button or setting that
  flips between two states, write one item per starting state and name that state in the item
  ("hits the Show Net button and the setting is enabled", "... and the setting is disabled"), not
  "hits the button again". Each item can then be checked on its own, in any order. This is the
  one case where both directions get an item: flipping is the feature.
- **Call a reset a reset.** When something goes back to its starting value, say it should
  "reset to its default" and give the default in brackets ("the Show Net button should reset
  to its default (on)"), not "turn back on", which reads like one more toggle.
- **Name the game action, not the controls.** "Aims", "jumps" and "interacts" are enough; QA knows
  which button that is on each platform, so leave out "(right-click, or left trigger on
  controller)". Name a platform only when the behavior differs there (a tap on mobile).
- **Stop at "should do nothing".** When the result is that nothing happens, don't follow it with
  the things that shouldn't happen ("should do nothing", not "should do nothing: it should not
  open the Map, and it should not end the race").
- **Leave out the reverse when it's implied.** If turning a setting off removes something, don't
  add "and turning it back on should restore it". Add the reverse only when it behaves
  differently from simply undoing the first action.
- **State a rule instead of one case of it.** When something holds for every value (a setting
  persisting whether it's on or off), write the rule ("whichever option is set for X should
  persist when a player leaves and rejoins"), not one scenario that tests a single value.
- **Use the names the player sees** (Boss Bell, Starter Kit), not code names. No variable,
  function, device or class names, no log lines, no millisecond timings.
- **Use numbers only when they are the point.** "Starts the next round with exactly 400 wood"
  belongs in a fix about wood. A countdown's length doesn't belong in a list about what
  happens when the countdown ends.
- **Some situations can't be made to happen on demand**, such as a rare failure or a timing
  fluke. If a test option or debug setting forces the situation, name it in the item. If
  nothing does, leave it out and mention it in the line after the list.

## 4. What to leave out

Unless the change is about them:

- every phase variation of the same action (one "if at any point" item covers leaving during
  the countdown, the fight and the results);
- regression checks on other features or areas that share code;
- multiplayer cases;
- scoring, sounds and UI details that didn't change;
- analytics events, including any on-screen analytics message in test sessions, even when the
  change added one.

If one of these stands out as extra important, for example the change could easily have broken
it, add one item for it.

Before sending, reread every item and cut any clause that repeats an earlier item, explains
something the feature's own name already makes obvious, or spells out the opposite of what was
just said. Cut whole items the same way: an item whose result already follows from earlier
items (entering again after a reset, when one item covers the reset and another covers
entering) adds nothing for QA. Then ask of each remaining clause whether a tester would notice it without
being told (which tool is in hand, which screen comes next, when exactly something arrives).
Usually those clauses can go. Keep one anyway when it's a simple detail that carries a lot of the
feature's weight, where it failing would mean the feature doesn't work, so it's worth stating
outright. Use judgment here; the user tweaks the list in review.

## 5. Engine and project specifics

**UEFN:** plain items; the QA team tests all of them by hand. Mention the player count only when
the item needs more than one ("with two players in the session"). Refer to things the way they
appear in Fortnite, not to devices or Verse.

**Roblox, other engines and apps:** plain items, like UEFN.

**Project rules come first.** A project's own instructions or another skill can add rules for
the list, such as a QA tag at the end of every item, a different sentence pattern, or where the
list goes. Follow them on top of this skill: they change those details, and everything else here
still applies.

## 6. Output

Reply with the list as a Markdown bullet list, one item per line, and nothing inside it but the
items: no heading, intro sentence or closing summary mixed in, so the user can paste it straight
into a ticket, a test plan or a card's checklist (in many board tools, pasting several lines into
a checklist's add-item box creates one item per line). Send the list alone. The only thing that may follow it
is the note about situations QA can't cause on demand (section 3); anything you were unsure about
was settled by asking first (section 1).

Only add the items to a card or ticket yourself when the user asks for that, following any
project rules for writing to the board.

## Examples

A change in a wave survival game: ringing the Boss Bell now pauses the wave during the Boss
Warning countdown and while the rewards screen is up, instead of enemies carrying on.

- When a player rings the Boss Bell, the wave should pause, meaning enemies should stop moving and no new enemies should spawn, and the Boss Warning countdown should start.
- When the Boss Warning countdown ends, the boss should appear and the wave should resume, with new enemies spawning depending on the difficulty the player has chosen.
- When the boss is defeated and the rewards screen is showing, the wave should pause again.
- When the rewards screen closes, the wave should resume as normal.
- If at any point the player leaves the match, the wave should end as expected.
- If at any point the player rings the Boss Bell again while the countdown is running, the countdown should end immediately and the boss should appear.

Notice what it does: it follows the player through the feature in order, defines "pause" once
and reuses it, covers leaving and calling the boss early with one "if at any point" item each,
names the real action (ringing the bell), leaves out timings and numbers that aren't the point,
and trusts QA to know how a wave already behaves.

Three more worked examples are in [references/examples.md](references/examples.md), each with what
the first draft got wrong and what the review changed: a tool given per stage and merged into one
rule, a prompt plus a persisted setting, and a Show Net toggle with a reset. Read it before writing
a list with any of those shapes.

## Files

| File | Read when |
|---|---|
| [references/examples.md](references/examples.md) | Writing a list that merges phases into one rule, has a prompt or setting that persists, or has a reset |

## Improving this skill

A new lesson goes into the matching bullet in section 3 or 4, changed in place instead of added
as a second rule. A worked example goes into `references/examples.md`, and only after a person
has reviewed a list and approved the rewrite. Rules for one project or team (QA tags, a house
sentence pattern, how to write to their board) belong in that project's instructions, which
section 5 tells sessions to follow, not here.
