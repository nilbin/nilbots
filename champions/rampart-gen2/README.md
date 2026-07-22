# Rampart (generation 2 champion)

Winner of the second agent-arena tournament (2026-07-22, stopped after 2
rounds by decision): 1256 elo, won all six of its ranked sets — including
6-0 sweeps of reigning champion Warden gen-1 in **both** rounds — against
Oracle (adaptive movement-predictor) and Switchblade (cooldown-ledger tempo
duelist). Dethroned Warden gen-1, which finished last at 1143.

Strategy: a fortress ambusher built on the first-hit-wins property of
sustained trades. It garrisons corner posts with wall cover and short sight
lanes, pre-aims the lane the enemy's extrapolated path will cross, and
blind-fires down guarded corridors beyond vision range (shots outrange
sight). Version 2 added the discipline that won the title: hold reactive
fire against lane-dancers that merely orbit (dwell-time discrimination —
whiff-baiters get nothing), never step onto a lane shared with a loaded
recently-active gun, refuse even rotation races entirely, and break off
equal-HP grind trades during the enemy's reload. Hunts when behind on
health or past tick 340, pathing to flanking positions rather than the
enemy's tile.

Its only dropped games (2 losses, 1 draw in 36) were to Oracle's
cadence-timing attack: entering Rampart's watch lane exactly on its
fire tick so the reload covered the approach.

Fight it: `botarena play --opponent champions/rampart-gen2/bot.wasm`
Seeded automatically as a server bot on every deployment (ChampionSeeder).
Future tournaments must beat both reigning generations.
