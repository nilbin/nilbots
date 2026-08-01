import type {
  ReplayActorIdentity,
  ReplayArcCoreId,
  ReplayArcRelayModeState,
  ReplayModel,
  ReplayPosition,
  ReplayStableUnitKey,
  ReplayTick,
} from '../replayModel';
import type { BotPose } from './interpolate';

export interface ArcRelayVisualContext {
  ctx: CanvasRenderingContext2D;
  replay: ReplayModel;
  tick: ReplayTick | undefined;
  time: number;
  fraction: number;
  tile: number;
  mapWidth: number;
  mapHeight: number;
  px: (x: number) => number;
  py: (y: number) => number;
  poses: readonly BotPose[];
  accentFor: (unitKey: ReplayStableUnitKey | null) => string;
}

type ArcState = ReplayArcRelayModeState;

export function drawArcRelayGround(input: ArcRelayVisualContext): void {
  const state = currentState(input);
  if (!state) return;

  drawPulse(input, state);
  drawWells(input, state);
  drawReactors(input, state);
  drawSignatures(input, state);
  drawCores(input, state, false);
  drawHandoffCommitments(input);
}

export function drawArcRelayOverlay(input: ArcRelayVisualContext): void {
  const state = currentState(input);
  if (!state) return;

  drawCores(input, state, true);
  drawTransferEffects(input);
  drawSignatureReadiness(input, state);
}

function currentState(input: ArcRelayVisualContext): ArcState | null {
  const mode = input.tick?.after.mode ?? input.replay.initialWorld?.mode;
  return mode?.kind === 'arc-relay' && 'wells' in mode
    ? (mode as ArcState)
    : null;
}

function centre(
  input: ArcRelayVisualContext,
  position: ReplayPosition,
): { x: number; y: number } {
  return {
    x: input.px(position.x) + input.tile / 2,
    y: input.py(position.y) + input.tile / 2,
  };
}

function actorCentre(
  input: ArcRelayVisualContext,
  actor: ReplayActorIdentity,
): { x: number; y: number } | null {
  const pose = input.poses.find((candidate) => candidate.actorKey === actor.actorKey);
  return pose ? centre(input, { x: pose.x, y: pose.y }) : null;
}

function teamAccent(input: ArcRelayVisualContext, teamId: number): string {
  return input.accentFor(
    input.replay.units.find((unit) => unit.teamId === teamId)?.unitKey ?? null,
  );
}

function coreKey(coreId: ReplayArcCoreId): string {
  return `${coreId.sourceWellId}:${coreId.sourceOrdinal}`;
}

function drawWells(input: ArcRelayVisualContext, state: ArcState): void {
  const { ctx, tile, time } = input;
  const rules =
    input.replay.contract.kind === 'v3-generic' &&
    input.replay.contract.rawContract.rules.gameMode.kind === 'arc-relay'
      ? input.replay.contract.rawContract.rules.gameMode
      : null;
  for (const well of state.wells) {
    const point = centre(input, well.position);
    const radius = tile * 0.37;
    const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.25);
    ctx.save();
    ctx.fillStyle = 'rgba(7, 12, 18, 0.74)';
    ctx.strokeStyle = 'rgba(226, 240, 249, 0.5)';
    ctx.lineWidth = Math.max(1.5, tile * 0.035);
    ctx.beginPath();
    ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    const schedule = rules?.wells.find((entry) => entry.wellId === well.wellId);
    if (well.nextScheduledBirthTick !== null && schedule) {
      const remaining = Math.max(0, well.nextScheduledBirthTick - time);
      const progress = Math.max(0, Math.min(1, 1 - remaining / schedule.cadenceTicks));
      ctx.strokeStyle = 'rgba(237, 245, 251, 0.86)';
      ctx.lineWidth = Math.max(2, tile * 0.06);
      ctx.beginPath();
      ctx.arc(
        point.x,
        point.y,
        radius + tile * 0.08,
        -Math.PI / 2,
        -Math.PI / 2 + Math.PI * 2 * progress,
      );
      ctx.stroke();
    }

    if (well.rearmCompletesAtTick !== null) {
      const duration = Math.max(1, rules?.pendingRearmTicks ?? 10);
      const progress = Math.max(
        0,
        Math.min(1, 1 - (well.rearmCompletesAtTick - time) / duration),
      );
      ctx.strokeStyle = 'rgba(246, 183, 60, 0.9)';
      ctx.lineWidth = Math.max(2, tile * 0.055);
      ctx.beginPath();
      ctx.arc(
        point.x,
        point.y,
        radius - tile * 0.07,
        -Math.PI / 2,
        -Math.PI / 2 + Math.PI * 2 * progress,
      );
      ctx.stroke();
    }

    drawSourceGlyph(
      ctx,
      point.x,
      point.y,
      tile * 0.15,
      well.wellId,
      well.outstandingCoreId ? 'rgba(148, 163, 184, 0.55)' : '#f3f8fb',
      false,
    );
    if (well.pendingCharge) {
      ctx.fillStyle = `rgba(246, 183, 60, ${0.72 + pulse * 0.28})`;
      ctx.beginPath();
      ctx.arc(
        point.x + radius * 0.72,
        point.y - radius * 0.72,
        Math.max(2.5, tile * 0.065),
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }
    ctx.restore();
  }
}

