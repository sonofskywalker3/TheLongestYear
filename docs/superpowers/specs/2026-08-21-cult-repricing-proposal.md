# Cult / hard-gate upgrade repricing — proposal (2026-08-21, awaiting ruling)

Input: `docs/superpowers/notes/2026-08-21-jp-budget.md` — a strong player banks **≈ 8,000–9,500 JP
per winning loop** (median 8,650; ≈ 1,930 of it fixed awards); an "everything now" player ≈
4,400–4,900; nobody exceeds ≈ 11,300. Balance feedback (all pre-engine): red cabbage + starfruit
are the only year-1-impossible asks; the 750-JP cult upgrades delete them after one loop
(PokeTheSilver204); a lucky cart buy upends a run (jneedham2); Spring-28 reset farming is a cheese
(Dusklight7).

Rule from the brief: **a hard-gate deletion should cost more than a strong player banks in one
loop.**

## Audit list

| Upgrade | Today | What it really does | Proposal | Why |
|---|---|---|---|---|
| `cult_red_cabbage` | 750 | Summer Mixed Seeds roll Red Cabbage 10 %/seed → deletes the year-1 gate after one loop | **10,000** | > max STRONG (9,512); ≈ 1.2 strong loops, 2+ ordinary winning loops, 5+ failing loops |
| `cult_starfruit` | 750 | same for Starfruit (Summer) | **10,000** | same rule; keep the two equal (they were equalised on purpose) |
| `keep_bus_unlocked` | 1,500 | bus stays repaired → the Vault money gate (3,125 → 31,250 g at +25 %) auto-passes every loop | **4,500** | it deletes a gate, but a gold gate a strong player passes anyway; ≈ half a strong loop |
| `fortune_rare_fish` | 525 | +25 % bite rate (JC-2 approximation, not a gate) | **keep 525** | not a deletion; it is a convenience buy |
| Cart Stall `cart_slot_2..10` | 40/80/140/220/320/450/620/850/1,200 (Σ 3,920) | each tier reveals one more Traveling Cart item; a full cart is the "lucky buy" path (red cabbage / truffle / sandfish) | **60/120/220/360/560/800/1,100/1,500/2,000 (Σ 6,720)** | keep the first stalls cheap (QoL), make the full-cart endgame cost ≈ ¾ of a strong loop; the Cart Whisperer tiers (350…1,500) already gate on them |

Sanity: a strong player who wins loop 1 with ~8,600 JP can afford ONE of {red cabbage, starfruit}
only by skipping every keep — the gate deletion is a deliberate multi-loop investment, not a
one-loop reflex. An ordinary player (4,500/loop, some keeps) reaches it in ~4 loops.

## Obtainability instead of a mixed-seeds roll?

The budget says the **price** is the wrong lever today (750 ≈ 9 % of a strong loop), not the
mechanism — but the roll has two independent problems worth deciding now:

1. **RNG with no agency.** 29 Summer Mixed Seeds ≈ 3 Red Cabbages; a bad roll gives 0 and the
   player learns nothing. Deterministic seed access (Pierre stocks Red Cabbage Seeds in Summer;
   Starfruit Seeds at Sandy in Summer — Sandy needs the bus) keeps the gate *hard-but-plannable*:
   9/13-day grow times mean the player must plant by Summer ~15 and still pay the gold.
2. **The roll is year-1-only in spirit but works every loop forever** — once bought, the gate is
   gone for good.

**Recommendation:** replace each roll with a 2-tier obtainability chain, priced so the chain
total satisfies the rule:

| Tier | Effect | Price |
|---|---|---|
| `cult_red_cabbage_1` "Cabbage Rumour" | Red Cabbage Seeds appear in the Traveling Cart's Summer stock pool (subject to Cart Stall) | 2,500 |
| `cult_red_cabbage_2` "Pierre's Summer Order" | Pierre sells Red Cabbage Seeds in Summer (year 1) | 7,500 (Σ 10,000) |
| `cult_starfruit_1` / `_2` | same shape via the cart / Sandy (Oasis) | 2,500 / 7,500 |

The Mixed-Seeds roll (`MixedSeedsPatch`) would be retired. If you'd rather keep the roll, take the
price column only (10,000 each).

## Ruling needed

1. Prices for `cult_red_cabbage` / `cult_starfruit`: 10,000 each (rule-derived) — or a softer
   number (e.g. 7,500 ≈ median strong loop minus fixed awards)?
2. `keep_bus_unlocked` → 4,500? `fortune_rare_fish` stays 525?
3. Cart Stall curve → 60…2,000 (Σ 6,720)?
4. Mechanism: keep the Mixed-Seeds roll, or switch to the 2-tier obtainability chains (cart pool →
   Pierre/Sandy in Summer)?
