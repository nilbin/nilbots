# Arc Relay entrants

Arc Relay's competitive entity is an **entrant**. The hosted product ships two
kinds on the same ladder surface:

- a **sheet**: saved evaluation-grade commander data executed by the frozen,
  registered stock mind;
- a **custom mind**: player source built by the controlled toolchain and run
  only in the fuel- and memory-limited WASM sandbox.

Every entrant has a stable identity, player-selected deterministic procedural
crest, eight-slot composition, and exact-compatible Elo rating. A sheet save or
mind resubmission advances its revision without resetting rating. Save-as-copy
or a new submission creates a new identity and starts at the default rating.

## Admission and composition

Both kinds snapshot eight unlocked classes with no more than two copies of one
class. Custom minds declare that composition at submission and revision time;
the reserved adaptive-composition fields remain empty in v1. The server
validates declarations against current ownership before it accepts them.

A custom mind must complete one hosted validation match without a runtime fault
before ladder opt-in. Ladder execution never trusts submitted code in-process.
Only the registered first-party stock artifact may use the trusted in-process
lane. A ranked match that trips a felt-degeneracy bar suspends the entrant from
pairing until a corrected revision is submitted and preflighted.

## Ranked lane

An account may opt in at most three entrants across both kinds. The background
pairer chooses different-account opponents by nearest rating, avoids recent
rematches where capacity permits, limits daily matches per entrant, and obeys
the existing admission locks and worker-capacity ceiling. An account's own
entrant-vs-entrant scrimmages are always unrated.

Ratings settle only after the causal broadcast is complete. Public ladder and
match surfaces expose no result-derived fact before that boundary. Every match
records the exact entrant revision, artifact/sheet hash, composition hash and
crest snapshot used for the match.

## Legacy retirement

The former Duel bot creation, submission and queue surfaces are retired. Server
admission is feature-gated off in production with an explicit retired response.
Historical bot pages, matches and replays remain available in a clearly marked
read-only archive. Engine identifiers, the nilbots brand and CLI, frozen
contracts, replay verification, and historical decisions are intentionally not
renamed or removed.