function drawReactors(input: ArcRelayVisualContext, state: ArcState): void {
  const { ctx, tile } = input;
  for (const reactor of state.reactors) {
    const point = centre(input, reactor.position);
    const accent = teamAccent(input, reactor.teamId);
    const radius = tile * 0.43;
    ctx.save();
    ctx.fillStyle = 'rgba(6, 11, 17, 0.82)';
    ctx.strokeStyle = 'rgba(226, 235, 242, 0.36)';
    ctx.lineWidth = Math.max(1.5, tile * 0.035);
    ctx.beginPath();
    ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();

    for (let index = 0; index < 3; index++) {
      const start = -Math.PI / 2 + index * (Math.PI * 2 / 3) + 0.09;
      const end = start + Math.PI * 2 / 3 - 0.18;
      ctx.strokeStyle =
        index < reactor.integritySegments
          ? accent
          : 'rgba(100, 116, 139, 0.28)';
      ctx.lineWidth = Math.max(3, tile * 0.09);
      ctx.beginPath();
      ctx.arc(point.x, point.y, radius + tile * 0.07, start, end);
      ctx.stroke();
    }

    const gap = tile * 0.17;
    for (let index = 0; index < 3; index++) {
      ctx.fillStyle =
        index < reactor.chargePips
          ? accent
          : 'rgba(100, 116, 139, 0.32)';
      ctx.beginPath();
      ctx.arc(
        point.x + (index - 1) * gap,
        point.y,
        Math.max(2.5, tile * 0.065),
        0,
        Math.PI * 2,
      );
      ctx.fill();
    }
    ctx.restore();
  }
}

function drawPulse(input: ArcRelayVisualContext, state: ArcState): void {
  if (state.latestPulseTick !== input.tick?.tick || state.latestPulseTeamId === null)
    return;
  const { ctx, fraction, tile, mapWidth, mapHeight, px, py } = input;
  const accent = teamAccent(input, state.latestPulseTeamId);
  const fromWest =
    state.reactors.find((reactor) => reactor.teamId === state.latestPulseTeamId)
      ?.position.x === Math.min(...state.reactors.map((reactor) => reactor.position.x));
  const progress = fromWest ? fraction : 1 - fraction;
  const x = px(0) + mapWidth * tile * progress;
  const halfWidth = tile * 1.5;
  const gradient = ctx.createLinearGradient(x - halfWidth, 0, x + halfWidth, 0);
  gradient.addColorStop(0, withAlpha(accent, 0));
  gradient.addColorStop(0.5, withAlpha(accent, 0.38 * Math.sin(fraction * Math.PI)));
  gradient.addColorStop(1, withAlpha(accent, 0));
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  ctx.fillStyle = gradient;
  ctx.fillRect(x - halfWidth, py(0), halfWidth * 2, mapHeight * tile);
  ctx.strokeStyle = withAlpha(accent, 0.78 * Math.sin(fraction * Math.PI));
  ctx.lineWidth = Math.max(2, tile * 0.05);
  for (const offset of [-0.34, 0, 0.34]) {
    ctx.beginPath();
    ctx.moveTo(x + offset * tile, py(0));
    ctx.lineTo(x + offset * tile, py(mapHeight));
    ctx.stroke();
  }
  ctx.restore();
}

