/**
 * Backfill the additive compact-broadcast vision column from its hash-matched
 * canonical replay. This is review-fixture migration only; new product
 * broadcasts are authored with the column by the engine.
 */
export function withCanonicalTeamVision(transport, replay) {
  if (transport.vision !== undefined || ![1, 2].includes(transport.broadcastVersion))
    return transport;
  const vision = replay.ticks.map((tick) => {
    const byTeam = new Map();
    for (const turn of tick.mindTurns ?? tick.actorTurns ?? []) {
      const teamId = turn.teamId ?? turn.actorId?.teamId;
      if (!Number.isInteger(teamId)) continue;
      let tiles = byTeam.get(teamId);
      if (tiles === undefined) {
        tiles = new Map();
        byTeam.set(teamId, tiles);
      }
      for (const tile of turn.observation?.visibleTiles ?? []) {
        const { x, y } = tile.position ?? {};
        if (Number.isInteger(x) && Number.isInteger(y))
          tiles.set(`${x},${y}`, [x, y]);
      }
    }
    return [...byTeam.entries()]
      .sort(([left], [right]) => left - right)
      .map(([teamId, tiles]) => [
        teamId,
        [...tiles.values()].sort(
          ([leftX, leftY], [rightX, rightY]) =>
            leftY - rightY || leftX - rightX,
        ),
      ]);
  });
  return { ...transport, vision };
}
