# Engine-authored replay fixtures

The replay-v2 normalization boundary is pinned to byte-for-byte engine output:

- `frontline-replay-v2.json`
- `frontline-replay-v2-partial-zero-tick.json`

`FrontlineReplayV2FixtureTests` produces both with the JS-unsafe seed
`9007199254740993`. The finalized fixture covers initial-life destruction, a
respawn gap, and the next lives returning; the partial fixture is the hashless
zero-tick prefix from an actor failure.

Normal test runs only compare exact bytes. Regenerate deliberately from the
repository root with:

```bash
UPDATE_GOLDEN=1 dotnet test tests/BotArena.Engine.Tests \
  --filter FullyQualifiedName~FrontlineReplayV2FixtureTests
```

The frontend test reads each file as raw text and passes it to
`decodeReplayJson`, preserving lexical integers and the original bytes for hash
verification. Do not regenerate or reserialize these fixtures in TypeScript.
