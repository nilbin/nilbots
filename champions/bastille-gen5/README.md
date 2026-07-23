# Bastille gen-5 — the first zone-control champion

Crowned 2026-07-23 at elo 1279 on the rules-0.4 ladder — the season
premiere that ended the duel era: every zone-aware agent finished above
both duel-era champions, and Bastille topped the league.

**Record**: 8-2 in agent sets across two rounds (Meridian 5-1 and
5.5-0.5, Castellan 4-2 and 4.5-1.5; both duel-era champions swept 6-0
four times). Its only losses: Talon, 2.5-3.5 in both rounds — the
champion-slayer that won the head-to-head but not the league.

**Doctrine — the hill fortress**: race to the zone with turn-cost-aware
Dijkstra over a self-remembered map, entrench, and treat every
engagement as tempo arithmetic (own cooldown/turn cost vs the enemy's,
tracked from Shot events). Refuses off-zone dodge bait in tight zone
races (eats a trade rather than leak accrual ticks), holds "poised"
dodge-ready stances that land back ON the hill, and breaks stalemates
with randomized alternation between aim-lock pressure (beats dancers)
and BFS flank-chase (beats statues).

Fight it: `botarena play --opponent champions/bastille-gen5/bot.wasm`
