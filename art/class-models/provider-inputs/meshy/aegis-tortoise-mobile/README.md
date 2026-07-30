# Aegis Tortoise mobile — review crop pack

Status: **raw review complete; provider submission blocked**.

The approved V2 sheet supplies the selected four views. V1 is retained only
for the accepted short-leg/depth inference. The V2 strict top already passes
the rich Aegis 2D authority at 95.46% rigid silhouette IoU, so it must not be
reshaped toward the conflicting fallback SVG.

## Source and crop record

Both source boards are 1536×1024. All views preserve model-local `+X`
East/forward/right and `+Y` physical up.

| Variant/view | Crop H×W | Offset y,x | Output W×H | SHA-256 |
| --- | ---: | ---: | ---: | --- |
| V1 top | 610×700 | 20,60 | 700×610 | `f91873d3354ab1773635d94febda5fc5840b29f3317cac778f8bff37be5f3103` |
| V1 side | 320×700 | 650,35 | 700×320 | `0ddacbcdbcaeffdd10da56435accacaea256fce0370ce224d1696a44ab846603` |
| V1 front ¾ | 510×740 | 90,760 | 740×510 | `26b47886bfd1e5eaacfb8d7b15ecea2ff620bf210082f2d3a7830fe173a0c686` |
| V1 rear ¾ | 375×710 | 610,775 | 710×375 | `8ad76f8e089d2eb901fc1fdd8bf0642e20ce4bcdb65be2eed8be0fceada84f51` |
| V2 top | 610×700 | 20,60 | 700×610 | `b02dd61edd6ed936e8fe620f2e495c770491ead9b635921428195df00df3f59f` |
| V2 side | 320×700 | 650,35 | 700×320 | `c506e36fc0770f64b54247b9c33da5f62e328faa947032ffa5ad1f28c2fac667` |
| V2 front ¾ | 510×740 | 90,760 | 740×510 | `a07e3a7f7bc24ddf739ca24d2d3cffeb2b97952c1d6c45989378dbdc58d0329a` |
| V2 rear ¾ | 375×710 | 610,775 | 710×375 | `b6cebc28b29d5002bc8480d63286ad2cb5de457292164c51784fb2d24c9b3fdf` |

Source hashes:

- V1 approval sheet:
  `9661600ab01d3cdf0617fc757ac40be5a43affc2054c83339082045513767b91`
- V2 approval sheet:
  `d1acf3d6af2ed71c64146a843f7564c99ede0b9384b26c28515218f4f57c3be7`
- Rich top authority:
  `22e41a0925f1b88497ff55265bebb249b21da004c9a2559a5f1d0f70db95fbd5`
- Semantic team-region SVG:
  `0e4e5c3c9ea91feec349f7516d6f1de09ac674aac7f2243d6af8845b92fc45f9`

## Blockers

- Replace these native crops with consistent 1254×1254 flat `#b8b8b8`
  inputs, at least 1040 px source detail, and no floor/cast/contact shadow.
- Keep the two long forward shield slits and cyan core as authored class
  energy, not team color.
- Restore the runtime SVG's west-centre semantic team inlay and prove it with
  a deterministic mask; do not broaden team ownership to unrelated cyan.
- Re-run the close top overlay after the final flattened top is authored.

The rejected experimental shadow mask is not retained: silhouette-audit
achromatic flooding removed real neutral gun/leg hardware when reused as
alpha, so it was unsafe as a provider input.
