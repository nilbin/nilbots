# Meshy Bulwark Smart T2 15k strict-top comparison

- Provider: Meshy
- Task: `019fb42a-e3ed-7f18-a54e-795edf685815`
- Status: succeeded
- Credits: 5 (`700` before, `695` after)
- Input: approved strict-top concept
- Purpose: preserve the game-critical planform while testing inferred depth

## Verdict

Leading Mobile geometry candidate; passed the planform/depth gate and is
pending deterministic semantic grouping and pivot validation. It is not yet
approved for texture or runtime.

Meshy treated the strict-top image as a vertical object face. A pure rigid
orientation correction restores the intended world axes without deforming the
mesh:

```text
M = Rz(180 degrees) @ Rx(-90 degrees)
[-1  0  0  0]
[ 0  0 -1  0]
[ 0 -1  0  0]
[ 0  0  0  1]
```

After correction, the result reaches `91.65%` top IoU, `95.74%` canonical
coverage, `95.54%` candidate containment, and `+0.21%` area delta. Its
height-to-planform ratio is `0.230882`, so the result has real modeled depth
rather than a zero-thickness plane.

The raw mesh contains 12,984 triangles and 122 welded connected components.
Component review separates the paired shield lobes, barrels, crown/reactor,
and pod clusters well enough to justify a local grouping/pivot proof. The
central carrier is still open/nonmanifold and the pods are multi-piece
clusters; provider components are not yet a production hierarchy.

Do not spend the 10-credit texture gate until the local rigid proof passes.

The ignored raw master is 244,492 bytes with SHA-256
`af4dc153e5a939b487fada55f26f1546a83658233dbac85529c6618eafbd80f6`.
The `audit/` directory contains corrected fixed views, the top overlay, and
component-color reviews.
