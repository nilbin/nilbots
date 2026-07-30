# Lattice Loom — review crop pack

Status: **clean V1 provider input ready**.

V2 supplies the selected low-hover geometry. V1 remains the rejected grounded
alternative, and V3 remains a semantic-accent review only; they must not be
mixed into one provider upload. The V2 outer planform already passes the rich
Lattice 2D authority at 94.44% rigid IoU.

## Source and crop record

All three source boards are 1536×1024. All views preserve model-local `+X`
East/forward/right and `+Y` physical up.

| Variant/view | Crop H×W | Offset y,x | Output W×H | SHA-256 |
| --- | ---: | ---: | ---: | --- |
| V1 top | 660×745 | 20,35 | 745×660 | `90a43e678bf3578e03284251bce833f8d7abb2c8077fb9f164a98501c68ae758` |
| V1 side | 260×770 | 690,30 | 770×260 | `812b917ac4fbed2f1eb06d30ca8a8850eebace211365c6a877119e1abd7b3081` |
| V1 front ¾ | 545×756 | 95,780 | 756×545 | `d94f74bd70334dc7ade46ff5ad3e1526efa51ab9ac94ef401ed4f7c6705b51a2` |
| V1 rear ¾ | 310×680 | 650,815 | 680×310 | `d3e7566595f9a370d2f91b7ddec8e2a31882aad0c12016b8d14392b3947340ff` |
| V2 top | 660×745 | 20,35 | 745×660 | `dd621b4313917a38bb920bcb22b79aa7c68728d4c91db29fb1b3e12195df1412` |
| V2 side | 260×770 | 690,30 | 770×260 | `e65853d39e0556a907e71f12659fc3b6604fbf524ff2295ed96ba3b43521e4b5` |
| V2 front ¾ | 545×756 | 95,780 | 756×545 | `6a058ae5ec2e75d8361afa945a6f3d81f200f3b97a50de2079102c978d82a94d` |
| V2 rear ¾ | 310×680 | 650,815 | 680×310 | `0cd1a8d2500014df3b5ab41eab3f89e258150a8d3c66b7b4b9656b2b27a64398` |
| V3 top | 660×745 | 20,35 | 745×660 | `14e44cfffe465a162ca5c80575e540055be501b9685b5b04540ae85c772ba756` |
| V3 side | 260×770 | 690,30 | 770×260 | `33b24288f820ea62000b19de93bcbe0e00340741b5389fe38e4d5f48d657b1e8` |
| V3 front ¾ | 545×756 | 95,780 | 756×545 | `c94f27165953a7252ff45e768b680d437bda05a32ff799c84a7feac157272d47` |
| V3 rear ¾ | 310×680 | 650,815 | 680×310 | `ba951318177762fadaa27570093ebbbc318f62815f06011e0fc1b474bb1f0186` |

Source hashes:

- V1 approval sheet:
  `5acf3673643e9caddf5ffb66f23f1406eb29b321dc654a06eb9479009ab58d4d`
- V2 approval sheet:
  `c208e69e8099e3b88f521a3af02471fada686161300215553d210244c25d9036`
- V3 approval sheet:
  `29d32230975d728a0ae72cc29540362c3aa7ddbdf8760f9964371c271bdc3db2`
- Rich top authority:
  `cd1d47fa5cbcaf488aa0c1ee06f7f7bf03767577daa216110d0c3bd9088a8ab5`
- Semantic team-region SVG:
  `f45a956fd2df4e726f889557e863a183ddaf74fbb8c5b763d07bdde22d8a6a61`

## Clean provider pack

`clean-v1` contains four 1254×1254 shadowless low-hover views on exact flat
`#b8b8b8`. The clean top passed the rich 2D authority at 90.96% full
silhouette IoU and 96.36% outer IoU. Exactly four cyan shoulder/fork-root
inlays remain; the unsupported north/south strips are gone.

The provider result remains a static reconstruction master. The Blender pass
must re-square/extend the east fork tips by roughly 3%, align the internal
diagonal truss cells, preserve all negative spaces, and split the four team
surfaces. The core, coils, fork-tip lights, and hover emitters remain authored
amber class energy.

The raw V1/V2/V3 crop packs remain immutable provenance. The rejected
experimental shadow mask is not retained: the silhouette audit's
global achromatic rule erased real pale ceramic and neutral truss hardware
when reused as alpha.
