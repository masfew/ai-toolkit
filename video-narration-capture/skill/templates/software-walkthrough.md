# Software walkthrough template

For a recording of an app, website or tool: a demo of a flow, a tour of
someone else's product, or a tutorial. A recording made mainly to show bugs
uses `bug-report.md` instead. Copy this
structure into `walkthrough.md` in the pack. Drop sections the recording does
not cover. Cite frames (`#0042`) or timestamps (`03:15`) throughout, and mark
each rule or number as **observed** or **inferred**.

Write it so someone who never sees the video could rebuild the flow or write
tests for it.

```markdown
# <Product name>: <what was walked through>

Source: <video file>, <duration>, recorded <date if known>
Product: <name, version or build if visible>, <platform: web / desktop / iOS / Android>, <browser or OS if visible>
Purpose: <reference for building / documenting existing behavior / tutorial>

## Summary
Three to five sentences: what the product is, what the recording shows, and
the main findings (ideas worth borrowing, behavior to document, or bugs).

## Flows
One subsection per task the narrator completes (sign up, create a project,
check out...).

### <Flow name>
Goal: what the user is trying to get done.
Start state: where it begins, and anything already set up (logged in, sample data).

| # | Time | Action | Result | Frames |
|---|---|---|---|---|
| 1 | 00:12 | Clicks "New project" in the sidebar | Modal opens with name and template fields | #0004 |

End state: what has changed once the flow is done.
Friction: extra steps, confusing moments, waits, things the narrator had to hunt for.

## Screens
### Screen inventory
| Screen | Reached from | Purpose | Frames |
|---|---|---|---|

### <Screen name>
- Layout: regions (header, sidebar, main, panels) and what sits in each
- Hierarchy: what draws the eye first, second, third
- Content: data shown, and where it seems to come from
- Actions: every button, link, menu item and shortcut, and what each does
- Components and states: default, hover, selected, disabled, loading, empty, error
- Text: labels, help text and messages that matter for behavior (quote exactly
  for the user's own product; paraphrase for someone else's)
- Responsive or platform differences, if visible

## Behavior and rules
What the product does that is not obvious from one screen, one bullet each,
marked observed or inferred:
- Validation: required fields, formats, limits, when errors appear
- Permissions and roles: who can see or do what
- Data: what is saved, when (autosave or explicit), and where it shows up again
- Side effects: emails, notifications, background jobs, changes elsewhere
- Limits and edge cases: maximums, empty states, what happens on undo or back

## Feel
Transitions, animations, loading indicators and feedback on actions. Timing
measured from dense frames where available (for example "toast slides in over
~0.2s, stays ~4s"). Perceived speed: where it waits and for how long.

## Issues
Bugs seen along the way, one subsection each, using the per-bug format from
`templates/bug-report.md` (read it when there are any). Skip this section if
there were none.

## Worth borrowing
For someone else's product: specific ideas to adapt, why each works, and how
it might change in ours.

## Do differently
Weak spots, friction, or things that do not fit our product.

## Open questions
What the recording did not show (error paths, other roles, settings), where
narration and frames disagree, and inferences that need checking.
```
