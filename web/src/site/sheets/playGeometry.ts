import type { SheetDraft, SheetIssue } from './sheetAuthoring';
import type { LinkedPlay, PlayGeometryGroup } from './workshopTypes';

export function synchronizePlayGeometry(
  draft: SheetDraft,
  playId: string,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  for (const slot of play.slots) {
    const anchor = next.mapPlan.anchors[slot.anchorId];
    if (!anchor) continue;
    const [x, y] = anchor;
    next.mapPlan.zones[slot.readinessZoneId] = {
      rect: [x - slot.tolerance, y - slot.tolerance, x + slot.tolerance, y + slot.tolerance],
    };
    const owned = play.ownedGeometry.find((entry) =>
      entry.kind === 'zone' && entry.id === slot.readinessZoneId);
    if (owned) owned.tolerance = slot.tolerance;
  }
  for (const group of play.geometryGroups)
    synchronizeGroup(next, group);
  return next;
}

export function setPlaySlotTolerance(
  draft: SheetDraft,
  playId: string,
  slotId: string,
  tolerance: number,
): SheetDraft {
  const next = structuredClone(draft);
  const slot = requirePlay(next, playId).slots.find((entry) => entry.id === slotId);
  if (!slot) throw new Error(`Unknown Play slot '${slotId}'.`);
  slot.tolerance = Math.max(0, Math.min(3, Math.trunc(tolerance)));
  return synchronizePlayGeometry(next, playId);
}

export function movePlayGeometryGroup(
  draft: SheetDraft,
  playId: string,
  groupId: string,
  dx: number,
  dy: number,
  width: number,
  height: number,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const group = play.geometryGroups.find((entry) => entry.id === groupId);
  if (!group) throw new Error(`Unknown geometry group '${groupId}'.`);
  const points = groupPoints(next, group);
  if (points.length === 0) return next;
  const boundedDx = Math.max(-Math.min(...points.map(([x]) => x)),
    Math.min(dx, width - 1 - Math.max(...points.map(([x]) => x))));
  const boundedDy = Math.max(-Math.min(...points.map(([, y]) => y)),
    Math.min(dy, height - 1 - Math.max(...points.map(([, y]) => y))));

  group.waypoints = group.waypoints?.map(([x, y]) => [x + boundedDx, y + boundedDy]);
  for (const id of group.objectIds) {
    if (next.mapPlan.anchors[id]) {
      const [x, y] = next.mapPlan.anchors[id];
      next.mapPlan.anchors[id] = [x + boundedDx, y + boundedDy];
    } else if (next.mapPlan.zones[id]
      && !play.slots.some((slot) => slot.readinessZoneId === id)) {
      const [x1, y1, x2, y2] = next.mapPlan.zones[id].rect;
      next.mapPlan.zones[id].rect = [
        x1 + boundedDx, y1 + boundedDy, x2 + boundedDx, y2 + boundedDy,
      ];
    } else if (next.mapPlan.routes[id]
      && !group.derivedObjectIds.includes(id)) {
      next.mapPlan.routes[id].waypoints = next.mapPlan.routes[id].waypoints
        .map(([x, y]) => [x + boundedDx, y + boundedDy]);
    }
  }
  return synchronizePlayGeometry(next, playId);
}