function drawSignatures(input: ArcRelayVisualContext, state: ArcState): void {
  const { ctx, tile, time } = input;
  for (const signature of state.visibleSignatures) {
    const accent = teamAccent(input, signature.ownerTeamId);
    const tell = signature.phase === 'tell';
    const alpha = tell ? 0.52 + 0.22 * Math.sin(time * Math.PI * 3) : 0.7;
    const points = signature.positions.map((position) => centre(input, position));
    ctx.save();
    ctx.strokeStyle = withAlpha(accent, alpha);
    ctx.fillStyle = withAlpha(accent, tell ? 0.055 : 0.12);
    ctx.lineWidth = Math.max(2, tile * (tell ? 0.045 : 0.07));
    if (tell) ctx.setLineDash([tile * 0.12, tile * 0.08]);

    const areaField = ['survey-flare', 'null-field', 'smoke-canister']
      .includes(signature.signatureId);
    if (areaField && points.length > 0) {
      const minX = Math.min(...points.map((point) => point.x));
      const maxX = Math.max(...points.map((point) => point.x));
      const minY = Math.min(...points.map((point) => point.y));
      const maxY = Math.max(...points.map((point) => point.y));
      ctx.beginPath();
      ctx.ellipse(
        (minX + maxX) / 2,
        (minY + maxY) / 2,
        (maxX - minX) / 2 + tile * 0.44,
        (maxY - minY) / 2 + tile * 0.44,
        0,
        0,
        Math.PI * 2,
      );
      ctx.fill();
      ctx.stroke();
    } else {
      for (const point of points) {
        ctx.fillRect(point.x - tile * 0.38, point.y - tile * 0.38, tile * 0.76, tile * 0.76);
        ctx.strokeRect(point.x - tile * 0.38, point.y - tile * 0.38, tile * 0.76, tile * 0.76);
      }
      if (points.length > 1) {
        ctx.beginPath();
        ctx.moveTo(points[0]!.x, points[0]!.y);
        for (const point of points.slice(1)) ctx.lineTo(point.x, point.y);
        ctx.stroke();
      }
    }

    const anchor = points[0] ?? actorCentre(input, signature.ownerActor);
    if (anchor) drawSignatureGlyph(ctx, signature.signatureId, anchor.x, anchor.y, tile, accent);
    if (signature.targetActor) {
      const target = actorCentre(input, signature.targetActor);
      const owner = actorCentre(input, signature.ownerActor);
      if (target) drawTargetBrackets(ctx, target.x, target.y, tile, accent);
      if (target && owner && ['repair-beam', 'exchange', 'tractor-hook'].includes(signature.signatureId)) {
        ctx.beginPath();
        ctx.moveTo(owner.x, owner.y);
        ctx.lineTo(target.x, target.y);
        ctx.stroke();
      }
    }
    if (signature.suppressed && anchor) {
      ctx.setLineDash([]);
      ctx.strokeStyle = 'rgba(226, 232, 240, 0.8)';
      ctx.lineWidth = Math.max(2, tile * 0.06);
      ctx.beginPath();
      ctx.moveTo(anchor.x - tile * 0.28, anchor.y - tile * 0.28);
      ctx.lineTo(anchor.x + tile * 0.28, anchor.y + tile * 0.28);
      ctx.moveTo(anchor.x + tile * 0.28, anchor.y - tile * 0.28);
      ctx.lineTo(anchor.x - tile * 0.28, anchor.y + tile * 0.28);
      ctx.stroke();
    }
    ctx.restore();
  }
}

