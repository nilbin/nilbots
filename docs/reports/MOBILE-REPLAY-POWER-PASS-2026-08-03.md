DECISION NEEDED: watch several matches from the phone review gallery for at
least five minutes and confirm that the device no longer heats objectionably.
The renderer-controlled workload target passes; browser instrumentation cannot
substitute for the owner's physical device and ambient conditions.

# Mobile replay power pass

## Result

The sustained phone renderer now has one capability-selected mobile profile for
both hosted WebGL and the Canvas2D host/fallback. It presents active play at 30
fps and paused micro-life at 12 fps, caps DPR at 1.5, uses a 1024² live shadow
map, and requests a low-power GPU. Replay and live-follow React updates use the
same active cadence, and the hosted viewer no longer advances an unused local
clock while the live server clock owns presentation.

No rule, replay, camera, model, effect, fog, animation duration, telegraph, or
tile-occupancy contract changed. The full path remains available in the exact
same build through `?render-profile=full`; it is unthrottled and retains DPR 2,
2048² shadows, and the high-performance GPU request.

## Measured evidence

The comparison used the same operation-proof replay, same 844×390 CSS viewport,
DPR 3 phone context, actual director camera, and the Apple-GPU WebKit engine.
Each active measurement followed a five-second warm-up and sampled 20 seconds.
Three.js performs one shadow clear and one color clear per rendered frame, which
the profiler records separately before deriving complete frames.

| Sustained metric | Full reference | Mobile | Change |
| --- | ---: | ---: | ---: |
| Presented frames/s | 59.9 | 29.95 | -50.0% |
| WebGL draw submissions/s | 9,984 | 4,996 | -50.0% |
| Drawing buffer | 1688×780 | 1266×585 | -43.8% pixels/frame |
| Live shadow map | 2048² | 1024² | -75.0% pixels/frame |
| Weighted color + shadow pixels/s | 330.0M | 53.6M | **-83.8%** |
| Runtime/request errors | 0 | 0 | unchanged |

Paused mobile presentation measured 12.0 fps and 21.5M weighted pixels/s. The
forced Canvas2D fallback measured 30.0 fps, a 1266×585 backing buffer, 19.0k
drawing operations/s, and 22.2M backing-buffer pixels/s. Its former uncapped
DPR-3 / 60 fps screen workload was 177.7M backing-buffer pixels/s, so the owned
fill proxy falls 87.5%. The forced-context console error is expected evidence
that fallback was actually exercised.

Headless Chromium uses SwiftShader on this machine and saturated its CPU in
both tiers, so it is recorded only as a software-renderer stress check: mobile
processed 11.15 complete frames/s versus 9.35 full and had no replay or request
errors. Apple-GPU WebKit is the relevant local hardware-accelerated comparison;
the final thermal judgment remains a physical-phone watch.

## Quality and correctness gates

- The full and mobile WebKit captures at the same replay tick and actual game
  camera preserve bot silhouettes, the levitating Core, team cues, projectiles,
  health, environment texture, and live shadows. The resolution/shadow change
  is not materially visible at the delivered phone scale.
- A cadence test covers both 60 Hz and 120 Hz display clocks. The pacing anchor
  follows the ideal deadline rather than accumulating callback jitter; measured
  WebKit output is 29.95 fps rather than the first implementation's 26 fps.
- A hard unit budget fixes the iPhone-class weighted workload at 53,675,580
  pixels/s, below the 54M limit and below 17% of the full reference arithmetic.
- The shadow-quality plumbing is exercised at 1024² in the real Three scene.
  Capability selection and both explicit evidence overrides are unit-tested.
- `scripts/profile-mobile-replay.mjs` is the repeatable active/idle,
  WebGL/Canvas2D, WebKit/Chromium measurement path. It reports raw counters and
  derived rates so a future render-pass change cannot silently reuse today's
  two-clear assumption.
- The web suite passes 396/396 tests, and the labelled operation gallery passes
  its existing WebGL smoke on 10/10 real replays. The production build succeeds.
  Each theme-scoped CLI viewer grows by 2,005 bytes (8,020 bytes total, under
  0.04%) for the shared scheduling/profile code; it still contains no GLB,
  KTX2, or Basis decoder asset.
