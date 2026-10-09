# Deja-vu festival memory lines (APPROVED 2026-10-09)

Companion to `2026-10-09-deja-vu-festivals-design.md`. Each memory is something the player did at a
festival in an earlier loop. The speaker cannot place it. He or she half-remembers it, or the moment
feels familiar. `{{item}}` is the item's in-game name and `{{partner}}` is this loop's dance partner,
both filled in at runtime. Status: APPROVED by Jeff 2026-10-09 ("festival lines are good"); in `i18n/default.json` verbatim as the `festmem.*` keys.

**Rewritten 2026-10-09 for Jeff's rule:** "they don't read like deja vu or memories, they read like
prophesies, which isn't the goal at all. they're talking about present or future, they need to be
talking about the past (weren't you here last year? or I remember you winning before, but that can't
be right) or saying how this feels familiar, (You beat me again? wait...this is your first year...)
same goes for all festivals, get it?"

So every line looks back. Either the speaker remembers something from "last year" and then notices it
can't be true, or something happening right now feels like it has happened before. No hunches, no
predictions, no advice about today. Jeff's own lines stay word for word.

Also per round 2 of Jeff's rulings: festival day only (no lead-up week), no friendship gate, and the
Winter Star words are the game's own ("secret gift exchange", "secret friend", "secret gift-giver").

**When:** "Before" lines play the first time the player talks to that villager at the festival, before
the contest or main event. "After" lines play when the player talks to that villager after a result
(the egg hunt winner, the soup tasting, the grange judging, the ice fishing winner, the secret gift).
Those need a new hook. See "Post-result moments" in the design spec.

Marks: **[JEFF]** Jeff's line, verbatim. **[REWRITTEN]** an earlier draft line rewritten to look back.
**[NEW]** added in this pass.

## Egg Festival

Won the egg hunt last time (`egghunt.won`). Guaranteed the first loop after a win.

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | Before the hunt | I remember you winning before, but that can't be right. |
| Abigail **[REWRITTEN]** | Before the hunt | I could swear you beat me at this once. You weren't even here last year. |
| Abigail **[JEFF]** | After the hunt, player won | You beat me again? Wait, this is your first year. |
| Vincent **[REWRITTEN]** | Before the hunt | Hey, you found the most eggs last time! Wait, were you here last time? |
| Jas **[REWRITTEN]** | Before the hunt | I think I saw you win the egg hunt once. Was that a dream? |

