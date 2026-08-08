# Arc Relay ten-operation live proof

This is an evaluation-grade mechanics corpus, not the player-facing sheet
format. Each sheet embeds one bounded team operation beside a complete stock
baseline. The same compiled `stock-mind-v3` artifact loads every sheet as
deterministic data.

Every retained example must show, in one authoritative WASM activation:

1. causal trigger evidence;
2. atomic participant preparation;
3. one locked branch commitment;
4. the authored mission success, including any required signature action;
5. physical recovery or its bounded deadline; and
6. surviving participants emitting ordinary baseline role tags after release.

Abort-only traces are diagnostic and never count as proof.

| Operation | Prepare and commit rule | Counted success | Failure and baseline behavior |
| --- | --- | --- | --- |
| Rear Hook | Two Towlines infiltrate after public Well timing and visible forward pressure; commit only to an observed carrier in the enemy return corridor. | The exact bound Core becomes loose or ours after at least one `tractor-hook`. | Before commit, loss of a required stager recovers. After commit, an optional strike survivor may continue; deadline releases all survivors. |
| Lantern Sweep | A carrier pauses at a safe fork while a Lantern and screen probe; visible risk selects alternate return before the ordered primary branch. | The carrier crosses onto its home half. | Core loss aborts. Surviving participants extract and return to their complete baseline roles. |
| Fork Shadow | A visible returning carrier triggers two Towlines; the observed north/south route selects one cutoff branch. | The exact carrier loses its Core or is forced outside that committed route after a `tractor-hook`. | The branch never flips. Optional strike partners may degrade to one survivor; deadline releases it. |
| Birth Rotation | Public next-Well timing claims any feasible two of three declared reserves. | Two committed rotators reach the forward-objective band. | Visible home overload aborts; otherwise recovery returns the surviving pool to baseline. |
| Escort Counterpunch | A pressured own carrier and one declared guard stage at the fork; two visible threats select the counter route before direct return. | An own carrier crosses onto its home half. | Core loss aborts. A dead guard is never silently replaced after commitment. |
| Smoke Breach | Visible centre resistance stages a Veil and one of two declared breachers. | Two committed bodies cross into centre-forward after `smoke-canister` use. | A three-enemy home emergency aborts; the concentrated pair otherwise accepts the exposed-lane cost. |
| Hardlight Gate | A pressured return claims a Lantern and Mason and commits once both form the home gate. | The carrier reaches its home half after the Mason has used `hardlight-block`. | Core loss aborts; the gate pair physically extracts before ordinary objective work resumes. |
| Relay Catch | A south Relay carrier and its paired Relay screen commit immediately; the receiver follows the specifically assigned carrier. | The assigned receiver becomes the Core carrier after `arc-toss`. | The in-flight one-tick possession gap is not mistaken for loss; deadline handles a genuinely failed catch. |
| Decoy Switch | Public Well timing plus visible north pressure stages one north decoy and a declared south pair. | Both hitters reach south-forward after the locked south-pincer branch. | A home overload aborts. The decoy and pair otherwise pay the loss of centre coverage. |
| Emergency Exchange | A wounded carrier in the risk fork claims the declared Switchback. | `exchange` occurs during the activation and an own carrier is on the home half. | Core loss or participant loss aborts; the Switchback cannot create a success merely by arriving late. |

Reproduce the proof from the repository root:

```sh
python3 scripts/generate-arc-relay-intelligent-operation-proofs.py
dotnet src/BotArena.Cli/bin/Debug/net10.0/botarena.dll build arena-bots/arc-relay/stock-mind-v3
python3 scripts/arc-relay-operation-proof.py prove \
  --catalog arena-bots/arc-relay/intelligent-operation-proof-v1-2026-08-02/catalog.json \
  --artifact arena-bots/arc-relay/stock-mind-v3/out/bot.wasm \
  --output /tmp/nilbots-operation-proof \
  --workers 4
```

The tracked compact receipt is
[`evidence/live-proof-summary.json`](evidence/live-proof-summary.json). Full
canonical replays are deliberately regenerated and verified rather than
committed. This corpus proves executable grammar and recovery behavior only;
it is not a balance, reliability, fun, or product-UX claim.
