# Meshy Aegis Tortoise kinematic texture donor

- Provider: Meshy
- Endpoint: `POST /openapi/v1/retexture`
- Task: `019fb4bc-03d1-7142-9a37-7c3a8e2ffc34`
- Look: `aegis-tortoise-kinematic`
- Status: `SUCCEEDED` at 100%
- Submitted: `2026-07-30T20:34:08.134Z`
- Finished: `2026-07-30T20:36:16.736Z`
- Credits: 10 (`655` before, `645` after)
- Request fingerprint:
  `5fcea80195e64d60aa3e0b40f130b7deab177beb8c5bf92688385338043424ba`

The recorded input was
`sandbox/bulwark-kinematic-master-v11/aegis-tortoise-texture-upload.glb`:
1,466,812 bytes, GLB 2, SHA-256
`f96a4cb61312e10f7f86ac73e092a3e0f341972c1d1bb8d97de0e2d31903b175`.
It was submitted as a local GLB data URI. The accepted v13 kinematic master's
texture-upload GLB is byte-identical, so this donor remains applicable.

## Exact request

```text
Stylized matte top-down game asset. Bulwark fortress palette: 70% near-black graphite, 25% cold mid-gunmetal, at most 5% pale ash-gray insets. Texture mobile chassis, four shield plates, crown, eight tower vanes, barrels and emitter bars consistently. Crisp broad seams, powder-coated armor, high roughness, low metalness, flat readable albedo. Only reactor and weapon apertures glow neutral white. No bronze Striker or pale Fabricator palette, white hull, chrome, glossy clearcoat, photoreal wear, tiny noise, dirt, text, logos, decals, blue/red team colors, or baked lighting. Preserve geometry.
```

Settings:

- `ai_model: meshy-6`
- `enable_original_uv: true`
- `enable_pbr: true`
- `texture_resolution: 2k`
- `remove_lighting: true`
- `target_formats: [glb]`

The credential came from macOS Keychain service `nilbots.meshy.api`; no secret
value is recorded here.

## Durable outputs

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `provider-preview.png` | 121,095 | `067ea2e51428952036b167811e7a8c6666fd03a9e3aa116ae7547395afcbfc49` |
| `raw-retextured-model.glb` | 8,680,372 | `d9c332494cb82ba679fc944ed6e1dd85c8993a80bca9cde112ffe113ff65d4a9` |
| `texture-0-base-color.png` | 5,185,541 | `7e905cc00d76bed6fab6548b9b4d801813123fc2e8750b28614450603ca7022b` |
| `texture-0-emission.png` | 111,014 | `99bfa16d3a9233301bfca5d66af7f2fcf6cea1e2fecd6e42b23036ef92e16f2b` |
| `texture-0-metallic.png` | 896,066 | `3e37d78d2af7c770c848340f67518ba3d28656d6d239096c036535e112d70e7d` |
| `texture-0-normal.png` | 6,126,114 | `0bf2fc72d2c5eb248a4258b798f03c52b0768688e00636c7cf0e303a33458dae` |
| `texture-0-roughness.png` | 951,775 | `dc91173f5bf29e9b72b6aa3356c6f584b2d2e8a13e4366fdf283384e6968635e` |

## Verdict

This run is the accepted 2K PBR texture donor, not a runtime model. The
provider output must not replace deterministic geometry, pivots, animation
clips, or the renderer-owned `NB_TeamAccent_Semantic` material. Provider
materials on `TeamAccent.*` meshes are discarded during transplantation.

The raw GLB and lossless maps are retained locally because provider download
URLs expire. `request-intent.json`, `request-record.json`, and
`task.sanitized.json` are the exact sanitized request, accounting, and task
ledgers.
