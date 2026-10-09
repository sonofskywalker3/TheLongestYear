# Deja-vu festival memory lines (draft for Jeff's review)

Companion to `2026-10-09-deja-vu-festivals-design.md`. Each memory is something the player did at a
festival in an earlier loop. The speaker cannot remember it, only half-feel it. A villager speaks only
at familiarity 60 and up (phase 1's tier 1), and only lines for a memory the player actually has.
"Festival day" lines are pushed onto the villager's festival dialogue and play before the contest or
main event; "lead-up" lines play in ordinary dialogue in the week before the festival. `{{item}}` is
the item's in-game name, filled in at runtime. Jeff's own three lines are used verbatim as the
fallback lines for their memories. Status: DRAFT, not coded.

Fallback lines go to any eligible villager without a line of his or her own for that memory.

## Egg Festival (festival day, before the hunt)

Won the egg hunt last time (`egghunt.won`)

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

## Flower Dance (lead-up week)

Came to the dance last time (`dance.attended`, anyone except last time's partner)

| Speaker | Line |
|---|---|
| Fallback | Did you come to town for the dance last year? |

Danced with this villager last time (`dance.partner`, only that partner speaks it)

| Speaker | Line |
|---|---|
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

## Feast of the Winter Star (lead-up week, only last time's gift recipient speaks)

Gave this villager a gift he or she loved or liked (`winterstar.liked`)

| Speaker | Line |
|---|---|
| Fallback | I have a feeling someone's going to give me {{item}} this year. I hope so. |

Gave this villager a gift he or she disliked or hated (`winterstar.disliked`)

| Speaker | Line |
|---|---|
| Fallback | For some reason I really hope nobody gives me {{item}} this year. |

## Count

51 lines: 15 fallback lines (3 of them Jeff's own, verbatim), 36 per-villager lines. Jeff's line
"Wait... why did I think that?" keeps its ellipsis because it is his; no other line uses one.
