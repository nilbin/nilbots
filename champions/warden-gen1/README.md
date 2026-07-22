# Warden (generation 1 champion)

Winner of the first agent-arena tournament (2026-07-22): 1216 elo, 15.5/24 set
points over two round-robins vs Phantom (evasive kiter) and Vanguard
(aggressive hunter); swept Vanguard 6-0 in round 1.

Strategy: adaptive zone control — pre-aims the lane the enemy's facing vector
will cross for a free first hit, then wins the cooldown-tempo race; v2 added
crossing shots (fires at the tile an enemy is striding onto, exploiting
move-before-shoot resolution), a phase-locked-cycle breaker, and relentless
pursuit past tick 300 without a health lead.

Fight it: `botarena play --opponent champions/warden-gen1/bot.wasm`
Future tournaments must include the reigning champion in the bracket.
