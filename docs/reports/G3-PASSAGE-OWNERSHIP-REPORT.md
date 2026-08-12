# G3 passage ownership — fast design review

Date: 2026-08-12  
Branch: `codex/order-urgency`  
Base: `main` at `4aaf80cc`  
Review gallery: <https://scotland-legislature-define-except.trycloudflare.com>

This is an in-process gameplay-design review, not a balance read or hosted
promotion. Tracked WASM, immutable stock, playlists, rating books, maps,
playbooks, rules, and release versions were not changed.

## Owner catch and root cause

Gallery G3 (`e-9005`), West team, units 3, 4 and 6 formed a deterministic
traffic circuit around `(8,8)`, `(7,9)` and `(7,8)` from roughly t138 through
t164. They were selected for three different legitimate normal-urgency
orders—centre Well, far Well, and centre watch—but rotated one opening every
six ticks:

1. one waiter advanced into `(8,9)`;
2. strict progress paid its block debt down;
3. the next waiter immediately outranked it;
4. `make-way` displaced the winner back into the pocket;
5. the third waiter repeated the exchange.

Every engine move succeeded. Static-stall and blockade summaries therefore
looked healthy even though the squad made no net progress. This was introduced
by urgency/debt arbitration: the executor had aging for a body waiting to enter
a passage, but no hysteresis for the body which had just won it.

The separate observation that team-wide `enemy-visible` can select a fight for
a body without personal sight remains a sheet/condition-design question. It was
not required to fix this generic traffic circuit and is not changed here.

## Fix

`TacticalTrafficGovernor` now grants a short, progress-backed **passage
lease** after a body advances through observed allied denial:

- it lasts at most four observations;
- two unsuccessful movement observations end it early;
- a changed life, order, goal, authored urgency, or activation ends it;
- another allied denial is required before it can be earned again;
- a higher effective urgency still wins;
- a full debt/starvation tier ranks before the lease, so bounded hysteresis
  cannot defeat starvation recovery;
- ties remain deterministic—there is no random, role, class, map, coordinate,
  carrier, or action-specific policy.

The reason grammar exposes the remaining lease, for example
`urgency-normal+b0+passage4`. A body asked to clear for that owner consequently
names `to-normal+b0+passage4` in its existing `make-way` reason.

## Regression and diagnostic

The real Tapline `e-9005` scene is now a focused integration test using the
same Wellwright/Hunter sides and seed. It requires the scene to exercise a
passage reason and rejects a repeated constant-period assigned-position circuit
for the three caught actor lives.

`scripts/arc-relay-traffic-read.py` now detects **squad loops**: three nearby
same-life bodies returning to the same assigned positions at a constant 2–12
tick period, with `make-way` present in both laps, sustained for at least 13
frames. That is specifically the successful-but-circular class which blockade
and individual A-B flap detectors miss. Its small unit fixture rejects both a
stationary group and a linearly advancing group.

On the motivating side:

| G3 West | Before | Candidate |
| --- | ---: | ---: |
| sustained squad-loop runs | 5 | 0 |
| worst squad loop | 36 frames | 0 frames |
| flaps / body-ticks | 104 / 1,702 (6.11%) | 49 / 2,604 (1.88%) |
| reversals / body-ticks | 118 / 1,702 (6.93%) | 78 / 2,604 (3.00%) |
| longest single-tile hold | 32 ticks | 34 ticks |

The three caught bodies leave the northwest pocket in the candidate instead of
cycling there. The longer match is an emergent outcome of the changed traffic
sequence, not a quality claim.

Across all twelve sides in the standard exhibit, the tightened diagnostic
reports zero sustained squad-loop runs. Every initial body leaves deployment by
t4, every side reaches its own Well, no body exceeds a 48-tick single-tile hold,
and every command retains a non-empty reason.

## Standard six-cell exhibit

Candidate Hunter versus unchanged Wellwright on `tapline-1`:

| Cell | Observed result |
| --- | --- |
| w-9001 | Wellwright win, reactor destroyed at t462 |
| w-9002 | Hunter win, reactor destroyed at t445 |
| e-9005 | Hunter win, reactor destroyed at t489 |
| e-9009 | Hunter win, reactor destroyed at t435 |
| w-9012 | Wellwright win, reactor destroyed at t464 |
| e-9004 | Hunter win, reactor destroyed at t523 |

This is a **4–2 exhibition record**, not a balance conclusion. The gallery cards
state the actual opponent, score, trigger, tactic, counterplay, fallback, and
the G3 watch cue. G3 is sample 3.

## Verification

- focused compiler, movement-governor and real-scene tests: **48/48 passed**;
- traffic diagnostic tests: **2/2 passed**;
- focused canonical/broadcast reason parser: **9/9 passed**;
- deterministic duplicate six-cell runs produced byte-identical replay gzip
  for the checked G3 cell;
- `git diff --check`: passed;
- public 390×844 Chromium smoke: six cards, G3 replay canvas and score HUD
  loaded with no page, console, or request errors.

The broad web run remains **462/463** on the same pre-existing missing Meshy
sentinel source noted in the parent urgency report. This branch does not touch
that art pipeline.

## Promotion boundary

No controlled WASM build or playlist work belongs in this fast iteration. If
the owner approves and asks to merge/promote the mind-source change, the
separate promotion gate still owes in-process/WASM gameplay parity, a new
immutable stock/playlist version if required, and a rating-continuity decision
from its stock-cell digest.
