import type { TacticalSheetCatalog } from '../api';
import type { OrderNode, SheetDraft, SheetIssue } from './sheetAuthoring.ts';
import type { LinkedPlay, PlayGeometryGroup } from './workshopTypes.ts';

export interface ProgramSpatialGuide {
  id: string;
  sourceAnchorId: string;
  targetAnchorId: string;
  label: string;
  range?: number;
  distance: number;
  lineOfSight: 'clear' | 'blocked' | 'not-required';
  valid: boolean;
}

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
    if (slot.facingAnchorId && slot.facing && slot.facing !== 'toward-point')
      next.mapPlan.anchors[slot.facingAnchorId] = cardinalFacingPoint(anchor, slot.facing);
    const deploymentTarget = slot.deployment
      ? next.mapPlan.anchors[slot.deployment.targetAnchorId] : undefined;
    if (slot.deployment && deploymentTarget) {
      const [targetX, targetY] = deploymentTarget;
      next.mapPlan.zones[slot.deployment.targetZoneId] = {
        rect: [targetX, targetY, targetX, targetY],
      };
    }
  }
  // A recipe may own a readiness zone without calling it a required body
  // slot. Keep that ordinary zone attached to its semantic anchor too.
  for (const owned of play.ownedGeometry.filter((entry) =>
    entry.kind === 'zone' && entry.derivedFrom)) {
    if (play.slots.some((slot) => slot.readinessZoneId === owned.id)) continue;
    const anchor = next.mapPlan.anchors[owned.derivedFrom!];
    if (!anchor) continue;
    const tolerance = owned.tolerance ?? 0;
    next.mapPlan.zones[owned.id] = {
      rect: [
        anchor[0] - tolerance,
        anchor[1] - tolerance,
        anchor[0] + tolerance,
        anchor[1] + tolerance,
      ],
    };
  }
  for (const group of play.geometryGroups)
    synchronizeGroup(next, group);
  return next;
}

