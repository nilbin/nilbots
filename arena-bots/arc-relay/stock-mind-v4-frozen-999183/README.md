# Frozen forward-combat compatibility artifact

`bot.wasm` is the byte-identical Arc Relay forward-combat artifact pinned by
`ArcRelayPlaylistDefinition.ForwardStockArtifactHash`:

`999183019785e9aac163ed607d43ed5fd6efa903264f216362e4f84711203b0f`

It is retained at this explicit compatibility path because the actively built
`stock-mind-v4/bot.wasm` later advanced while historical hosted playlist
definitions must remain executable without changing their canonical identity.
The archived bytes originate from repository commit `11c3309b`.