Lost the egg hunt last time (`egghunt.lost`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the hunt | I remember Abigail beating you at this. But you weren't here last year. |
| Abigail **[REWRITTEN]** | Before the hunt | Didn't I already beat you at this? Huh. Must have been someone else. |
| Abigail **[NEW]** | After the hunt, Abigail won | Beat you again. Wait, again? We've never done this before. |
| Vincent **[REWRITTEN]** | Before the hunt | Abigail beat you last time. Wait, were you even here? |

## Flower Dance (festival day)

The old lead-up week lines (`dance.partner`) are gone. Their best ideas moved into the two tables below.

Came to the dance last time (`dance.attended`, anyone who was never your partner)

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | Any time | Did you come to town for the dance last year? |

A past partner says yes again (`dance.again`). Plays as a second page right after the villager's own
"yes" line, so none of these says yes itself.

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | After yes | I swear we've danced before, but this is your first time right? |
| Abigail **[REWRITTEN]** | After yes | Okay, this is weird. I feel like you've asked me this before. |
| Alex **[REWRITTEN]** | After yes | Funny. I could swear I've said yes to you before. |
| Elliott **[REWRITTEN]** | After yes | How strange. I feel as though we've shared this dance already, some other spring. |
| Emily **[REWRITTEN]** | After yes | I know this dance. I think we've done it together before. Maybe in another life. |
| Haley **[REWRITTEN]** | After yes | Didn't you step on my dress last time? Wait. There wasn't a last time. |
| Harvey **[REWRITTEN]** | After yes | Odd. I could swear you've asked me this before. I'd remember, surely. |
| Leah **[REWRITTEN]** | After yes | Funny. I feel like I already know how you dance. I've never seen you dance. |
| Maru **[REWRITTEN]** | After yes | I have the strongest feeling we've done this before. I don't see how. |
| Penny **[REWRITTEN]** | After yes | Have you asked me this before? Sorry. It just feels like you have. |
| Sam **[REWRITTEN]** | After yes | Wait, haven't we done this before? Dude, I swear we have. |
| Sebastian **[REWRITTEN]** | After yes | Weird. This feels like a rerun. |
| Shane **[REWRITTEN]** | After yes | Don't laugh. I think I danced with you once. I don't dance. |

A past partner sees you dancing with someone else (`dance.other`). Plays when the player talks to a past
partner after already having a partner this year.

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | After you have a partner | You're going with {{partner}}? I could have sworn we danced together last year. |
| Abigail **[REWRITTEN]** | After you have a partner | {{partner}}, huh? Weird. I remember dancing with you. That didn't happen, right? |
| Alex **[REWRITTEN]** | After you have a partner | {{partner}}? Huh. I remember you asking me last year. You weren't here last year. |
| Elliott **[REWRITTEN]** | After you have a partner | {{partner}}, then. Odd. I have a memory of dancing with you, and it can't be real. |
| Emily **[REWRITTEN]** | After you have a partner | Have fun with {{partner}}! It's funny, I remember us dancing. It must have been a dream. |
| Haley **[REWRITTEN]** | After you have a partner | You're taking {{partner}}? You took me last time. Wait, what last time? |
| Harvey **[REWRITTEN]** | After you have a partner | Oh, {{partner}}. Of course. Odd, I remember dancing with you. I must be mixing something up. |
| Leah **[REWRITTEN]** | After you have a partner | Have fun with {{partner}}. Strange. I keep remembering you dancing with me. |
| Maru **[REWRITTEN]** | After you have a partner | {{partner}}? I have a memory of dancing with you. I can't place when that was. |
| Penny **[REWRITTEN]** | After you have a partner | Oh, you're dancing with {{partner}}. Sorry, I thought we'd danced before. I must be confused. |
| Sam **[REWRITTEN]** | After you have a partner | Dude, {{partner}}? I swear you were my partner last time. Wait, you weren't even here. |
| Sebastian **[REWRITTEN]** | After you have a partner | Fine by me. Weird, though. I remember you dancing with me. |
| Shane **[REWRITTEN]** | After you have a partner | Thought you danced with me once. Guess not. Good. I don't dance. |

## Luau (festival day)

Put a bad or toxic ingredient in the pot last time (`luau.bad`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | Before the soup | Just don't put {{item}} in the pot this year. Wait... why did I think that? |
| Gus **[REWRITTEN]** | Before the soup | Didn't you put {{item}} in my soup once? I remember the Governor's face. You've never been to a Luau. |
| Gus **[NEW]** | After the tasting, bad again | That's twice you've done that to the Governor. Hang on. This is your first Luau. |
| Pierre **[REWRITTEN]** | Before the soup | Weren't you the one who put {{item}} in the soup last year? No, you only just moved here. |
| Marnie **[REWRITTEN]** | Before the soup | I remember someone putting {{item}} in the soup. I thought it was you, dear. That can't be right. |

Put a good or best ingredient in the pot last time (`luau.good`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | Before the soup | For some reason I think {{item}} would be amazing if you have any. |
| Gus **[REWRITTEN]** | Before the soup | Your {{item}} was the best thing in that pot last year. Hang on. You weren't here last year. |
| Gus **[NEW]** | After the tasting, good again | The Governor loved yours again. Again? Funny. This is your first Luau. |
| Jodi **[REWRITTEN]** | Before the soup | I remember the Governor loving your {{item}}. Isn't that funny? You've never been to a Luau. |
| Pierre **[REWRITTEN]** | Before the soup | I remember the Governor raving about some {{item}} you brought. Funny. I could have sworn it came from my shop. |

Put Lewis's shorts in the pot last time (`luau.shorts`). Lewis's lead-up line is dropped (no lead-up
week, and he is the host on the day).

| Speaker | When | Line |
|---|---|---|
| Fallback **[NEW]** | Before the soup | Didn't something purple end up in the soup last year? I can't remember whose it was. |
| Marnie **[REWRITTEN]** | Before the soup | I remember something purple in that pot. And Lewis going very red. I don't know where that came from. |

## Dance of the Moonlight Jellies (festival day)

Came to watch the jellies last time (`jellies.attended`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[JEFF]** | Any time | Weren't you here last year? |
| Willy **[REWRITTEN]** | Any time | I'd swear ye stood on this dock with me last year. But ye only came to town this spring. |

## Stardew Valley Fair (festival day)

Grange display took first place last time (`fair.won`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the judging | Didn't your display take first place last year? I could swear it did. |
| Pierre **[REWRITTEN]** | Before the judging | You beat my display once. I know you did. I just can't remember when. |
| Pierre **[NEW]** | After the judging, player won | First place again? Wait. You've never entered before. |
| Marnie **[REWRITTEN]** | Before the judging | I remember your display winning, dear. But this is your first fair, isn't it? |

Grange display placed second or lower last time (`fair.lost`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the judging | I remember Pierre winning the grange last year. Were you here for that? |
| Pierre **[REWRITTEN]** | Before the judging | I've beaten your display before. I'm sure of it. Don't ask me when. |
| Pierre **[NEW]** | After the judging, Pierre won | Beat you again. Funny. I've never beaten you before. |

Put Lewis's shorts in the grange display last time (`fair.shorts`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the judging | Didn't someone put something embarrassing in a display last year? I can't place it. |
| Marnie **[REWRITTEN]** | Before the judging | I remember Lewis's face at the judging. Something purple. I don't know why I remember that. |

## Spirit's Eve (festival day)

Found the golden pumpkin last time (`spirits.pumpkin`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Any time | Didn't I see you come out of the maze with the golden pumpkin? You've never even been in it. |
| Vincent **[REWRITTEN]** | Any time | You found the gold pumpkin last time! Didn't you? |
| Jas **[REWRITTEN]** | Any time | I think you found the treasure in the maze once. Was I dreaming? |

## Festival of Ice (festival day)

Won the ice fishing contest last time (`icefish.won`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the contest | Didn't you win the fishing contest last winter? You weren't here last winter. |
| Willy **[REWRITTEN]** | Before the contest | I'd swear I've seen ye pull the most fish out of this ice. Can't have been, though. |
| Willy **[NEW]** | After the contest, player won | The most fish again? Hold on. This is yer first winter here. |
| Pam **[REWRITTEN]** | Before the contest | Didn't I win money on you at this last year? Huh. Must've been somebody else. |

Lost the ice fishing contest last time (`icefish.lost`)

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | Before the contest | Didn't you come up short at the fishing contest last year? Huh. You weren't here. |
| Willy **[REWRITTEN]** | Before the contest | I'd swear I watched ye lose this contest once. Can't be. Ye only came this spring. |

## Night Market (dropped)

Both lines were lead-up week only, and the Night Market has no festival actors to carry a festival-day
line, so `nightmarket.attended` has no lines this pass.

## Feast of the Winter Star (festival day)

Only villagers the player was secret friend to in an earlier loop speak these. Any past recipient from
any loop can speak them, using the latest gift the player gave him or her.

This year's secret friend again (`winterstar.again`). Talking to him or her opens the gift prompt first,
so this plays after the player gives the gift.

| Speaker | When | Line |
|---|---|---|
| Fallback **[REWRITTEN]** | After the secret gift | Were you my secret gift-giver last year too? No, that can't be right. You weren't here. |

A past secret friend who isn't this year's (`winterstar.seen`). The lead-up `winterstar.liked` and
`winterstar.disliked` lines move here: the latest gift picks the pool, and a neutral latest gift uses the
plain line.

| Speaker | When | Line |
|---|---|---|
| Fallback, latest gift neutral **[REWRITTEN]** | Any time | Did you give me a present once? I could swear you did. |
| Fallback, latest gift loved or liked **[REWRITTEN]** | Any time | I remember you giving me {{item}} once. I loved it. But we've never swapped gifts. |
| Fallback, latest gift disliked or hated **[REWRITTEN]** | Any time | I have this memory of you giving me {{item}}. I didn't like it much. Strange, since you never have. |

## Count

72 lines: 19 fallback lines and 53 per-villager lines. 7 are Jeff's, 7 are new (six post-result lines
and the `luau.shorts` fallback), and 58 are rewritten. "I swear we've danced before, but this is your
first time right?" is now used once, as the `dance.again` fallback. Cut in this pass: all 12 per-villager `dance.partner` lead-up lines
(ideas folded into `dance.again` and `dance.other`), Lewis's `luau.shorts` lead-up line, both Night
Market lines, and the separate `winterstar.liked` / `winterstar.disliked` lead-up pools (now the gift
variants of `winterstar.seen`).

"Wait... why did I think that?" keeps its ellipsis because it is Jeff's. No other line uses one.

Post-result lines (need the new hook, see the design spec):

- Egg Festival: Abigail after the hunt, player won (`egghunt.won`), Abigail after the hunt, Abigail won
  (`egghunt.lost`).
- Luau: Gus after the tasting, bad again (`luau.bad`), Gus after the tasting, good again (`luau.good`).
- Fair: Pierre after the judging, player won (`fair.won`), Pierre after the judging, Pierre won
  (`fair.lost`).
- Festival of Ice: Willy after the contest, player won (`icefish.won`).
- Winter Star: the `winterstar.again` fallback, after the secret gift.
