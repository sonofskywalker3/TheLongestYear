# Morris ending playthrough feedback (2026-10-02)

Source: TODO.md, "Morris ending playthrough feedback (Jeff, 2026-10-02)", notes 1 to 8. Jeff: "you go
ahead and do the fixes and we'll test the rest next go round." Branch `story`; no manifest version
bump (feature branch rule). Spec: `docs/superpowers/specs/2026-09-25-joja-offer-design.md`.

Agents build and run unit tests only. No deploy, no game launch: the game install is shared, and one
live headless check runs at the end, after every task is merged.

## Task A: the offer scene (note 1)
`Integration/JojaOfferEvent.cs`. On entering JojaMart the vanilla room shows (Morris behind his
counter), then Morris snaps to the scene start. Hold the screen black from the warp until every actor
is placed, then fade in and run. The player must never see the normal room state first. Applies to
the skippable replay too.

## Task B: Morris's lines after a No (notes 2 and 7)
`Loop/JojaCounterPatch.cs`, `Core/Joja/JojaOffer.cs`, `Core/MetaState.cs`, `i18n/default.json`.
- Note 2: after the player refuses, Morris's refusal lines use a displeased portrait (not a smile).
  Pick the portrait from his real sheet by looking at it; check the offer-scene and position-filled
  lines too.
- Note 7: remember how the rejection happened (said to his face, or the 4th "make a decision" letter).
  After a letter rejection, Morris's first counter line is Jeff's, verbatim: "You must not have read
  my letter. You're no longer welcome here. Leave." Shown once; after that the existing
  `joja.morris.refuse-again` line. Old saves (rejected before this field existed) count as in person.
  The later-loop "position filled" line is unchanged.

## Task C: letters at least a week apart (note 6)
`Core/Joja/JojaOffer.cs` `PlanLetterDays`. Still two "come see me" letters per season on random days,
but any two letters at least 7 days apart, including across a season boundary. Unit tests over many
seeds. Existing saves keep their already-rolled `JojaLetterDays` unless they break the rule, in which
case they re-plan from today (decide in code, test it).

## Task D: Game Over eye glow (note 5)
`UI/JojaGameOverMenu.cs`. The farmer's glow sits on the bridge of the nose for Jeff's farmer. Tone the
glow down (both figures) and place the farmer's glow over the whites of the eyes for any farmer:
male/female base, any skin, eye colour, hair, hat. Derive the spots from the farmer sprite actually
drawn (the eye pixels of the base texture for the drawn frame), not fixed offsets measured on one farmer.

## Task E: the bad ending (notes 3, 4, 8)
`Integration/JojaBadEnding.cs` and its command files.
- Note 3: after the coops and barns go up, show only the first row, then fade out (no pan over every row).
- Note 4: in town, hold on Pierre's about 1 second, then pan up to the Community Center, shown as the
  Joja warehouse (vanilla draws it for `JojaMember` with the CC refurbished; borrow it in memory only,
  nothing saved), then the Game Over screen. Beach: per Jeff's answer.
- Note 8: the whole bad ending plays in daylight whatever the clock says when the player says Yes.