function cardinalFacingPoint(
  [x, y]: [number, number],
  facing: 'north' | 'east' | 'south' | 'west',
): [number, number] {
  if (facing === 'north') return [x, y - 1];
  if (facing === 'south') return [x, y + 1];
  if (facing === 'west') return [x - 1, y];
  return [x + 1, y];
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

export function movePlayGeometryObject(
  draft: SheetDraft,
  playId: string,
  objectId: string,
  dx: number,
  dy: number,
  width: number,
  height: number,
): SheetDraft {
  const next = structuredClone(draft);
  const play = requirePlay(next, playId);
  const object = play.ownedGeometry.find((entry) => entry.id === objectId);
  if (!object || object.derivedFrom)
    throw new Error(`Program object '${objectId}' is not directly movable.`);

  const points: Array<[number, number]> = object.kind === 'anchor'
    ? [next.mapPlan.anchors[objectId]]
    : object.kind === 'zone'
      ? (() => {
        const [x1, y1, x2, y2] = next.mapPlan.zones[objectId].rect;
        return [[x1, y1], [x2, y2]] as Array<[number, number]>;
      })()
      : next.mapPlan.routes[objectId].waypoints;
  if (points.length === 0) return next;
  const boundedDx = Math.max(-Math.min(...points.map(([x]) => x)),
    Math.min(dx, width - 1 - Math.max(...points.map(([x]) => x))));
  const boundedDy = Math.max(-Math.min(...points.map(([, y]) => y)),
    Math.min(dy, height - 1 - Math.max(...points.map(([, y]) => y))));

  if (object.kind === 'anchor') {
    const [x, y] = next.mapPlan.anchors[objectId];
    next.mapPlan.anchors[objectId] = [x + boundedDx, y + boundedDy];
  } else if (object.kind === 'zone') {
    const [x1, y1, x2, y2] = next.mapPlan.zones[objectId].rect;
    next.mapPlan.zones[objectId].rect = [
      x1 + boundedDx, y1 + boundedDy, x2 + boundedDx, y2 + boundedDy,
    ];
  } else {
    next.mapPlan.routes[objectId].waypoints = points
      .map(([x, y]) => [x + boundedDx, y + boundedDy]);
  }
  return synchronizePlayGeometry(next, playId);
}

export function playGeometryIssues(
  draft: SheetDraft,
  playId: string,
  tileRows?: string[],
  catalog?: TacticalSheetCatalog,
): SheetIssue[] {
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
  for (const group of play.readinessGroups ?? []) {
    if (group.maximumSpread === undefined) continue;
    const roleIds = new Set(group.projectionIds.map((projectionId) =>
      play.projections.find((projection) => projection.id === projectionId)?.roleId)
      .filter((id): id is string => Boolean(id)));
    const slots = play.slots.filter((slot) => slot.required && roleIds.has(slot.roleId));
    for (let left = 0; left < slots.length; left++) for (let right = left + 1; right < slots.length; right++) {
      const a = draft.mapPlan.anchors[slots[left].anchorId];
      const b = draft.mapPlan.anchors[slots[right].anchorId];
      if (!a || !b) continue;
      const furthest = Math.max(Math.abs(a[0] - b[0]), Math.abs(a[1] - b[1]))
        + slots[left].tolerance + slots[right].tolerance;
      if (furthest > group.maximumSpread) issues.push({
        path: `workshop.plays.${playId}.readinessGroups.${group.id}`,
        severity: 'error',
        message: `${group.name} can spread ${furthest} tiles across its Post envelopes, above the authored maximum ${group.maximumSpread}. Move the Posts, reduce tolerance, or increase the maximum spread.`,
      });
    }
  }
  for (const group of play.geometryGroups) {
    for (const link of group.tossLinks ?? []) {
      const route = draft.mapPlan.routes[link.routeId]?.waypoints;
      const source = route?.[link.sourceWaypointIndex];
      const receiver = draft.mapPlan.anchors[link.receiverAnchorId];
      if (!source || !receiver) continue;
      const distance = Math.max(
        Math.abs(source[0] - receiver[0]),
        Math.abs(source[1] - receiver[1]),
      );
      if (distance < 1 || distance > 5) {
        issues.push({
          path: `workshop.plays.${playId}.geometry.${link.receiverAnchorId}`,
          severity: 'error',
          message: `${link.receiverAnchorId} is ${distance} tiles from its Toss point, outside Arc Toss range.`,
        });
        continue;
      }
      if (!isStraight(source, receiver)) {
        issues.push({
          path: `workshop.plays.${playId}.geometry.${link.receiverAnchorId}`,
          severity: 'error',
          message: `${link.receiverAnchorId} is not on a horizontal, vertical, or 45° line from its Toss point.`,
        });
        continue;
      }
      const wall = tileRows && firstArcTossWallBetween(source, receiver, tileRows);
      if (wall) issues.push({
        path: `workshop.plays.${playId}.geometry.${link.receiverAnchorId}`,
        severity: 'error',
        message: `${link.receiverAnchorId} is blocked by terrain at ${wall[0]},${wall[1]}; Arc Toss would land before its receiver.`,
      });
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
      if (distance > 5) {
        issues.push({
          path: `workshop.plays.${playId}.geometry.${rightId}`,
          severity: 'error',
          message: `${leftId} → ${rightId} is ${distance} tiles apart, outside Arc Toss range.`,
        });
        continue;
      }
      if (!isStraight(left, right)) {
        issues.push({
          path: `workshop.plays.${playId}.geometry.${rightId}`,
          severity: 'error',
          message: `${leftId} → ${rightId} is not on a horizontal, vertical, or 45° Arc Toss line.`,
        });
        continue;
      }
      const wall = tileRows && firstArcTossWallBetween(left, right, tileRows);
      if (wall) issues.push({
        path: `workshop.plays.${playId}.geometry.${rightId}`,
        severity: 'error',
        message: `${leftId} → ${rightId} is blocked by terrain at ${wall[0]},${wall[1]}; Arc Toss would land before its receiver.`,
      });
    }
  }
  if (catalog) for (const guide of playSpatialGuides(draft, playId, catalog)) {
    if (guide.valid) continue;
    issues.push({
      path: `workshop.plays.${playId}.geometry.${guide.targetAnchorId}`,
      severity: 'error',
      message: guide.range !== undefined && guide.distance > guide.range
        ? `${guide.label} is ${guide.distance} tiles from its caster Post, outside range ${guide.range}. Move either point on the map.`
        : `${guide.label} has blocked line of sight. Move its caster Post or target point.`,
    });
  }
  return issues;
}

/** Static visual guidance only. Runtime legality remains authoritative. */
export function playSpatialGuides(
  draft: SheetDraft,
  playId: string,
  catalog: TacticalSheetCatalog,
): ProgramSpatialGuide[] {
  const play = requirePlay(draft, playId);
  const guides: ProgramSpatialGuide[] = [];
  for (const slot of play.slots) {
    const deployment = slot.deployment;
    if (!deployment) continue;
    const source = draft.mapPlan.anchors[slot.anchorId];
    const target = draft.mapPlan.anchors[deployment.targetAnchorId];
    if (!source || !target) continue;
    const role = draft.orders.roles.find((entry) => entry.id === slot.roleId);
    const descriptor = role?.classes.map((classId) => catalog.classes.find((entry) =>
      entry.id === classId && entry.signatureId === deployment.skill))
      .find(Boolean)?.signatureAuthoring;
    const range = descriptor?.engagementRange;
    const distance = chebyshev(source, target);
    const wall = firstWallBetween(source, target, catalog.map.tileRows);
    const lineOfSight = wall ? 'blocked' : 'clear';
    guides.push({
      id: `${slot.id}:${deployment.skill}`,
      sourceAnchorId: slot.anchorId,
      targetAnchorId: deployment.targetAnchorId,
      label: `${humanize(deployment.skill)} target`,
      range,
      distance,
      lineOfSight,
      valid: (range === undefined || distance <= range) && !wall,
    });
  }
  const existingTargets = new Set(guides.map((guide) => guide.targetAnchorId));
  for (const projection of play.projections) {
    const role = draft.orders.roles.find((entry) => entry.id === projection.roleId);
    for (const binding of projection.signatureOpportunities ?? []) {
      const action = findOrder(projection.orders, binding.useOrderId);
      if (!action?.use?.at || existingTargets.has(action.use.at)) continue;
      const host = findOrder(projection.orders, binding.hostOrderId);
      const post = play.slots.find((entry) => entry.hostOrderId === binding.hostOrderId);
      const sourceAnchorId = post?.anchorId
        ?? (host?.move?.to && typeof host.move.to === 'object' && 'anchor' in host.move.to
          ? host.move.to.anchor : undefined);
      if (!sourceAnchorId) continue;
      const source = draft.mapPlan.anchors[sourceAnchorId];
      const target = draft.mapPlan.anchors[action.use.at];
      if (!source || !target) continue;
      const descriptor = role?.classes.map((classId) => catalog.classes.find((entry) =>
        entry.id === classId && entry.signatureId === action.use!.skill))
        .find(Boolean)?.signatureAuthoring;
      const range = descriptor?.engagementRange;
      const distance = chebyshev(source, target);
      const wall = firstWallBetween(source, target, catalog.map.tileRows);
      guides.push({
        id: `${binding.id}:${action.use.skill}`,
        sourceAnchorId,
        targetAnchorId: action.use.at,
        label: `${humanize(action.use.skill)} target`,
        range,
        distance,
        lineOfSight: wall ? 'blocked' : 'clear',
        valid: (range === undefined || distance <= range) && !wall,
      });
    }
  }
  return guides;
}

function findOrder(orders: OrderNode[], id: string): OrderNode | undefined {
  for (const order of orders) {
    if (order.id === id) return order;
    const nested = order.orders ? findOrder(order.orders, id) : undefined;
    if (nested) return nested;
  }
  return undefined;
}

function isStraight(from: [number, number], to: [number, number]) {
  const dx = Math.abs(to[0] - from[0]);
  const dy = Math.abs(to[1] - from[1]);
  return dx === 0 || dy === 0 || dx === dy;
}

function firstWallBetween(
  from: [number, number],
  to: [number, number],
  tileRows: string[],
): [number, number] | null {
  const width = Math.abs(to[0] - from[0]);
  const height = Math.abs(to[1] - from[1]);
  const dx = Math.sign(to[0] - from[0]);
  const dy = Math.sign(to[1] - from[1]);
  let x = from[0];
  let y = from[1];
  let horizontal = 0;
  let vertical = 0;
  while (horizontal < width || vertical < height) {
    const decision = (1 + 2 * horizontal) * height
      - (1 + 2 * vertical) * width;
    if (decision === 0) {
      // Corner-strict supercover: a diagonal ray touches both orthogonal
      // companions before entering the diagonal tile.
      if (tileRows[y]?.[x + dx] === '#') return [x + dx, y];
      if (tileRows[y + dy]?.[x] === '#') return [x, y + dy];
      x += dx;
      y += dy;
      horizontal += 1;
      vertical += 1;
    } else if (decision < 0) {
      x += dx;
      horizontal += 1;
    } else {
      y += dy;
      vertical += 1;
    }
    if (tileRows[y]?.[x] === '#') return [x, y];
  }
  return null;
}

/** Arc Toss follows its authored cardinal/45° ray and clips only when the
 * landing ray itself enters a wall. It deliberately does not use the combat
 * line-of-sight supercover rule. */
function firstArcTossWallBetween(
  from: [number, number],
  to: [number, number],
  tileRows: string[],
): [number, number] | null {
  const dx = Math.sign(to[0] - from[0]);
  const dy = Math.sign(to[1] - from[1]);
  const distance = chebyshev(from, to);
  let x = from[0];
  let y = from[1];
  for (let step = 0; step < distance; step++) {
    x += dx;
    y += dy;
    if (tileRows[y]?.[x] === '#') return [x, y];
  }
  return null;
}

function chebyshev(from: [number, number], to: [number, number]) {
  return Math.max(Math.abs(from[0] - to[0]), Math.abs(from[1] - to[1]));
}

function humanize(value: string) {
  return value.replaceAll('-', ' ');
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
