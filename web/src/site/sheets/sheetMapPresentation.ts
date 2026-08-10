export type MapViewSide = 'west' | 'east';
export type MapTeamRelation = 'own' | 'enemy' | 'neutral';
export type MapLandmarkKind =
  | 'well'
  | 'heal'
  | 'reactor'
  | 'delivery'
  | 'objective';

export interface MapRegionPresentation {
  kind: MapLandmarkKind;
  label: string;
  symbol: string;
  physicalSide: MapViewSide | null;
}

interface MapPointLike {
  x: number;
  y: number;
}

interface MapSpawnLike {
  id: string;
  position: MapPointLike;
}

const REGION_PRESENTATION: Record<string, Omit<MapRegionPresentation, 'physicalSide'>> = {
  'well-north': { kind: 'well', label: 'North Well', symbol: 'N' },
  'well-centre': { kind: 'well', label: 'Centre Well', symbol: 'C' },
  'well-south': { kind: 'well', label: 'South Well', symbol: 'S' },
  'heal-north': { kind: 'heal', label: 'North healing tile', symbol: '+' },
  'heal-south': { kind: 'heal', label: 'South healing tile', symbol: '+' },
};

/** Translate stable map-region IDs into the player's map language. The map
 * export remains the geometry authority; this adds no editor-only tiles. */
export function mapRegionPresentation(id: string): MapRegionPresentation {
  const known = REGION_PRESENTATION[id];
  if (known) return { ...known, physicalSide: null };

  const physicalSide = id.endsWith('-west')
    ? 'west'
    : id.endsWith('-east')
      ? 'east'
      : null;
  if (id.startsWith('reactor-')) {
    return {
      kind: 'reactor',
      label: `${physicalSide === 'west' ? 'West' : physicalSide === 'east' ? 'East' : ''} reactor`.trim(),
      symbol: 'R',
      physicalSide,
    };
  }
  if (id.startsWith('home-')) {
    return {
      kind: 'delivery',
      label: `${physicalSide === 'west' ? 'West' : physicalSide === 'east' ? 'East' : ''} delivery zone`.trim(),
      symbol: '↓',
      physicalSide,
    };
  }
  return {
    kind: 'objective',
    label: id.replaceAll('-', ' '),
    symbol: '·',
    physicalSide,
  };
}

export function relationForPhysicalSide(
  physicalSide: MapViewSide | null,
  viewSide: MapViewSide,
): MapTeamRelation {
  if (physicalSide === null) return 'neutral';
  return physicalSide === viewSide ? 'own' : 'enemy';
}

export function spawnTeam(id: string): 0 | 1 | null {
  if (id.startsWith('team-0-')) return 0;
  if (id.startsWith('team-1-')) return 1;
  return null;
}

export function relationForSpawn(
  id: string,
  viewSide: MapViewSide,
): MapTeamRelation {
  const team = spawnTeam(id);
  if (team === null) return 'neutral';
  const ownTeam = viewSide === 'west' ? 0 : 1;
  return team === ownTeam ? 'own' : 'enemy';
}

/** Spawn-protected pads arrive as one tag spanning both teams. Associate each
 * tagged tile with the nearest authoritative spawn cluster; ties stay neutral. */
export function nearestSpawnTeam(
  point: MapPointLike,
  spawns: readonly MapSpawnLike[],
): 0 | 1 | null {
  let bestDistance = Number.POSITIVE_INFINITY;
  let bestTeam: 0 | 1 | null = null;
  let tiedAcrossTeams = false;
  for (const spawn of spawns) {
    const team = spawnTeam(spawn.id);
    if (team === null) continue;
    const distance = Math.abs(point.x - spawn.position.x)
      + Math.abs(point.y - spawn.position.y);
    if (distance < bestDistance) {
      bestDistance = distance;
      bestTeam = team;
      tiedAcrossTeams = false;
    } else if (distance === bestDistance && team !== bestTeam) {
      tiedAcrossTeams = true;
    }
  }
  return tiedAcrossTeams ? null : bestTeam;
}

export function relationForTeam(
  team: 0 | 1 | null,
  viewSide: MapViewSide,
): MapTeamRelation {
  if (team === null) return 'neutral';
  return team === (viewSide === 'west' ? 0 : 1) ? 'own' : 'enemy';
}
