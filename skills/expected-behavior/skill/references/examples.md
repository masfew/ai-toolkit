# More worked examples

Each example below shows a list after review, and what the first draft got wrong.

A change to a three-stage Training Course: the Starter Kit used to give one tool for all three
stages, and now gives a tool per stage.

- When a player picks the Starter Kit, their tools should be replaced with a Grapple Hook for the first stage and a Jetpack for the second and third stages, and they should have the Checkpoint Flag alongside the tool in every stage.
- When a player completes the Training Course, they should get back the tools they had when they started.
- If a player leaves the Training Course early, they should get back the tools they had when they started.
- If a player doesn't pick the Starter Kit, they should keep their own tools through all the stages.

The first draft had six items: one per stage, each saying the tool should be in the player's
hands and naming the moment they're taken to the stage, plus "leaves a stage and confirms they
want to quit". The review merged the per-stage items into one rule, dropped the test details
(tool in hand, grant timing), named "leaves early" instead of the exit screen, and reworded the
first item so the Checkpoint Flag reads as given in every stage, not as the first of three grants.

A change in a pirate game, after review: aiming with the Spyglass opens the Map (the one the
Compass and the pause menu open), with an on-screen prompt and a setting to turn it off.

- When a player has the Spyglass out anywhere the Compass or the pause menu would open the Map, a "Map" prompt with their aim button icon should appear on the left side of the screen.
- When the player aims with the Spyglass out, the Map should open.
- When a player on mobile taps the "Map" prompt, the Map should open.
- When the player switches from the Spyglass to another item, the prompt should disappear, and aiming should not open the Map.
- While a player is in a ship battle or a race, the prompt should not appear, and aiming with the Spyglass should do nothing.
- When a player opens Settings, there should be a "Spyglass Map" setting that is turned on by default.
- When the player turns the "Spyglass Map" setting off, the prompt should disappear and aiming with the Spyglass should no longer open the Map.
- Whichever option is set for "Spyglass Map" in Settings should persist when a player leaves and rejoins.

The draft's items said more than they needed to. Aiming was spelled out as "(right-click, or left
trigger on controller)", and the second item added "the same Map the Compass and the pause menu
open" after the first item had already set that up. The ship battle item followed "should do
nothing" with "it should not open the Map, and it should not end the race". The setting item ended
with "and turning it back on should restore both". The last item tested one case, "a player who
turned the setting off leaves and rejoins", where a rule covering both values was what QA needed.
The review cut each of those, leaving every item to say its one thing once.

A change in a golf game, as rewritten in review: the net around the Driving Range rises when a
player enters, and a Show Net button lowers and raises it.

- When a player enters the Driving Range, the net around the range should rise up.
- When the player hits the Show Net button and the setting is enabled, the net should lower back into the ground and the button should turn off.
- When the player hits the Show Net button and the setting is disabled, the net should rise back up and the button should light up.
- While a Long Drive Contest is running on the range, the Show Net button should still work, even though the other settings buttons are locked.
- When the last player leaves the Driving Range, the net should lower back into the ground, and the Show Net button should reset to its default (on).

The draft gave the net a purpose ("so balls don't fly off into the course") and explained the
button's look ("dim to show it's off"). It added the on-screen analytics message to the first
button item. The toggle items depended on their order ("hits the button again"), the contest item
repeated what the button does instead of "should still work", and the reset said "turn back on".
A last item checked that the next player enters with the net up, which the reset item and the
first item already cover. The rewrite cut or changed each of those.
