# The Longest Year — 0.9.0 — Tester Guide

Thanks for helping test! This is the first public beta. The systems are all in
and stable; the goal now is to find out whether it's **fun and fairly balanced**.

## Setup (2 minutes)

1. SMAPI **4.0.0+** installed.
2. Unzip the mod into `Stardew Valley/Mods/` → `Mods/TheLongestYear/TheLongestYear.dll`.
3. Launch through SMAPI, **start a new game on the Standard farm**. (Farm type and
   the intro toggle are handled for you — just click through.)
4. Play. There's no separate setup step; the Season Goals tracker, Cookbook,
   Craftbook, and Junimo Stash are placed automatically.

## What to expect

- An **intro** plays before you take control (Lewis, then a Junimo). Then you
  pick your **first weekly theme**.
- Each **week**, a planning hub opens so you can pick a theme (a bonus + a
  paired downside).
- Each **season**, donate enough to the Community Center to clear that season's
  minimum. **Miss it and the year rewinds to Spring 1.**
- On every rewind (and on a win), the **Junimo Shrine** opens so you can spend
  banked **Junimo Points** on upgrades that carry strength into the next loop.
- The **Season Goals tracker** is above the CC fireplace. The **Cookbook**
  (kitchen) and **Craftbook** (table) bank recipes; the **Junimo Stash** chest
  keeps a few items across resets.

## What we most want to hear

1. **Difficulty.** Are the seasonal minimums fair? Which season was the wall?
   Did you ever feel it was impossible vs. just hard?
2. **Pricing.** Are JP earnings and shrine costs balanced? What did you buy
   first, and was it worth it? Anything that felt like a trap or an auto-pick?
3. **Pacing.** How many loops in did the run start to feel good? Did the
   carryover upgrades make later loops feel meaningfully stronger?
4. **Clarity.** Did the intro + tracker make the goal clear, or were you ever
   lost about what to do next?
5. **Anything that broke immersion or felt unfinished.**

## Reporting bugs

Please include:
- A short description + what you were doing.
- Your **`SMAPI-latest.txt`** from `Stardew Valley/ErrorLogs/` (this is the most
  useful single thing — it captures errors even if nothing looked wrong in-game).
- Your config.json if you changed any values.

## Known limitations (not bugs)

- PC only; no Android.
- Standard farm only.
- New saves only — don't load it onto an existing year-1+ save.
- Intro cutscene and dialogue are a first draft.
- Multiplayer is untested — single-player only for now.

## Tuning it yourself

If you want to experiment, `Mods/TheLongestYear/config.json` exposes the JP
values, per-season multipliers, completion bonuses, starting gold, and per-bundle
quotas. If you find numbers that feel better than the defaults, tell me what you
changed — that's exactly the kind of feedback this beta is for.
