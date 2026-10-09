# Deja-vu festival memory lines (draft for Jeff's review)

Companion to `2026-10-09-deja-vu-festivals-design.md`. Each memory is something the player did at a
festival in an earlier loop. The speaker cannot remember it, only half-feel it. "Festival day" lines
are pushed onto the villager's festival dialogue and play before the contest or main event; "lead-up"
lines play in ordinary dialogue in the week before the festival. `{{item}}` is the item's in-game name
and `{{partner}}` is this loop's dance partner, both filled in at runtime. Jeff's own lines are used
verbatim. Status: DRAFT, not coded.

**Updated 2026-10-09 for Jeff's rulings.** Who can speak:

- Ordinary memories: any villager at familiarity 60 and up (phase 1's tier 1), 20% each.
- Bond memories (every past dance partner and every past Winter Star recipient, from any loop):
  50% each, no familiarity needed.
- After an egg hunt win: guaranteed at the next Egg Festival, from whichever eligible villager the
  player talks to first.
- One memory per festival per loop. The first one heard silences the rest.

Fallback lines go to any eligible villager without a line of his or her own for that memory.

Marks: **[NEW]** added in this pass, **[CHANGED]** reworded or moved in this pass. Unmarked lines are
unchanged from the first draft.

## Egg Festival (festival day, before the hunt)

Won the egg hunt last time (`egghunt.won`). Guaranteed the first loop after a win.

| Speaker | Line |
|---|---|
| Fallback | I've got a hunch you'll win the egg hunt today. |
| Abigail | I have this feeling I'm not winning the egg hunt this year. |
| Vincent | I think you're going to find all the eggs! |
| Jas | Are you good at finding eggs? I think you are. |

Lost the egg hunt last time (`egghunt.lost`)

| Speaker | Line |
|---|---|
| Fallback | Don't feel bad if Abigail wins. I just have a feeling she will. |
| Abigail | Something tells me I'm beating you today. Don't ask how I know. |
| Vincent | I think Abigail wins. She always wins. |

## Flower Dance

### Lead-up week

Came to the dance last time (`dance.attended`, anyone who was never your partner)

| Speaker | Line |
|---|---|
| Fallback | Did you come to town for the dance last year? |

Danced with this villager in any earlier loop (`dance.partner`, only past partners speak it)

| Speaker | Line |
|---|---|
| Fallback **[NEW]** | I swear we've danced before, but this is your first time right? |
| Abigail | I had a dream I went to the Flower Dance with you. Weird, right? |
| Alex | For some reason I keep picturing you at the Flower Dance. With me. |
| Elliott | I keep imagining the Flower Dance with you as my partner. I can't think where the idea came from. |
| Emily | I keep seeing us dancing together in my dreams. The Flower Dance, I think. |
| Haley | I already know what I'm wearing to the dance. For some reason I think you've seen it. |
| Harvey | Odd. I keep remembering a dance with you. I don't think we've ever danced. |
| Leah | I keep thinking I already know how you dance. That doesn't make any sense. |
| Maru | I keep remembering dancing with you. I'd remember if that had happened. I think. |
| Penny | I've been thinking about the Flower Dance. I keep picturing you there with me. |
| Sam | Dude, I swear we danced together once. Was that a dream? |
| Sebastian | I hate dancing. But I keep thinking I'd go if it was with you. |
| Shane | Don't laugh. I keep thinking I danced with you once. I don't dance. |

### Festival day, a past partner says yes again (`dance.again`) [NEW]

Plays as a second page right after the villager's own "yes" line, so none of these says yes itself.

| Speaker | Line |
|---|---|
| Fallback **[NEW]** | I swear we've danced before, but this is your first time right? |
| Abigail **[NEW]** | This is going to sound weird, but I think we've done this before. |
| Alex **[NEW]** | Funny. I feel like I've said yes to you before. |
| Elliott **[NEW]** | How strange. I feel as though we've shared this dance already. |
| Emily **[NEW]** | I think we've danced together before. Maybe in another life. |
| Haley **[NEW]** | Try not to step on my dress. I feel like you did that once. |
| Harvey **[NEW]** | Odd. I could swear you've asked me this before. |
| Leah **[NEW]** | I think I already know you're a good dancer. I don't know how. |
| Maru **[NEW]** | I have the strongest feeling we've done this before. That's not possible. |
| Penny **[NEW]** | I feel like you've asked me this before. Sorry, that's silly. |
| Sam **[NEW]** | Wait, haven't we done this before? I swear we have. |
| Sebastian **[NEW]** | I feel like I've already done this with you. Weird. |
| Shane **[NEW]** | I feel like I already danced with you once. Don't ask. |

### Festival day, a past partner sees you ask someone else (`dance.other`) [NEW]

Plays when the player talks to a past partner after already having a partner this year.

| Speaker | Line |
|---|---|
| Fallback **[NEW]** | You're dancing with {{partner}}? I thought you'd ask me. I don't know why. |
| Abigail **[NEW]** | Huh. I had a feeling you'd ask me this year. |
| Alex **[NEW]** | {{partner}}, huh? I figured you'd ask me. No idea why. |
| Elliott **[NEW]** | I confess I half expected you to ask me. I can't say why. |
| Emily **[NEW]** | I was so sure you'd ask me! My intuition must be off today. |
| Haley **[NEW]** | You asked {{partner}}? I was sure it'd be me. |
| Harvey **[NEW]** | Oh, you asked {{partner}}. I don't know why I thought it would be me. |
| Leah **[NEW]** | Have fun with {{partner}}. I had a feeling you'd ask me. |
| Maru **[NEW]** | I was sure you'd ask me. I'm usually better at predictions. |
| Penny **[NEW]** | Oh, you're dancing with {{partner}}. I thought you might ask me. |
| Sam **[NEW]** | Dude, I was sure you'd ask me. Weird. |
| Sebastian **[NEW]** | I was sure you'd ask me. Not that I wanted to dance. |
| Shane **[NEW]** | Thought you'd ask me. Good thing you didn't. I don't dance. |

## Luau (festival day, before you add to the soup)

Put a bad or toxic ingredient in the pot last time (`luau.bad`)

| Speaker | Line |
|---|---|
| Fallback | Just don't put {{item}} in the pot this year. Wait... why did I think that? |
| Gus | Do me a favor and keep {{item}} out of the pot. Don't ask me why. |
| Pierre | If you're thinking of adding {{item}} to the soup, don't. I can't tell you how I know. |
| Marnie | Oh, not {{item}} in the soup, dear. I'm not sure why I said that. |

Put a good or best ingredient in the pot last time (`luau.good`)

| Speaker | Line |
|---|---|
| Fallback | For some reason I think {{item}} would be amazing if you have any. |
| Gus | Funny. I keep tasting {{item}} in that soup, and nobody's put it in yet. |
| Jodi | I have a feeling {{item}} is just what that pot needs. |
| Pierre | {{item}} would go great in the soup. I just know it. |

Put Lewis's shorts in the pot last time (`luau.shorts`)

| Speaker | When | Line |
|---|---|---|
| Lewis | Lead-up week | I've had the strangest urge to check my dresser all week. |
| Marnie | Festival day | I keep worrying something embarrassing is going to end up in that pot. Something purple. |

## Dance of the Moonlight Jellies (festival day)

Came to watch the jellies last time (`jellies.attended`)

| Speaker | Line |
|---|---|
| Fallback | I feel like we've watched the jellies together before. |
| Willy | I could swear I've stood on this dock with ye before. |

## Stardew Valley Fair (festival day, before the judging)

Grange display took first place last time (`fair.won`)

| Speaker | Line |
|---|---|
| Fallback | Something tells me you're the one to beat at the judging. |
| Pierre | I've got a bad feeling about the grange judging. About you, specifically. |
| Marnie | I think your display is going to win, dear. Call it a hunch. |

Grange display placed second or lower last time (`fair.lost`)

| Speaker | Line |
|---|---|
| Fallback | I have a feeling Pierre's going to win the judging. |
| Pierre | I've got a good feeling about my display this year. Better than yours, I bet. |

Put Lewis's shorts in the grange display last time (`fair.shorts`)

| Speaker | Line |
|---|---|
| Fallback | I keep thinking something embarrassing turns up at the judging. Isn't that strange? |
| Marnie | If you see anything purple in a display, tell Lewis. I don't know why I said that. |

## Spirit's Eve (festival day)

Found the golden pumpkin last time (`spirits.pumpkin`)

| Speaker | Line |
|---|---|
| Fallback | I've got a feeling you know your way around the maze already. |
| Vincent | I bet you know where the treasure is. You look like you know. |
| Jas | Are you going in the maze? I think you're good at it. |

## Festival of Ice (festival day, before the contest)

Won the ice fishing contest last time (`icefish.won`)

| Speaker | Line |
|---|---|
| Fallback | I think you're going to win the fishing contest. Just a feeling. |
| Willy | Somethin' tells me ye'll pull the most fish today. |
| Pam | I've got money on you for the fishing. Don't know why. |

Lost the ice fishing contest last time (`icefish.lost`)

| Speaker | Line |
|---|---|
| Fallback | Something tells me the fishing contest isn't your day. |
| Willy | Keep yer line still out there today. I've a feelin' ye need tellin'. |

## Night Market (lead-up week)

Went to the Night Market last time (`nightmarket.attended`)

| Speaker | Line |
|---|---|
| Fallback | Feels like I've seen you at the Night Market before. Have you been? |
| Willy | The Night Market's comin' up. Feels like I already saw ye there. |

## Feast of the Winter Star

Only villagers you were Secret Santa for in an earlier loop speak these. **[CHANGED]** The first draft
used last loop's recipient only; now any past recipient from any loop can speak them, using the
latest gift you gave him or her.

### Lead-up week

You're his or her Secret Santa again this loop, after you've read Lewis's letter (`winterstar.again`)
[NEW]

| Speaker | Line |
|---|---|
| Fallback **[NEW]** | I have a funny feeling you're my Secret Santa this year. |

Gave this villager a gift he or she loved or liked (`winterstar.liked`)

| Speaker | Line |
|---|---|
| Fallback | I have a feeling someone's going to give me {{item}} this year. I hope so. |

Gave this villager a gift he or she disliked or hated (`winterstar.disliked`)

| Speaker | Line |
|---|---|
| Fallback | For some reason I really hope nobody gives me {{item}} this year. |

### Festival day, a past recipient who isn't this year's (`winterstar.seen`) [NEW]

| Speaker | Line |
|---|---|
| Fallback **[NEW]** | Did you give me a present once? I keep thinking you did. |

## Count

80 lines: 20 fallback lines and 60 per-villager lines. 29 are new in this pass (both new Flower Dance
tables, the `dance.partner` fallback, `winterstar.again`, `winterstar.seen`), none reworded. Jeff's
four lines are used verbatim; "I swear we've danced before, but this is your first time right?" is
used twice, as the `dance.partner` and `dance.again` fallback, so mod-added partners get it in the
lead-up and at the dance. "Wait... why did I think that?" keeps its ellipsis because it is Jeff's; no
other line uses one.
