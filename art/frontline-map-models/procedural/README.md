# Deterministic procedural comparison

`proof.html` is a presentation-only comparison route for the provider pilot. It
is not imported by the web application.

The proof:

- reads `maps/experimental/frontline-01.json`;
- assigns perimeter versus cover with the same border/family rule as
  `WallLayout`;
- derives cardinal neighbours from the real tile rows;
- instances reusable rounded pods, connectors, caps, channels, and service
  panels;
- uses the existing Ember Forge floor and wall PBR helpers;
- uses the runtime renderer's 58-degree camera pitch; and
- never emits or stores a whole-map mesh.

Run after installing `web` dependencies:

```sh
node scripts/render-frontline-map-kit-proof.mjs
FRONTLINE_KIT_VIEW=lineup node scripts/render-frontline-map-kit-proof.mjs
```

The result is an intentionally rough geometry baseline. Its purpose is to
answer whether a parameterized, topology-driven kit can reach the approved
profile without provider inference, and to give Meshy modules a fair
same-camera comparison. It does not modify `arenaCamera`, `ArenaCanvas3D`, or
the shipping scene.

The current proof uses a hybrid that fits the proposed runtime contract:
continuous family substrates are traced from current map tiles at load time,
while caps, channels, and service panels are reusable instances selected from
family/topology data. The output is
`review/procedural/frontline-procedural-topology-proof.png`.

The lineup view isolates the first viability gate—one perimeter straight, one
rounded corner/end transition, one low cover segment, and the continuous floor
under the same camera—and writes
`review/procedural/frontline-procedural-kit-lineup-v1.png`.