function drawSignatureGlyph(
  ctx: CanvasRenderingContext2D,
  signatureId: string,
  x: number,
  y: number,
  tile: number,
  accent: string,
): void {
  ctx.save();
  ctx.setLineDash([]);
  ctx.strokeStyle = withAlpha(accent, 0.9);
  ctx.fillStyle = withAlpha(accent, 0.18);
  ctx.lineWidth = Math.max(2, tile * 0.055);
  if (['survey-flare', 'null-field', 'kinetic-burst', 'smoke-canister'].includes(signatureId)) {
    ctx.beginPath();
    ctx.arc(x, y, tile * (signatureId === 'null-field' ? 0.55 : 0.42), 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
  } else if (signatureId === 'falling-star') {
    ctx.beginPath();
    ctx.moveTo(x - tile * 0.42, y);
    ctx.lineTo(x + tile * 0.42, y);
    ctx.moveTo(x, y - tile * 0.42);
    ctx.lineTo(x, y + tile * 0.42);
    ctx.stroke();
  } else if (signatureId === 'trip-node' || signatureId === 'sentinel-seed') {
    ctx.beginPath();
    ctx.moveTo(x, y - tile * 0.3);
    ctx.lineTo(x + tile * 0.3, y + tile * 0.25);
    ctx.lineTo(x - tile * 0.3, y + tile * 0.25);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
  } else if (signatureId === 'hardlight-block' || signatureId === 'prism-wall') {
    ctx.fillRect(x - tile * 0.28, y - tile * 0.28, tile * 0.56, tile * 0.56);
    ctx.strokeRect(x - tile * 0.28, y - tile * 0.28, tile * 0.56, tile * 0.56);
  } else if (signatureId === 'arc-toss') {
    ctx.beginPath();
    ctx.arc(x, y, tile * 0.32, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(x - tile * 0.18, y);
    ctx.lineTo(x + tile * 0.18, y);
    ctx.moveTo(x, y - tile * 0.18);
    ctx.lineTo(x, y + tile * 0.18);
    ctx.stroke();
  } else {
    ctx.beginPath();
    ctx.moveTo(x - tile * 0.32, y + tile * 0.2);
    ctx.lineTo(x + tile * 0.32, y);
    ctx.lineTo(x - tile * 0.32, y - tile * 0.2);
    ctx.stroke();
  }
  ctx.restore();
}

function drawTargetBrackets(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  tile: number,
  accent: string,
): void {
  const inner = tile * 0.28;
  const outer = tile * 0.43;
  ctx.save();
  ctx.setLineDash([]);
  ctx.strokeStyle = withAlpha(accent, 0.92);
  ctx.lineWidth = Math.max(2, tile * 0.055);
  for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const) {
    ctx.beginPath();
    ctx.moveTo(x + sx * inner, y + sy * outer);
    ctx.lineTo(x + sx * outer, y + sy * outer);
    ctx.lineTo(x + sx * outer, y + sy * inner);
    ctx.stroke();
  }
  ctx.restore();
}

function drawCores(
  input: ArcRelayVisualContext,
  state: ArcState,
  carriedPass: boolean,
): void {
  const threat = closestCoreToReactor(state);
  for (const core of state.visibleCores) {
    const carried = core.disposition === 'carried' && core.carrierActor !== null;
    if (carried !== carriedPass) continue;
    const base = centre(input, core.position);
    const accent = carried
      ? input.accentFor(core.carrierActor!.unitKey)
      : '#f5f8fb';
    const radius = input.tile * 0.16;
    let x = base.x;
    let y = base.y;
    if (carried) {
      const orbit = input.time * Math.PI * 1.4;
      x += Math.cos(orbit) * input.tile * 0.3;
      y += Math.sin(orbit) * input.tile * 0.18 - input.tile * 0.14;
      input.ctx.save();
      input.ctx.strokeStyle = withAlpha(accent, 0.78);
      input.ctx.lineWidth = Math.max(2, input.tile * 0.05);
      input.ctx.beginPath();
      input.ctx.moveTo(base.x, base.y);
      input.ctx.quadraticCurveTo(base.x, y - input.tile * 0.18, x, y);
      input.ctx.stroke();
      input.ctx.restore();
    } else if (core.disposition === 'in-flight' && core.flightTarget) {
      const target = centre(input, core.flightTarget);
      const progress = input.fraction;
      x = base.x + (target.x - base.x) * progress;
      y = base.y + (target.y - base.y) * progress -
        Math.sin(progress * Math.PI) * input.tile * 0.42;
      input.ctx.save();
      input.ctx.setLineDash([input.tile * 0.1, input.tile * 0.08]);
      input.ctx.strokeStyle = 'rgba(245, 248, 251, 0.55)';
      input.ctx.lineWidth = Math.max(1.5, input.tile * 0.035);
      input.ctx.beginPath();
      input.ctx.moveTo(base.x, base.y);
      input.ctx.quadraticCurveTo(
        (base.x + target.x) / 2,
        Math.min(base.y, target.y) - input.tile * 0.8,
        target.x,
        target.y,
      );
      input.ctx.stroke();
      input.ctx.restore();
    }

    const cracked = (input.tick?.events ?? []).some(
      (event) =>
        event.arcRelayFact?.kind === 'core-dropped' &&
        coreKey(event.arcRelayFact.coreId) === coreKey(core.coreId),
    );
    input.ctx.save();
    input.ctx.shadowColor = accent;
    input.ctx.shadowBlur = Math.max(4, input.tile * (carried ? 0.28 : 0.18));
    drawSourceGlyph(input.ctx, x, y, radius, core.coreId.sourceWellId, accent, cracked);
    if (coreKey(core.coreId) === threat) {
      input.ctx.strokeStyle = withAlpha(accent, 0.7);
      input.ctx.lineWidth = Math.max(1.5, input.tile * 0.035);
      input.ctx.beginPath();
      input.ctx.arc(x, y, radius + input.tile * 0.12, 0, Math.PI * 2);
      input.ctx.stroke();
    }
    if (core.nextRelocationTick > Math.floor(input.time)) {
      input.ctx.shadowBlur = 0;
      input.ctx.strokeStyle = 'rgba(148, 163, 184, 0.95)';
      input.ctx.lineWidth = Math.max(2, input.tile * 0.055);
      input.ctx.beginPath();
      input.ctx.moveTo(x - radius * 0.65, y - radius - input.tile * 0.08);
      input.ctx.lineTo(x + radius * 0.65, y - radius - input.tile * 0.08);
      input.ctx.stroke();
    }
    input.ctx.restore();
  }
}

function closestCoreToReactor(state: ArcState): string | null {
  let best: { key: string; distance: number } | null = null;
  for (const core of state.visibleCores) {
    const destinations = core.carrierActor
      ? state.reactors.filter((reactor) => reactor.teamId === core.carrierActor!.teamId)
      : state.reactors;
    const distance = Math.min(
      ...destinations.map((reactor) =>
        Math.abs(reactor.position.x - core.position.x) +
        Math.abs(reactor.position.y - core.position.y)),
    );
    const candidate = { key: coreKey(core.coreId), distance };
    if (!best || candidate.distance < best.distance ||
        (candidate.distance === best.distance && candidate.key < best.key))
      best = candidate;
  }
  return best?.key ?? null;
}

function drawSourceGlyph(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
  wellId: string,
  color: string,
  cracked: boolean,
): void {
  ctx.save();
  ctx.fillStyle = color;
  ctx.strokeStyle = color;
  ctx.lineWidth = Math.max(1.5, radius * 0.28);
  ctx.beginPath();
  if (wellId.includes('north')) {
    ctx.moveTo(x, y - radius);
    ctx.lineTo(x + radius * 0.9, y + radius * 0.72);
    ctx.lineTo(x - radius * 0.9, y + radius * 0.72);
    ctx.closePath();
    ctx.fill();
  } else if (wellId.includes('south')) {
    ctx.moveTo(x, y - radius);
    ctx.lineTo(x + radius, y);
    ctx.lineTo(x, y + radius);
    ctx.lineTo(x - radius, y);
    ctx.closePath();
    ctx.fill();
  } else {
    ctx.arc(x, y, radius * 0.78, 0, Math.PI * 2);
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(x, y, radius * 0.25, 0, Math.PI * 2);
    ctx.fill();
  }
  if (cracked) {
    ctx.strokeStyle = 'rgba(8, 13, 19, 0.95)';
    ctx.lineWidth = Math.max(1.5, radius * 0.22);
    ctx.beginPath();
    ctx.moveTo(x - radius * 0.45, y - radius * 0.75);
    ctx.lineTo(x + radius * 0.05, y - radius * 0.12);
    ctx.lineTo(x - radius * 0.12, y + radius * 0.22);
    ctx.lineTo(x + radius * 0.48, y + radius * 0.76);
    ctx.stroke();
  }
  ctx.restore();
}

function drawHandoffCommitments(input: ArcRelayVisualContext): void {
  const { ctx, tile, fraction } = input;
  if (fraction > 0.72) return;
  for (const turn of input.tick?.actorTurns ?? []) {
    if (turn.actionResolution.validatedActionId !== 'handoff-core') continue;
    const targetKey = turn.actionResolution.validatedPayload?.unitKey;
    if (!targetKey) continue;
    const source = actorCentre(input, turn.actor);
    const targetPose = input.poses.find((pose) => pose.unitKey === targetKey);
    const target = targetPose
      ? centre(input, { x: targetPose.x, y: targetPose.y })
      : null;
    if (!source || !target) continue;
    const accent = input.accentFor(turn.actor.unitKey);
    ctx.save();
    ctx.strokeStyle = withAlpha(accent, 0.75);
    ctx.lineWidth = Math.max(2, tile * 0.05);
    ctx.setLineDash([tile * 0.1, tile * 0.08]);
    ctx.beginPath();
    ctx.moveTo(source.x, source.y);
    ctx.lineTo(target.x, target.y);
    ctx.stroke();
    for (const point of [source, target]) {
      ctx.beginPath();
      ctx.arc(point.x, point.y, tile * 0.48, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.restore();
  }
}

function drawTransferEffects(input: ArcRelayVisualContext): void {
  const { ctx, tile, fraction } = input;
  for (const event of input.tick?.events ?? []) {
    if (event.arcRelayFact?.kind !== 'core-handed-off') continue;
    const from = actorCentre(input, event.arcRelayFact.sourceActor);
    const to = actorCentre(input, event.arcRelayFact.targetActor);
    if (!from || !to) continue;
    const accent = input.accentFor(event.arcRelayFact.targetActor.unitKey);
    const midX = (from.x + to.x) / 2;
    const midY = (from.y + to.y) / 2 - tile * 0.45;
    ctx.save();
    ctx.strokeStyle = withAlpha(accent, 0.88 * (1 - fraction * 0.55));
    ctx.lineWidth = Math.max(2, tile * 0.06);
    ctx.beginPath();
    ctx.moveTo(from.x, from.y);
    ctx.quadraticCurveTo(midX, midY, to.x, to.y);
    ctx.stroke();
    const firstX = from.x + (midX - from.x) * fraction;
    const firstY = from.y + (midY - from.y) * fraction;
    const dotX = firstX + (to.x - firstX) * fraction;
    const dotY = firstY + (to.y - firstY) * fraction;
    ctx.fillStyle = '#f5f8fb';
    ctx.beginPath();
    ctx.arc(dotX, dotY, Math.max(2.5, tile * 0.07), 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }
}

function drawSignatureReadiness(
  input: ArcRelayVisualContext,
  state: ArcState,
): void {
  if (
    input.replay.contract.kind !== 'v3-generic' ||
    input.replay.contract.rawContract.rules.gameMode.kind !== 'arc-relay'
  ) return;
  const signatures = input.replay.contract.rawContract.rules.gameMode.signatures;
  for (const pose of input.poses) {
    if (!pose.formId.startsWith('arc-body-') || pose.status !== 'active') continue;
    const classId = pose.formId.slice('arc-body-'.length);
    const signature = signatures.find((entry) => entry.classId === classId);
    if (!signature) continue;
    const turn = input.tick?.actorTurns.find((entry) => entry.actorKey === pose.actorKey);
    const legal = turn?.observation.actions?.find(
      (action) => action.actionId === signature.actionId,
    );
    const active = state.visibleSignatures.some(
      (entry) => entry.ownerActor.actorKey === pose.actorKey,
    );
    const point = centre(input, { x: pose.x, y: pose.y });
    const x = point.x + input.tile * 0.34;
    const y = point.y + input.tile * 0.34;
    const accent = input.accentFor(pose.unitKey);
    input.ctx.save();
    input.ctx.fillStyle = active
      ? '#f6b73c'
      : legal?.available
        ? accent
        : 'rgba(100, 116, 139, 0.72)';
    input.ctx.strokeStyle = 'rgba(5, 9, 14, 0.88)';
    input.ctx.lineWidth = Math.max(1.5, input.tile * 0.035);
    input.ctx.beginPath();
    input.ctx.arc(x, y, Math.max(3, input.tile * 0.075), 0, Math.PI * 2);
    input.ctx.fill();
    input.ctx.stroke();
    input.ctx.restore();
  }
}

function withAlpha(color: string, alpha: number): string {
  if (!/^#[0-9a-f]{6}$/i.test(color)) return color;
  const red = Number.parseInt(color.slice(1, 3), 16);
  const green = Number.parseInt(color.slice(3, 5), 16);
  const blue = Number.parseInt(color.slice(5, 7), 16);
  return `rgba(${red}, ${green}, ${blue}, ${Math.max(0, Math.min(1, alpha))})`;
}
