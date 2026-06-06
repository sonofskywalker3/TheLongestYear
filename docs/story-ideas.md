# The Longest Year — Story / Narrative Ideas

Running capture of narrative + story ideas for TLY and its planned sequels (TLY2 / TLY3).
**Capture-only — nothing here is approved for implementation** unless explicitly marked. We
brainstorm and flesh these out before any code/dialogue work.

Conventions:
- **[v1]** = could fit the current mod · **[TLY2]/[TLY3]** = sequel · **[any]** = era-agnostic.
- Each entry: the idea, why it works, open questions, and status.

---

## Kent — subconscious loop-awareness on return  [v1 / TLY3] · status: RECORDED, NOT IMPLEMENTED

**Idea:** Modify Kent's return/intro dialogue to hint he *subconsciously* feels the stretched
time of the loops — déjà-vu, "it feels so much longer than it was." Keep it as close to vanilla
as possible; just add the stretched-time beat.

**Draft (keeps vanilla opening verbatim, adds one new dialogue box):**
> `Hello, farmer.#$e#I've been gone so long... I feel like a stranger.#$b#It was only a couple of years... but in my dreams it stretches on and on, like I've lived the same seasons a dozen times over.$s`
(First sentence = real vanilla Kent line, pulled from `Kent.xnb`. Second box `#$b#…` is the new beat.)

**Key finding (important):** Kent only **returns in Year 2**. In a normal loop the year keeps
resetting to Spring 1, so Kent never comes back mid-loop — that's literally why the "Kent stuck at
war" community joke lands. So this dialogue would only fire **after a win**, when the player
continues into Year 2 (continue-after-victory). It's a **post-win easter egg**, which is
thematically perfect (you broke the loop → time resumes → Kent returns subtly feeling the stretch).

**Implementation notes (for when approved):** gated `Characters/Dialogue/Kent` asset edit that only
applies while TLY is active (so it never touches Kent on non-TLY saves — consistent with the
dormant gate); confirm the exact vanilla `Introduction` key text at build time so the untouched
part is byte-accurate.

**Origin:** the recurring "poor Kent is perpetually at war" joke on the beta threads
(u/concentrate7, u/Nocturnal_Elexir_Owl on r/StardewValleyMods). Broader villager-memory seed
credited to **u/Gribbleby**. Related: the v1 [1.0.0] déjà-vu plan in `TODO.md` and the TLY3
"villagers fondly remember the loops" note in memory.

---

<!-- New ideas below. Append as we go. -->
