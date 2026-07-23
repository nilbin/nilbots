# DX findings — agent-arena gen-5 (season premiere, rules 0.4)

Single challenger (Meridian, adaptive all-court) vs the gen-4 veterans and
both duel-era champions, one improvement iteration, 6-map pool. This
generation's specific test: do the shipped v0.4 docs stand entirely on their
own? **Headline: yes — finding #6 below is "no contradictions found"; every
zone semantic the docs state was confirmed by experiment.** Tournament
outcome and crown in DECISIONS #55. Findings, ranked:

## Open
1. **[bug, high] SpawnVariation's fallback bypasses ZoneSpawnFairness.** A
   ranked game spawned at zone distances 1 vs 4+ despite the documented
   ≤2-step bound — consistent with the 64-attempt sampler exhausting and
   returning MAP-FIXED spawns, which skip every constraint. At gen-4/5
   parity the opening race IS the match, so this decides games. Fix: raise
   attempts under fairness constraints and/or make the fallback re-sample
   with constraints relaxed in documented order (verify with a seed sweep
   first).
2. **[med] `play --bot .` leaves `out/bot.wasm` stale** (only `build .`
   refreshes it). Meridian sparred its "mirror" against a 15-iterations-old
   artifact and burned a long false-determinism investigation. Fix: `play`
   refreshes the copy, or prints the artifact hash it used.
3. **[med] `set` cannot pin its sampled map/seed pairs for exact A/B** —
   every invocation resamples; `--seed N` looks accepted but doesn't pin.
   The Repro-line workaround exists but is easy to miss; 6-game variance
   repeatedly misled iteration (a 1-5 → 3-3 swing with zero code change).
   Fix candidates: honor `--seeds` count-of-one by cycling, or add
   `--repro "<line>"`.
4. **[med] Map knowledge is undiscoverable except by playing.**
   `botarena maps` lists name/size only; zone layout and wall structure
   must be reverse-engineered from replays (arena-01's two disconnected
   pads are strategically enormous and documented nowhere). Fix:
   `botarena maps --show <id>` ASCII render with zone tiles marked.
5. **[low] Shot events under-deliver vs the docs' phrasing** — a
   VisibleEvent for an out-of-vision shot carries origin only, no ray end
   or direction; "muzzle flashes carry information" oversells it. Either
   enrich the event (SDK/protocol bump) or soften the doc line.
6. **[doc, positive] No rule/doc contradictions found.** Global exclusive
   accrual, end-of-tick position keying, post-move shot resolution,
   mutual-move-block deadlocks — all confirmed exactly as documented. Two
   doc suggestions kept: state explicitly that individual games are not
   expected to be balanced (only mirrored set pairs), and warn that the
   mutual-move-block rule enables deliberate deadlock-farming.
7. **[low] Cosmetics**: 64-char hash names in `set` tables when sparring
   artifacts (label by parent dir like `play` does); a per-tick zone-score
   column in `replay --summary` would save Debug.Write instrumentation.

## Worked as advertised
Bit-identical artifact parity on first submission (again); fuel headroom
~400x; the `--full` summary flag, Repro-line reruns, zone-aware summaries
and the docs' corner-strict-vision warnings all used in anger. The
determinism scare in #2 resolved to: determinism itself is flawless — the
tooling just let two different artifacts wear the same filename.
