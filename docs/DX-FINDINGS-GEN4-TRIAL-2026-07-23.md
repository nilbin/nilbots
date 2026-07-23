# DX findings — agent-arena gen-4 TRIAL (hill), 2026-07-23

First run at the new **trial** length (time-boxed single challenger, one
round-robin + mirror set, no improvement iterations): Castellan, a
zone-control tactician under `BOTARENA_RULES=hill` (then shared accrual).
Pipeline verdict: green end-to-end — rules pin used throughout, first-poll
"Built" with bit-identical artifact parity, 12-0 server sweep vs both
champions, mirror set surfaced the shared-accrual degeneracy that became
DECISIONS #50. Findings:

## Fixed same-day
1. **[severe] SDK strafe docs said "Rules 0.3+"** while strafe shipped in no
   ruleset — the agent built a strafe-based movement plan and lost its first
   three practice games to silent Wait/Blocked coercion. Doc-comments on
   strafe/zone now describe *when the feature is inert* instead of naming a
   version (fa75d2b — that pattern can't go stale when ship decisions
   change).
2. **[med] Zone semantics had to be reverse-engineered** (accrual counts the
   post-tick tile regardless of action; ZoneTiles is the full list from tick
   0, not vision-gated; simultaneous accrual when both stand on zone; the
   150 threshold only in the brief). The skill's hill brief now states all
   four — rewritten for exclusive accrual (DECISIONS #50).
3. **[med] Site docs' ranked pool was stale** ("basic-01 and arena-01") —
   now lists crossfire-01 (d9c0aaf).
4. **[low] `build .` printed "Game rules: 0.3"** even under a hill pin —
   line removed; artifacts are rules-agnostic, the ruleset is chosen per
   match.
5. **[low] Resolution-order phrasing read contradictory** ("shoot from
   post-move positions" vs "ray fires from pre-move position") — docs now
   say shots resolve against the post-move board and the shooter itself
   never moves that tick. Also corrected the MaxTicks line: tiebreak is
   health then damage dealt; only all-equal is a draw.

## Open
6. **[low] Timeline off-by-one takes learning** — the summary states the
   convention, but cooldown-cadence analysis is fiddly; a worked example in
   docs/REPLAY-FORMAT.md would help. Not blocking; revisit if gen-4 agents
   trip on it too.

Balance observations went to docs/GAME-DESIGN.md and DECISIONS #50 (hill v2
= exclusive accrual), not here.