export function playGeometryIssues(draft: SheetDraft, playId: string): SheetIssue[] {
  const play = requirePlay(draft, playId);
  const issues: SheetIssue[] = [];
  const requiredByRole = new Map<string, number>();
  for (const slot of play.slots) {
    if (slot.required)
      requiredByRole.set(slot.roleId, (requiredByRole.get(slot.roleId) ?? 0) + 1);
  }
  for (const [roleId, required] of requiredByRole) {
    const available = draft.orders.roles.find((role) => role.id === roleId)?.classes.length ?? 0;
    if (required > available) issues.push({
      path: `workshop.plays.${playId}.slots`,
      severity: 'error',
      message: `${roleId} has ${available} ${available === 1 ? 'body' : 'bodies'} for ${required} required positions. Split or expand the role, or remove a required position.`,
    });
  }
  for (let left = 0; left < play.slots.length; left++) {
    const a = play.slots[left];
    if (!a.required) continue;
    for (let right = left + 1; right < play.slots.length; right++) {
      const b = play.slots[right];
      if (!b.required || a.roleId !== b.roleId) continue;
      const aZone = draft.mapPlan.zones[a.readinessZoneId]?.rect;
      const bZone = draft.mapPlan.zones[b.readinessZoneId]?.rect;
      if (aZone && bZone && overlaps(aZone, bZone)) {
        issues.push({
          path: `workshop.plays.${playId}.slots.${b.id}`,
          severity: 'error',
          message: `${a.id} and ${b.id} overlap. One ${a.roleId} body could falsely fill both required positions.`,
        });
      }
    }
  }
  for (const group of play.geometryGroups.filter((entry) => entry.kind === 'relay-corridor')) {
    const stationIndexes = group.stationWaypointIndexes ?? {};
    const stations = Object.entries(stationIndexes).sort(([, left], [, right]) => left - right);
    for (let index = 0; index < stations.length - 1; index++) {
      const [leftId] = stations[index];
      const [rightId] = stations[index + 1];
      const left = draft.mapPlan.anchors[leftId];
      const right = draft.mapPlan.anchors[rightId];
      if (!left || !right) continue;
      const distance = Math.max(Math.abs(left[0] - right[0]), Math.abs(left[1] - right[1]));
      if (distance > 5) issues.push({
        path: `workshop.plays.${playId}.geometry.${rightId}`,
        severity: 'error',
        message: `${leftId} → ${rightId} is ${distance} tiles apart, outside Arc Toss range.`,
      });
    }
  }
  return issues;
}

function synchronizeGroup(draft: SheetDraft, group: PlayGeometryGroup) {
  if (group.kind !== 'relay-corridor' || !group.waypoints) return;
  for (const [anchorId, index] of Object.entries(group.stationWaypointIndexes ?? {})) {
    const anchor = draft.mapPlan.anchors[anchorId];
    if (anchor && index >= 0 && index < group.waypoints.length)
      group.waypoints[index] = [...anchor];
  }
  for (const slice of group.routeSlices ?? []) {
    const from = Math.min(slice.fromWaypointIndex, slice.toWaypointIndex);
    const to = Math.max(slice.fromWaypointIndex, slice.toWaypointIndex);
    const points = group.waypoints.slice(from, to + 1);
    if (slice.fromWaypointIndex > slice.toWaypointIndex) points.reverse();
    if (points.length >= 2) draft.mapPlan.routes[slice.routeId] = { waypoints: points };
  }
}

function groupPoints(draft: SheetDraft, group: PlayGeometryGroup): Array<[number, number]> {
  const points = [...(group.waypoints ?? [])];
  for (const id of group.objectIds) {
    const anchor = draft.mapPlan.anchors[id];
    if (anchor) points.push(anchor);
    const zone = draft.mapPlan.zones[id];
    if (zone) {
      const [x1, y1, x2, y2] = zone.rect;
      points.push([x1, y1], [x2, y2]);
    }
    const route = draft.mapPlan.routes[id];
    if (route) points.push(...route.waypoints);
  }
  return points;
}

function requirePlay(draft: SheetDraft, playId: string): LinkedPlay {
  const play = draft.workshop.plays.find((entry) => entry.id === playId);
  if (!play) throw new Error(`Unknown Play '${playId}'.`);
  return play;
}

function overlaps(
  a: [number, number, number, number],
  b: [number, number, number, number],
) {
  return a[0] <= b[2] && b[0] <= a[2] && a[1] <= b[3] && b[1] <= a[3];
}
