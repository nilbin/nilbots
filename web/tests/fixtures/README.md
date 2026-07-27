# Engine-authored replay fixtures

The normalization tests currently use compact hand-authored structural inputs.
When the replay-v2 serializer is frozen, place its byte-for-byte outputs here as:

- `frontline-replay-v2.json`
- `frontline-replay-v2-partial-zero-tick.json`

Tests should read those files as text first so backend hash verification can be
tested independently, then `JSON.parse` and pass the unknown value to
`decodeReplay`. Do not regenerate or reserialize them in TypeScript.
