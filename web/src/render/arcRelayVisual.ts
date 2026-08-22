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
import { unitLook } from './unitPresentation';
import { arcBankValue, arcOriginAccent } from '../replayPresentation';
import { teamAccentedEffectImage } from './arenaThemes';
import type { CrestPresentation } from '../components/EntrantCrest';
import { ARC_CORE_NEUTRAL_PALETTE } from '../presentation/arcCorePalette';
import {
  arcVeterancyFor,
  LEVEL_UP_BEAT_TICKS,
} from '../replayArcVeterancy';
import {
  arcWorkshopsFor,
  type ArcWorkshopVisual,
} from './arcWorkshopPresentation';
import {
  resolveArcSignaturePresentation,
  type ArcSignatureCanvasMarker,
} from './arcSignaturePresentation';
import { ARC_SIGNATURE_PRESENTATION_ID } from './arcSignaturePresentationId';
import { ARC_TOSS_RELEASE_TICK_OFFSET } from './signatures/definitions/arcToss';
import {
  HEALTH_BAR_RELATIVE_WIDTH,
  healthBarState,
} from '../presentation/healthBar';

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
  entrants?: readonly { teamId: number; crest: CrestPresentation }[];
}

type ArcState = ReplayArcRelayModeState;

export function drawArcRelayGround(input: ArcRelayVisualContext): void {
  const state = currentState(input);
  if (!state) return;

  drawPulse(input, state);
  drawHealZones(input);
  drawWorkshopPlates(input, state);
  drawWells(input, state);
  drawReactors(input, state);
  drawSignatures(input, state);
  drawCores(input, state, false);
  drawHandoffCommitments(input);
}

// The lit cone (DECISIONS #212) is GONE (owner ruling 2026-08). The
// tracking ray of #215 already says who the strike is for and how close it
// is, so blinking the frozen wedge underneath it was noise on top of the
// answer. `drawStrikeAims` in drawArena owns the windup read and the red
// slash owns the landing; the wedge stays a wire fact, not a floor decal.

export function drawArcRelayOverlay(input: ArcRelayVisualContext): void {
  const state = currentState(input);
  if (!state) return;

  drawCores(input, state, true);
  drawTransferEffects(input);
  drawWorkshopReads(input, state);
  drawDiegeticEvents(input, state);
  drawLevelUpBeats(input);
  drawSignatureCombatEvents(input, state);
  drawSignatureReadiness(input, state);
}

/**
 * Signature damage and repair resolve during tick start, so their facts
 * arrive in `lifecycleEvents`, not `events` — reading only the latter is
 * how sentinel fire spent a whole campaign invisible. A sentinel shot is
 * a tracer from its turret tile; a mine or artillery hit is an impact
 * burst; a repair is a heal pulse on the patient.
 */
const SIGNATURE_SHOT_LINGER_TICKS = 3;

function drawSignatureCombatEvents(
  input: ArcRelayVisualContext,
  state: ArcState,
): void {
  const { ctx, tile, fraction } = input;
  const tick = input.tick;
  if (!tick) return;
  // A signature shot is instant in the engine — no projectile entity, one
  // fact, one tick. Drawn only on its own tick it is a subliminal flash at
  // playback speed, which read as "sentries never shoot". So each shot is
  // presented as a bolt that travels muzzle-to-impact and lingers as an
  // impact burst over the following ticks.
  for (let age = 0; age < SIGNATURE_SHOT_LINGER_TICKS; age += 1) {
    const source = age === 0
      ? tick
      : input.replay.ticks[tick.tick - age];
    if (!source) continue;
    const shotAge = age + fraction;
    const life = Math.max(
      0,
      1 - shotAge / SIGNATURE_SHOT_LINGER_TICKS,
    );
    if (life <= 0) continue;
    for (const event of [...source.lifecycleEvents, ...source.events]) {
      const fact = event.arcRelayFact;
      if (!fact) continue;
      if (fact.kind !== 'signature-damage' && fact.kind !== 'signature-repair')
        continue;
      const impact = centre(input, fact.position);
      const accent = teamAccent(input, fact.ownerActor.teamId);
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      if (fact.kind === 'signature-repair') {
        ctx.strokeStyle = withAlpha('#6ee7a8', life * 0.9);
        ctx.lineWidth = Math.max(2, tile * 0.08);
        const arm = tile * (0.14 + 0.16 * shotAge);
        ctx.beginPath();
        ctx.moveTo(impact.x - arm, impact.y);
        ctx.lineTo(impact.x + arm, impact.y);
        ctx.moveTo(impact.x, impact.y - arm);
        ctx.lineTo(impact.x, impact.y + arm);
        ctx.stroke();
        ctx.restore();
        continue;
      }
      const operation = state.visibleSignatures.find(
        (candidate) => candidate.operationId === fact.operationId,
      );
      const muzzle = operation?.positions[0]
        ? centre(input, operation.positions[0])
        : actorCentre(input, fact.ownerActor);
      const beamlike = fact.signatureId === 'sentinel-seed';
      if (
        muzzle &&
        beamlike &&
        (muzzle.x !== impact.x || muzzle.y !== impact.y)
      ) {
        // Bolt head runs the line over the first tick; the trail behind it
        // fades for the rest of the linger window.
        const travel = Math.min(1, shotAge);
        const headX = muzzle.x + (impact.x - muzzle.x) * travel;
        const headY = muzzle.y + (impact.y - muzzle.y) * travel;
        ctx.strokeStyle = withAlpha(accent, life * 0.85);
        ctx.lineWidth = Math.max(3, tile * 0.12);
        ctx.beginPath();
        ctx.moveTo(muzzle.x, muzzle.y);
        ctx.lineTo(headX, headY);
        ctx.stroke();
        ctx.fillStyle = withAlpha('#f8fafc', life);
        ctx.beginPath();
        ctx.arc(headX, headY, Math.max(3, tile * 0.16), 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = withAlpha('#f8fafc', life * 0.9);
        ctx.beginPath();
        ctx.arc(muzzle.x, muzzle.y, Math.max(2.5, tile * 0.14), 0, Math.PI * 2);
        ctx.fill();
      }
      const burst =
        fact.signatureId === 'falling-star'
          ? 0.6
          : fact.signatureId === 'trip-node'
            ? 0.5
            : 0.34;
      ctx.strokeStyle = withAlpha(accent, life * 0.9);
      ctx.lineWidth = Math.max(2.5, tile * 0.09);
      ctx.beginPath();
      ctx.arc(
        impact.x,
        impact.y,
        tile * burst * Math.min(1, 0.35 + shotAge * 0.65),
        0,
        Math.PI * 2,
      );
      ctx.stroke();
      ctx.fillStyle = withAlpha('#f8fafc', life * 0.6);
      ctx.beginPath();
      ctx.arc(impact.x, impact.y, Math.max(2.5, tile * 0.12), 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  }
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

/**
 * Heal zones are static map regions: a dim green plate with a cross,
 * breathing slowly. The channel itself is announced on the bot (its green
 * ring in `drawArena`); the pad only marks where channelling is possible.
 */
function drawHealZones(input: ArcRelayVisualContext): void {
  const { ctx, tile, time } = input;
  const tiles = input.replay.map.regions
    .filter((region) => region.regionId.startsWith('heal-'))
    .flatMap((region) => region.tiles);
  for (const [index, position] of tiles.entries()) {
    const point = centre(input, position);
    const pulse = 0.5 + 0.5 * Math.sin(time * Math.PI * 0.8 + index);
    const radius = tile * 0.34;
    ctx.save();
    ctx.fillStyle = 'rgba(22, 101, 52, 0.5)';
    ctx.beginPath();
    ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = `rgba(74, 222, 128, ${0.34 + 0.2 * pulse})`;
    ctx.lineWidth = Math.max(1.5, tile * 0.05);
    ctx.beginPath();
    ctx.arc(point.x, point.y, radius + tile * 0.05, 0, Math.PI * 2);
    ctx.stroke();
    ctx.fillStyle = 'rgba(134, 239, 172, 0.85)';
    const bar = tile * 0.3;
    const thickness = Math.max(2, tile * 0.075);
    ctx.fillRect(point.x - bar / 2, point.y - thickness / 2, bar, thickness);
    ctx.fillRect(point.x - thickness / 2, point.y - bar / 2, thickness, bar);
    ctx.restore();
  }
}

/**
 * The Workshop's own colour. Deliberately outside every other landmark's
 * family — Wells are monochrome and amber, heal pads green, reactors team
 * accents — so a violet hexagon is never mistaken for one of them, and it
 * stays neutral because the landmark is neutral.
 */
const WORKSHOP_NEUTRAL = '#a78bfa';
const WORKSHOP_CONTESTED = '#fb7185';
const WORKSHOP_PLATE = 'rgba(24, 16, 43, 0.78)';
const WORKSHOP_DORMANT_PLATE = 'rgba(13, 10, 22, 0.6)';

/**
 * The Workshop landmark, in two halves on purpose.
 *
 * The PLATE is floor furniture and belongs on the ground, under the shroud and
 * under the bodies standing on it, exactly like the Well beside it. The READ —
 * phase, channel progress, dormancy countdown — is public to both teams
 * (DECISIONS: Workshop state carries no fog), so it is drawn in the overlay
 * pass where the shroud cannot swallow a fact both minds can see.
 */
function drawWorkshopPlates(
  input: ArcRelayVisualContext,
  state: ArcState,
): void {
  const { ctx, tile, time } = input;
  // `?? []` guards a hand-built mode state from before the field existed; a
  // normalized replay always carries the array, empty when there are none.
  for (const workshop of state.workshops ?? []) {
    const point = centre(input, workshop.trainingTile);
    const radius = tile * 0.4;
    const dormant = workshop.phase === 'dormant';
    const contested = workshop.phase === 'contested';
    // A contested Workshop names no team, so its alert is the landmark's own
    // colour shifted to rose rather than anyone's accent.
    const accent =
      contested || workshop.trainingTeamId === null
        ? contested
          ? WORKSHOP_CONTESTED
          : WORKSHOP_NEUTRAL
        : teamAccent(input, workshop.trainingTeamId);
    const breathe =
      0.5 + 0.5 * Math.sin(time * Math.PI * (contested ? 3.6 : 0.9));
    const dim = dormant ? 0.4 : 1;

    ctx.save();
    ctx.fillStyle = dormant ? WORKSHOP_DORMANT_PLATE : WORKSHOP_PLATE;
    workshopHexPath(ctx, point.x, point.y, radius);
    ctx.fill();
    ctx.strokeStyle = withAlpha(
      accent,
      (contested ? 0.5 + 0.45 * breathe : 0.34 + 0.28 * breathe) * dim,
    );
    ctx.lineWidth = Math.max(1.5, tile * 0.04);
    ctx.stroke();
    drawAnvilGlyph(
      ctx,
      point.x,
      point.y,
      tile * 0.19,
      withAlpha(dormant ? WORKSHOP_NEUTRAL : accent, dim * 0.95),
    );
    ctx.restore();
  }
}

function drawWorkshopReads(
  input: ArcRelayVisualContext,
  state: ArcState,
): void {
  const { ctx, tile, time } = input;
  const workshops = arcWorkshopsFor(input.replay).visualsAt(
    state.workshops,
    time,
  );
  for (const workshop of workshops) {
    const point = centre(input, workshop.tile);
    const radius = tile * 0.4;
    const ring = radius + tile * 0.09;
    const accent =
      workshop.trainingTeamId === null
        ? WORKSHOP_NEUTRAL
        : teamAccent(input, workshop.trainingTeamId);
    ctx.save();
    ctx.lineCap = 'butt';
    if (workshop.phase === 'training') {
      ctx.strokeStyle = 'rgba(148, 163, 184, 0.3)';
      ctx.lineWidth = Math.max(2, tile * 0.055);
      ctx.beginPath();
      ctx.arc(point.x, point.y, ring, 0, Math.PI * 2);
      ctx.stroke();
      ctx.strokeStyle = withAlpha(accent, 0.94);
      ctx.lineWidth = Math.max(2.5, tile * 0.07);
      ctx.beginPath();
      ctx.arc(
        point.x,
        point.y,
        ring,
        -Math.PI / 2,
        -Math.PI / 2 + Math.PI * 2 * workshop.progressFraction,
      );
      ctx.stroke();
      // The ratio is the honest read of a channel; it is dropped rather than
      // shrunk to mush when the camera is far enough out that it cannot be
      // read anyway.
      if (tile >= 20) {
        drawWorkshopLabel(
          ctx,
          point.x,
          // Above the tile: the space below belongs to the role tag of the
          // body that is standing on the Workshop while it trains.
          point.y - radius - tile * 0.26,
          tile,
          `${workshop.progressTicks}/${workshop.requiredTicks}`,
          accent,
        );
      }
    } else if (workshop.phase === 'contested') {
      const flash = 0.5 + 0.5 * Math.sin(time * Math.PI * 3.6);
      ctx.strokeStyle = withAlpha(WORKSHOP_CONTESTED, 0.55 + 0.4 * flash);
      ctx.lineWidth = Math.max(2.5, tile * 0.07);
      ctx.beginPath();
      ctx.arc(point.x, point.y, ring, 0, Math.PI * 2);
      ctx.stroke();
      drawSuppressionCross(ctx, point, tile * 0.8);
    } else if (workshop.phase === 'dormant') {
      // Only the countdown is certain; the ring needs a declared dormancy
      // length, so a ruleset that never published one gets the number alone
      // rather than an arc measured against a guess.
      if (workshop.dormantFraction !== null) {
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.22)';
        ctx.lineWidth = Math.max(2, tile * 0.05);
        ctx.beginPath();
        ctx.arc(point.x, point.y, ring, 0, Math.PI * 2);
        ctx.stroke();
        ctx.strokeStyle = withAlpha(WORKSHOP_NEUTRAL, 0.6);
        ctx.lineWidth = Math.max(2, tile * 0.055);
        ctx.beginPath();
        ctx.arc(
          point.x,
          point.y,
          ring,
          -Math.PI / 2,
          -Math.PI / 2 + Math.PI * 2 * workshop.dormantFraction,
        );
        ctx.stroke();
      }
      if (workshop.dormantRemainingTicks !== null) {
        drawWorkshopLabel(
          ctx,
          point.x,
          point.y - radius - tile * 0.26,
          tile,
          String(workshop.dormantRemainingTicks),
          'rgba(203, 213, 225, 0.92)',
        );
      }
    } else {
      const breathe = 0.5 + 0.5 * Math.sin(time * Math.PI * 1.1);
      ctx.strokeStyle = withAlpha(WORKSHOP_NEUTRAL, 0.34 + 0.34 * breathe);
      ctx.lineWidth = Math.max(1.5, tile * 0.045);
      ctx.beginPath();
      ctx.arc(point.x, point.y, ring, 0, Math.PI * 2);
      ctx.stroke();
    }
    drawWorkshopBeats(input, workshop, point, radius);
    ctx.restore();
  }
}

/**
 * The two Workshop beats. A completion is the XP actually leaving the anvil,
 * so it is a bright expanding hexagon in the AWARDED team's accent carrying
 * the exact `xpAward` — never the trainee, whose identity the landmark does
 * not publish. An interruption is the opposite motion: a rose ring collapsing
 * inward over the work that was lost.
 */
function drawWorkshopBeats(
  input: ArcRelayVisualContext,
  workshop: ArcWorkshopVisual,
  point: CanvasPoint,
  radius: number,
): void {
  const { ctx, tile } = input;
  const completion = workshop.completion;
  if (completion) {
    const accent = teamAccent(input, completion.teamId);
    const spread = radius + tile * (0.12 + 0.6 * (1 - completion.strength));
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.shadowColor = accent;
    ctx.shadowBlur = Math.max(8, tile * 0.3);
    ctx.strokeStyle = withAlpha(accent, completion.strength * 0.95);
    ctx.lineWidth = Math.max(2.5, tile * 0.07);
    workshopHexPath(ctx, point.x, point.y, spread);
    ctx.stroke();
    ctx.restore();
    if (tile >= 16) {
      drawWorkshopLabel(
        ctx,
        point.x,
        // Stacked above the phase read, and rising as it fades.
        point.y - radius - tile * (0.66 + 0.34 * (1 - completion.strength)),
        tile,
        `+${completion.xpAward} XP`,
        withAlpha(accent, Math.min(1, 0.35 + completion.strength)),
      );
    }
  }
  const interruption = workshop.interruption;
  if (interruption) {
    const collapse = radius + tile * 0.5 * interruption.strength;
    ctx.save();
    ctx.strokeStyle = withAlpha(
      WORKSHOP_CONTESTED,
      interruption.strength * 0.8,
    );
    ctx.setLineDash([Math.max(2, tile * 0.1), Math.max(2, tile * 0.1)]);
    ctx.lineWidth = Math.max(2, tile * 0.055);
    ctx.beginPath();
    ctx.arc(point.x, point.y, collapse, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
  }
}

function workshopHexPath(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
): void {
  ctx.beginPath();
  for (let corner = 0; corner < 6; corner += 1) {
    const angle = -Math.PI / 2 + (corner * Math.PI) / 3;
    const cx = x + Math.cos(angle) * radius;
    const cy = y + Math.sin(angle) * radius;
    if (corner === 0) ctx.moveTo(cx, cy);
    else ctx.lineTo(cx, cy);
  }
  ctx.closePath();
}

/** A stubby anvil: horned top plate, waist, base. Reads at a phone's tile. */
function drawAnvilGlyph(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
  color: string,
): void {
  ctx.save();
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.moveTo(x - radius * 0.95, y - radius * 0.62);
  ctx.lineTo(x + radius * 0.72, y - radius * 0.62);
  ctx.lineTo(x + radius * 1.24, y - radius * 0.28);
  ctx.lineTo(x + radius * 0.72, y - radius * 0.18);
  ctx.lineTo(x + radius * 0.36, y - radius * 0.18);
  ctx.lineTo(x + radius * 0.36, y + radius * 0.3);
  ctx.lineTo(x + radius * 0.86, y + radius * 0.3);
  ctx.lineTo(x + radius * 0.86, y + radius * 0.66);
  ctx.lineTo(x - radius * 0.86, y + radius * 0.66);
  ctx.lineTo(x - radius * 0.86, y + radius * 0.3);
  ctx.lineTo(x - radius * 0.36, y + radius * 0.3);
  ctx.lineTo(x - radius * 0.36, y - radius * 0.18);
  ctx.lineTo(x - radius * 0.95, y - radius * 0.18);
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

function drawWorkshopLabel(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  tile: number,
  text: string,
  color: string,
): void {
  const size = Math.min(13, Math.max(7, Math.round(tile * 0.24)));
  ctx.save();
  ctx.font = `700 ${size}px ui-monospace, SFMono-Regular, Menlo, monospace`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.lineJoin = 'round';
  ctx.lineWidth = Math.max(2, size * 0.34);
  ctx.strokeStyle = 'rgba(2, 6, 12, 0.85)';
  ctx.strokeText(text, x, y);
  ctx.fillStyle = color;
  ctx.fillText(text, x, y);
  ctx.restore();
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
    if (well.rearmCompletesAtTick !== null) {
      const duration = Math.max(
        1,
        rules?.bankTriggeredWellRechargeTicks ?? rules?.pendingRearmTicks ?? 10,
      );
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
        radius + tile * 0.08,
        -Math.PI / 2,
        -Math.PI / 2 + Math.PI * 2 * progress,
      );
      ctx.stroke();
    } else if (well.nextScheduledBirthTick !== null && schedule) {
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
    if (rules?.teamRelativeCoreCharge) {
      const teams = [...state.reactors].sort((left, right) => left.teamId - right.teamId);
      ctx.font = `800 ${Math.max(8, tile * 0.22)}px ui-monospace, monospace`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      teams.forEach((reactor, index) => {
        const x = point.x + (index === 0 ? -1 : 1) * tile * 0.2;
        const y = point.y + tile * 0.47;
        const accent = teamAccent(input, reactor.teamId);
        ctx.fillStyle = 'rgba(4, 8, 13, 0.94)';
        ctx.beginPath();
        ctx.arc(x, y, tile * 0.14, 0, Math.PI * 2);
        ctx.fill();
        ctx.strokeStyle = withAlpha(accent, 0.95);
        ctx.lineWidth = Math.max(1, tile * 0.035);
        ctx.stroke();
        ctx.fillStyle = '#f8fafc';
        ctx.fillText(
          String(arcBankValue(input.replay, state, well.wellId, reactor.teamId, 1)),
          x,
          y + 0.5,
        );
      });
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
    const socketOrder = ['north', 'centre', 'south'];
    const sockets = reactor.filledSocketWellIds ?? [];
    for (let index = 0; index < 3; index++) {
      // Threefold sockets light positionally in the lane's own hue; the
      // count-based fill remains for every other ruleset (both styles are
      // identical at zero charge).
      ctx.fillStyle = sockets.length > 0
        ? sockets.includes(socketOrder[index]!)
          ? arcOriginAccent(socketOrder[index]!)
          : 'rgba(100, 116, 139, 0.32)'
        : index < reactor.chargePips
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
    const crest = input.entrants?.find((value) => value.teamId === reactor.teamId)?.crest;
    if (crest) drawReactorCrest(ctx, point.x, point.y, tile, crest);
    ctx.restore();
  }
}

function drawReactorCrest(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  tile: number,
  crest: CrestPresentation,
): void {
  const radius = tile * 0.19;
  ctx.save();
  ctx.fillStyle = crest.secondary;
  ctx.strokeStyle = crest.detail;
  ctx.lineWidth = Math.max(1, tile * 0.025);
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fill(); ctx.stroke();
  ctx.fillStyle = crest.primary;
  if (crest.pattern === 'split') ctx.fillRect(x, y - radius, radius, radius * 2);
  else if (crest.pattern === 'band') ctx.fillRect(x - radius, y - radius * .35, radius * 2, radius * .7);
  else { ctx.beginPath(); ctx.arc(x, y, radius * .58, 0, Math.PI * 2); ctx.fill(); }
  ctx.fillStyle = crest.detail;
  ctx.font = `900 ${Math.max(7, tile * .18)}px ui-monospace, monospace`;
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(crest.mark.slice(0, 1).toUpperCase(), x, y + tile * .01);
  ctx.restore();
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
    if (
      input.replay.signaturePresentationId === ARC_SIGNATURE_PRESENTATION_ID &&
      signature.signatureId === 'arc-toss' &&
      signature.phase === 'in-flight' &&
      time < signature.startedTick + ARC_TOSS_RELEASE_TICK_OFFSET
    ) continue;
    const accent = teamAccent(input, signature.ownerTeamId);
    const tell = signature.phase === 'tell';
    const alpha = tell ? 0.52 + 0.22 * Math.sin(time * Math.PI * 3) : 0.7;
    const points = signature.positions.map((position) => centre(input, position));
    ctx.save();
    ctx.strokeStyle = withAlpha(accent, alpha);
    ctx.fillStyle = withAlpha(accent, tell ? 0.055 : 0.12);
    ctx.lineWidth = Math.max(2, tile * (tell ? 0.045 : 0.07));
    if (tell) ctx.setLineDash([tile * 0.12, tile * 0.08]);
    const resolution = resolveArcSignaturePresentation(
      input.replay,
      signature.signatureId,
    );
    if (resolution.kind === 'legacy')
      drawLegacySignature(input, signature, points, accent);
    else if (resolution.kind === 'authored')
      drawAuthoredSignatureMarker(
        input,
        signature,
        resolution.definition.canvasMarker,
        points,
        accent,
      );
    else drawGenericSignatureMarker(input, signature, points, accent);
    ctx.restore();
  }
}

type CanvasSignature = ArcState['visibleSignatures'][number];
type CanvasPoint = { x: number; y: number };

/** Frozen compatibility renderer for replays without a presentation package. */
function drawLegacySignature(
  input: ArcRelayVisualContext,
  signature: CanvasSignature,
  points: CanvasPoint[],
  accent: string,
): void {
  const { ctx, tile, time } = input;
  const areaField = ['survey-flare', 'null-field', 'smoke-canister']
    .includes(signature.signatureId);
  const deployed = signature.phase === 'active'
    && ['sentinel-seed', 'trip-node'].includes(signature.signatureId);
  if (deployed && points.length > 0) {
    for (const point of points) {
      if (signature.signatureId === 'sentinel-seed')
        drawSentinelTurret(input, signature, point, accent);
      else {
        drawMine(ctx, point, tile, accent, time);
        drawConstructDurability(ctx, point, tile, signature, accent);
      }
      if (signature.suppressed) drawSuppressionCross(ctx, point, tile);
    }
    return;
  }
  if (areaField && points.length > 0) drawAreaEllipse(ctx, points, tile);
  else drawGenericFootprint(ctx, points, tile);

  const anchor = points[0] ?? actorCentre(input, signature.ownerActor);
  if (anchor)
    drawClassSignatureGlyph(
      input,
      signature.ownerActor,
      signature.signatureId,
      anchor.x,
      anchor.y,
      accent,
    );
  if (signature.targetActor) {
    const target = actorCentre(input, signature.targetActor);
    const owner = actorCentre(input, signature.ownerActor);
    if (target) drawTargetBrackets(ctx, target.x, target.y, tile, accent);
    if (
      target &&
      owner &&
      ['repair-beam', 'exchange', 'tractor-hook'].includes(signature.signatureId)
    )
      drawCanvasLine(ctx, owner, target);
  }
  if (signature.suppressed && anchor) drawSuppressionCross(ctx, anchor, tile);
}

/** Compact Canvas truth marker; WebGL owns the ornamental treatment. */
function drawAuthoredSignatureMarker(
  input: ArcRelayVisualContext,
  signature: CanvasSignature,
  marker: ArcSignatureCanvasMarker,
  points: CanvasPoint[],
  accent: string,
): void {
  const { ctx, tile, time } = input;
  const owner = actorCentre(input, signature.ownerActor);
  const target = signature.targetActor
    ? actorCentre(input, signature.targetActor)
    : null;
  const anchor = points[0] ?? target ?? owner;
  if (!anchor) return;

  if (marker === 'prop' && signature.phase === 'active' && points.length > 0) {
    for (const point of points) {
      if (signature.signatureId === 'sentinel-seed')
        drawSentinelTurret(input, signature, point, accent);
      else drawMine(ctx, point, tile, accent, time);
      if (signature.suppressed) drawSuppressionCross(ctx, point, tile);
    }
    return;
  }

  if (
    marker === 'calibration-field' ||
    marker === 'dark-field' ||
    marker === 'smoke-field'
  ) {
    if (points.length > 0) drawAreaEllipse(ctx, points, tile);
    if (marker === 'calibration-field') drawCalibrationTicks(ctx, points, tile);
    else if (marker === 'smoke-field') drawSmokeLobes(ctx, points, tile, time);
  } else if (marker === 'panels') {
    if ((signature.maximumCapacity ?? 0) >= 10)
      drawMovingPrismScreen(input, signature, accent);
    else drawConstructTiles(ctx, points, tile, false);
  } else if (marker === 'block') {
    drawConstructTiles(ctx, points, tile, true);
  } else if (marker === 'cardinal-target') {
    drawCardinalTarget(ctx, anchor, tile);
  } else if (marker === 'objective-arc') {
    // drawCores owns the only in-flight Core; this is destination truth only.
    const destination = points.at(-1) ?? target ?? anchor;
    drawDestinationRing(ctx, destination, tile);
  } else if (marker === 'tether' || marker === 'braid' || marker === 'paired-fold') {
    const from = owner ?? points[0];
    const to = target ?? points.at(-1);
    if (from && to) {
      drawCanvasLine(ctx, from, to);
      if (marker === 'braid') drawBraidedCanvasLine(ctx, from, to, tile);
      drawTargetBrackets(ctx, to.x, to.y, tile, accent);
      if (marker === 'paired-fold')
        drawTargetBrackets(ctx, from.x, from.y, tile, accent);
    }
  } else if (marker === 'pressure-lane') {
    drawPressureLane(ctx, points.length > 1 ? points : [owner, target].filter(isCanvasPoint), tile);
  } else if (marker === 'target-brackets') {
    const bracketTarget = target ?? anchor;
    drawTargetBrackets(ctx, bracketTarget.x, bracketTarget.y, tile, accent);
    drawCapacityTallies(ctx, bracketTarget, tile, signature.remainingCapacity);
  } else if (marker === 'pressure-ring') {
    drawPressureRing(ctx, owner ?? anchor, tile, signature.phase === 'tell');
  } else if (marker === 'arrow') {
    drawPathArrow(ctx, owner ?? anchor, points, tile);
  } else if (marker === 'prop') {
    drawDestinationRing(ctx, anchor, tile * 0.78);
  }

  if (signature.suppressed) drawSuppressionCross(ctx, anchor, tile);
  const durabilityAnchor = marker === 'panels' &&
      (signature.maximumCapacity ?? 0) >= 10
    ? owner ?? anchor
    : anchor;
  drawConstructDurability(ctx, durabilityAnchor, tile, signature, accent);
}

/** Unknown packages remain mechanically visible without borrowing current art. */
function drawGenericSignatureMarker(
  input: ArcRelayVisualContext,
  signature: CanvasSignature,
  points: CanvasPoint[],
  accent: string,
): void {
  const { ctx, tile } = input;
  const owner = actorCentre(input, signature.ownerActor);
  const target = signature.targetActor
    ? actorCentre(input, signature.targetActor)
    : null;
  const anchors = points.length > 0
    ? points
    : [target ?? owner].filter(isCanvasPoint);
  drawGenericFootprint(ctx, anchors, tile);
  if (target) drawTargetBrackets(ctx, target.x, target.y, tile, accent);
  const anchor = anchors[0];
  if (signature.suppressed && anchor) drawSuppressionCross(ctx, anchor, tile);
}

function isCanvasPoint(point: CanvasPoint | null | undefined): point is CanvasPoint {
  return point !== null && point !== undefined;
}

function drawGenericFootprint(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
): void {
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

function drawAreaEllipse(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
): void {
  if (points.length === 0) return;
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
}

function drawConstructTiles(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
  block: boolean,
): void {
  const inset = block ? 0.16 : 0.1;
  for (const point of points) {
    const half = tile * (0.5 - inset);
    ctx.fillRect(point.x - half, point.y - half, half * 2, half * 2);
    ctx.strokeRect(point.x - half, point.y - half, half * 2, half * 2);
  }
}

function drawMovingPrismScreen(
  input: ArcRelayVisualContext,
  signature: CanvasSignature,
  accent: string,
): void {
  const pose = input.poses.find(
    (candidate) => candidate.actorKey === signature.ownerActor.actorKey,
  );
  if (!pose) return;
  const owner = centre(input, { x: pose.x, y: pose.y });
  const { ctx, tile } = input;
  const health = Math.max(
    0,
    Math.min(
      1,
      signature.remainingCapacity / (signature.maximumCapacity ?? 1),
    ),
  );
  ctx.save();
  ctx.translate(owner.x, owner.y);
  ctx.rotate(pose.angle);
  ctx.lineCap = 'round';
  ctx.setLineDash([]);
  const front = tile * 0.92;
  for (let index = -1; index <= 1; index += 1) {
    const centreY = index * tile * 0.91;
    const half = tile * 0.4;
    ctx.strokeStyle = withAlpha('#c4a15e', signature.suppressed ? 0.38 : 0.92);
    ctx.lineWidth = Math.max(3, tile * 0.13);
    ctx.beginPath();
    ctx.moveTo(front, centreY - half);
    ctx.lineTo(front, centreY + half);
    ctx.stroke();
    ctx.strokeStyle = withAlpha(
      signature.suppressed ? '#b4bdc4' : '#75d8ff',
      (0.3 + health * 0.42) * (signature.suppressed ? 0.5 : 1),
    );
    ctx.lineWidth = Math.max(2, tile * 0.075);
    ctx.beginPath();
    ctx.moveTo(front, centreY - half * 0.92);
    ctx.lineTo(front, centreY + half * 0.92);
    ctx.stroke();
  }
  ctx.strokeStyle = withAlpha(accent, signature.suppressed ? 0.24 : 0.72);
  ctx.lineWidth = Math.max(2, tile * 0.04);
  ctx.beginPath();
  ctx.moveTo(tile * 0.32, 0);
  ctx.lineTo(tile * 0.68, -tile * 0.18);
  ctx.moveTo(tile * 0.32, 0);
  ctx.lineTo(tile * 0.68, tile * 0.18);
  ctx.stroke();
  ctx.restore();
}

function drawCardinalTarget(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
): void {
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * 0.34, 0, Math.PI * 2);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(point.x - tile * 0.52, point.y);
  ctx.lineTo(point.x - tile * 0.18, point.y);
  ctx.moveTo(point.x + tile * 0.18, point.y);
  ctx.lineTo(point.x + tile * 0.52, point.y);
  ctx.moveTo(point.x, point.y - tile * 0.52);
  ctx.lineTo(point.x, point.y - tile * 0.18);
  ctx.moveTo(point.x, point.y + tile * 0.18);
  ctx.lineTo(point.x, point.y + tile * 0.52);
  ctx.stroke();
}

function drawDestinationRing(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
): void {
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * 0.38, 0, Math.PI * 2);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * 0.13, 0, Math.PI * 2);
  ctx.stroke();
}

function drawCanvasLine(
  ctx: CanvasRenderingContext2D,
  from: CanvasPoint,
  to: CanvasPoint,
): void {
  ctx.beginPath();
  ctx.moveTo(from.x, from.y);
  ctx.lineTo(to.x, to.y);
  ctx.stroke();
}

function drawBraidedCanvasLine(
  ctx: CanvasRenderingContext2D,
  from: CanvasPoint,
  to: CanvasPoint,
  tile: number,
): void {
  const length = Math.hypot(to.x - from.x, to.y - from.y) || 1;
  const ox = (-(to.y - from.y) / length) * tile * 0.065;
  const oy = ((to.x - from.x) / length) * tile * 0.065;
  drawCanvasLine(ctx, { x: from.x + ox, y: from.y + oy }, { x: to.x + ox, y: to.y + oy });
  drawCanvasLine(ctx, { x: from.x - ox, y: from.y - oy }, { x: to.x - ox, y: to.y - oy });
}

function drawPressureLane(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
): void {
  if (points.length < 2) return;
  ctx.save();
  ctx.setLineDash([]);
  ctx.lineWidth = Math.max(4, tile * 0.22);
  ctx.globalAlpha *= 0.38;
  ctx.beginPath();
  ctx.moveTo(points[0]!.x, points[0]!.y);
  for (const point of points.slice(1)) ctx.lineTo(point.x, point.y);
  ctx.stroke();
  ctx.restore();
}

function drawPressureRing(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
  contracting: boolean,
): void {
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * (contracting ? 0.48 : 0.72), 0, Math.PI * 2);
  ctx.stroke();
}

function drawPathArrow(
  ctx: CanvasRenderingContext2D,
  owner: CanvasPoint,
  points: readonly CanvasPoint[],
  tile: number,
): void {
  const destination = points.at(-1) ?? owner;
  drawCanvasLine(ctx, owner, destination);
  const angle = Math.atan2(destination.y - owner.y, destination.x - owner.x);
  for (const turn of [-0.7, 0.7]) {
    ctx.beginPath();
    ctx.moveTo(destination.x, destination.y);
    ctx.lineTo(
      destination.x - Math.cos(angle + turn) * tile * 0.34,
      destination.y - Math.sin(angle + turn) * tile * 0.34,
    );
    ctx.stroke();
  }
}

function drawCalibrationTicks(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
): void {
  for (const [index, point] of points.entries()) {
    if (index % 2 !== 0) continue;
    ctx.beginPath();
    ctx.moveTo(point.x - tile * 0.12, point.y);
    ctx.lineTo(point.x + tile * 0.12, point.y);
    ctx.stroke();
  }
}

function drawSmokeLobes(
  ctx: CanvasRenderingContext2D,
  points: readonly CanvasPoint[],
  tile: number,
  time: number,
): void {
  for (const [index, point] of points.entries()) {
    const radius = tile * (0.2 + 0.025 * Math.sin(time + index));
    ctx.beginPath();
    ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
    ctx.fill();
  }
}

function drawCapacityTallies(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
  remaining: number,
): void {
  ctx.setLineDash([]);
  for (let index = 0; index < Math.min(3, remaining); index += 1) {
    const x = point.x + (index - 1) * tile * 0.16;
    ctx.fillRect(x - tile * 0.045, point.y + tile * 0.5, tile * 0.09, tile * 0.22);
  }
}

function drawSuppressionCross(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
): void {
  ctx.setLineDash([]);
  ctx.strokeStyle = 'rgba(226, 232, 240, 0.8)';
  ctx.lineWidth = Math.max(2, tile * 0.06);
  ctx.beginPath();
  ctx.moveTo(point.x - tile * 0.28, point.y - tile * 0.28);
  ctx.lineTo(point.x + tile * 0.28, point.y + tile * 0.28);
  ctx.moveTo(point.x + tile * 0.28, point.y - tile * 0.28);
  ctx.lineTo(point.x - tile * 0.28, point.y + tile * 0.28);
  ctx.stroke();
}

/**
 * A deployed Sentinel reads as a turret, not a glyph: base plate, dome,
 * hull pips, a duration arc, and a barrel that tracks its work — this
 * tick's fire target when it shot, otherwise the nearest enemy in reach.
 * Sentinels fire omnidirectionally without a facing, so the barrel is
 * presentation; the tracer in drawSignatureCombatEvents is the shot.
 */
function drawSentinelTurret(
  input: ArcRelayVisualContext,
  signature: ArcState['visibleSignatures'][number],
  point: { x: number; y: number },
  accent: string,
): void {
  const { ctx, tile, time } = input;
  const tick = input.tick;
  let aim: { x: number; y: number } | null = null;
  for (const event of tick ? [...tick.lifecycleEvents, ...tick.events] : []) {
    const fact = event.arcRelayFact;
    if (
      fact?.kind === 'signature-damage' &&
      fact.operationId === signature.operationId
    ) {
      aim = centre(input, fact.position);
      break;
    }
  }
  if (!aim) {
    let best = Number.POSITIVE_INFINITY;
    for (const pose of input.poses) {
      if (pose.teamId === signature.ownerTeamId) continue;
      const enemy = centre(input, { x: pose.x, y: pose.y });
      const range = Math.hypot(enemy.x - point.x, enemy.y - point.y);
      if (range < best && range <= tile * 5) {
        best = range;
        aim = enemy;
      }
    }
  }
  const angle = aim
    ? Math.atan2(aim.y - point.y, aim.x - point.x)
    : -Math.PI / 2;

  ctx.setLineDash([]);
  ctx.fillStyle = withAlpha(accent, 0.28);
  ctx.strokeStyle = withAlpha(accent, 0.95);
  ctx.lineWidth = Math.max(2, tile * 0.07);
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * 0.58, 0, Math.PI * 2);
  ctx.fill();
  ctx.stroke();

  if (signature.endsAtTick !== null && tick) {
    const total = signature.endsAtTick - signature.startedTick;
    const left = Math.max(0, signature.endsAtTick - tick.tick);
    if (total > 0) {
      ctx.strokeStyle = withAlpha(accent, 0.55);
      ctx.lineWidth = Math.max(1.5, tile * 0.045);
      ctx.beginPath();
      ctx.arc(
        point.x,
        point.y,
        tile * 0.72,
        -Math.PI / 2,
        -Math.PI / 2 + (Math.PI * 2 * left) / total,
      );
      ctx.stroke();
    }
  }

  ctx.strokeStyle = withAlpha('#f8fafc', 0.95);
  ctx.lineWidth = Math.max(4, tile * 0.16);
  ctx.beginPath();
  ctx.moveTo(point.x, point.y);
  ctx.lineTo(
    point.x + Math.cos(angle) * tile * 0.78,
    point.y + Math.sin(angle) * tile * 0.78,
  );
  ctx.stroke();

  ctx.fillStyle = withAlpha('#f8fafc', 0.9);
  ctx.beginPath();
  ctx.arc(
    point.x,
    point.y,
    tile * (0.22 + 0.02 * Math.sin(time * Math.PI * 2)),
    0,
    Math.PI * 2,
  );
  ctx.fill();

  drawDurabilityBar(
    ctx,
    point.x,
    point.y + tile * 0.82,
    tile,
    signature.remainingCapacity,
    signature.maximumCapacity ?? signature.remainingCapacity,
    accent,
  );
}

function drawConstructDurability(
  ctx: CanvasRenderingContext2D,
  point: CanvasPoint,
  tile: number,
  signature: CanvasSignature,
  accent: string,
): void {
  if (signature.maximumCapacity === undefined) return;
  ctx.setLineDash([]);
  drawDurabilityBar(
    ctx,
    point.x,
    point.y + tile * 0.52,
    tile,
    signature.remainingCapacity,
    signature.maximumCapacity,
    accent,
  );
}

function drawDurabilityBar(
  ctx: CanvasRenderingContext2D,
  centreX: number,
  centreY: number,
  tile: number,
  current: number,
  maximum: number,
  accent: string,
): void {
  const health = healthBarState(current, maximum);
  const width = tile * HEALTH_BAR_RELATIVE_WIDTH;
  const height = Math.max(4, tile * 0.075);
  const rim = Math.max(1, tile * 0.018);
  const x = centreX - width / 2;
  const y = centreY - height / 2;
  ctx.fillStyle = withAlpha(accent, 0.95);
  ctx.fillRect(x, y, width, height);
  ctx.fillStyle = 'rgba(7, 16, 23, 0.92)';
  ctx.fillRect(x + rim, y + rim, width - rim * 2, height - rim * 2);
  if (health.fraction <= 0) return;
  ctx.fillStyle = health.color;
  ctx.fillRect(
    x + rim,
    y + rim,
    (width - rim * 2) * health.fraction,
    height - rim * 2,
  );
}

function drawMine(
  ctx: CanvasRenderingContext2D,
  point: { x: number; y: number },
  tile: number,
  accent: string,
  time: number,
): void {
  ctx.setLineDash([]);
  ctx.fillStyle = withAlpha(accent, 0.3);
  ctx.strokeStyle = withAlpha(accent, 0.9);
  ctx.lineWidth = Math.max(2, tile * 0.06);
  ctx.beginPath();
  for (let prong = 0; prong < 8; prong += 1) {
    const spike = (prong * Math.PI) / 4;
    const radius = tile * (prong % 2 === 0 ? 0.38 : 0.2);
    const x = point.x + Math.cos(spike) * radius;
    const y = point.y + Math.sin(spike) * radius;
    if (prong === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  }
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
  ctx.fillStyle = withAlpha('#f8fafc', 0.55 + 0.35 * Math.sin(time * Math.PI * 2));
  ctx.beginPath();
  ctx.arc(point.x, point.y, tile * 0.07, 0, Math.PI * 2);
  ctx.fill();
}

function drawClassSignatureGlyph(
  input: ArcRelayVisualContext,
  owner: ReplayActorIdentity,
  signatureId: string,
  x: number,
  y: number,
  accent: string,
): void {
  const pose = input.poses.find((candidate) => candidate.actorKey === owner.actorKey);
  const look = unitLook(input.replay, owner.unitKey, pose?.formId);
  const image = teamAccentedEffectImage(look, accent);
  if (image?.complete && image.naturalWidth > 0) {
    const size = input.tile * 1.08;
    input.ctx.save();
    input.ctx.globalCompositeOperation = 'source-over';
    input.ctx.drawImage(image, x - size / 2, y - size / 2, size, size);
    input.ctx.restore();
    return;
  }
  drawSignatureGlyph(input.ctx, signatureId, x, y, input.tile, accent);
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
    const recordedBase = centre(input, core.position);
    // The mode snapshot owns possession, while the shared pose owns where the carrier is
    // between its two recorded tiles. Binding the Core to that pose keeps the combined
    // silhouette from snapping ahead of a slowly moving carrier.
    const base = carried
      ? actorCentre(input, core.carrierActor!) ?? recordedBase
      : recordedBase;
    const accent = carried
      ? input.accentFor(core.carrierActor!.unitKey)
      : '#f5f8fb';
    // The lane owns the sphere: a Core reads as its origin wherever it is,
    // loose, carried, or in flight (owner ruling 2026-08-05).
    const originAccent = arcOriginAccent(core.coreId.sourceWellId);
    const radius = input.tile * 0.16;
    let x = base.x;
    let y = base.y;
    if (carried) {
      y -= input.tile * (0.66 + 0.025 * Math.sin(input.time * Math.PI * 1.1));
      input.ctx.save();
      input.ctx.strokeStyle = withAlpha(accent, 0.78);
      input.ctx.lineWidth = Math.max(2, input.tile * 0.05);
      input.ctx.beginPath();
      input.ctx.moveTo(base.x, base.y);
      input.ctx.quadraticCurveTo(base.x, y + input.tile * 0.28, x, y);
      input.ctx.stroke();
      input.ctx.restore();
    } else if (core.disposition === 'in-flight' && core.flightTarget) {
      const target = centre(input, core.flightTarget);
      const releaseOffset =
        input.replay.signaturePresentationId ===
          ARC_SIGNATURE_PRESENTATION_ID
          ? ARC_TOSS_RELEASE_TICK_OFFSET
          : 0;
      const operation = state.visibleSignatures.find((signature) =>
        signature.signatureId === 'arc-toss' &&
        signature.phase === 'in-flight' &&
        signature.completesAtTick === core.flightCompletesAtTick &&
        signature.positions.some((position) =>
          position.x === core.position.x && position.y === core.position.y
        ) &&
        signature.positions.some((position) =>
          position.x === core.flightTarget!.x &&
          position.y === core.flightTarget!.y
        )
      );
      const distance = Math.max(
        Math.abs(core.flightTarget.x - core.position.x),
        Math.abs(core.flightTarget.y - core.position.y),
      );
      const authoritativeStart = operation
        ? operation.startedTick +
          (input.replay.signaturePresentationId ===
              ARC_SIGNATURE_PRESENTATION_ID
            ? 0
            : 1)
        : (core.flightCompletesAtTick ?? Math.floor(input.time) + 1) -
          Math.max(1, Math.ceil(distance / 2));
      const visualStart = authoritativeStart + releaseOffset;
      const visualEnd = core.flightCompletesAtTick ?? visualStart + 1;
      const progress = Math.max(
        0,
        Math.min(
          1,
          (input.time - visualStart) /
            Math.max(0.001, visualEnd - visualStart),
        ),
      );
      x = base.x + (target.x - base.x) * progress;
      y = base.y + (target.y - base.y) * progress -
        Math.sin(progress * Math.PI) * input.tile * 0.42;
      if (input.time < visualStart) y -= input.tile * 0.66;
      if (input.time >= visualStart) {
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
    } else {
      // Keep a loose Core visually anchored to its authoritative tile while lifting the
      // sphere just enough to read as the same object that later rides above a carrier.
      y -= input.tile * 0.12;
    }

    const cracked = (input.tick?.events ?? []).some(
      (event) =>
        event.arcRelayFact?.kind === 'core-dropped' &&
        coreKey(event.arcRelayFact.coreId) === coreKey(core.coreId),
    );
    input.ctx.save();
    input.ctx.shadowColor = originAccent;
    input.ctx.shadowBlur = Math.max(4, input.tile * (carried ? 0.28 : 0.18));
    const atSourceWell = core.disposition === 'loose' && state.wells.some(
      (well) =>
        well.wellId === core.coreId.sourceWellId &&
        well.position.x === core.position.x &&
        well.position.y === core.position.y,
    );
    if (core.disposition === 'loose' && !atSourceWell)
      drawSourceGlyph(
        input.ctx,
        base.x,
        base.y,
        radius,
        core.coreId.sourceWellId,
        originAccent,
        cracked,
      );
    drawCoreSphere(input.ctx, x, y, radius, originAccent);
    if (!carried && coreKey(core.coreId) === threat) {
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

/** Canvas fallback for the WebGL Core: internally lit energy, not a glossy ball. */
function drawCoreSphere(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  radius: number,
  teamAccent: string | null,
): void {
  const glow = ctx.createRadialGradient(x, y, radius * 0.2, x, y, radius * 1.9);
  glow.addColorStop(
    0,
    teamAccent
      ? withAlpha(teamAccent, 0.58)
      : ARC_CORE_NEUTRAL_PALETTE.canvasGlowInner,
  );
  glow.addColorStop(
    0.45,
    teamAccent
      ? withAlpha(teamAccent, 0.3)
      : ARC_CORE_NEUTRAL_PALETTE.canvasGlowMiddle,
  );
  glow.addColorStop(
    1,
    teamAccent
      ? withAlpha(teamAccent, 0)
      : ARC_CORE_NEUTRAL_PALETTE.canvasGlowOuter,
  );
  ctx.save();
  ctx.globalCompositeOperation = 'lighter';
  ctx.fillStyle = glow;
  ctx.beginPath();
  ctx.arc(x, y, radius * 1.9, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();

  const shade = ctx.createRadialGradient(x, y, radius * 0.06, x, y, radius);
  if (teamAccent) {
    shade.addColorStop(0, '#f7fcff');
    shade.addColorStop(0.28, teamAccent);
    shade.addColorStop(0.76, teamAccent);
    shade.addColorStop(1, '#10232a');
  } else {
    shade.addColorStop(0, ARC_CORE_NEUTRAL_PALETTE.canvasCentre);
    shade.addColorStop(0.34, ARC_CORE_NEUTRAL_PALETTE.canvasInner);
    shade.addColorStop(0.72, ARC_CORE_NEUTRAL_PALETTE.canvasMiddle);
    shade.addColorStop(1, ARC_CORE_NEUTRAL_PALETTE.canvasEdge);
  }
  ctx.fillStyle = shade;
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fill();
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

/**
 * The possession beats formerly stated by a banner, now spoken in the world itself.
 * Geometry and cadence are deliberately distinct so the animation still communicates
 * without team colour: birth blooms, a steal snaps inward, a bank locks, a Pulse strikes.
 */
function drawDiegeticEvents(
  input: ArcRelayVisualContext,
  state: ArcState,
): void {
  const { ctx, tile, fraction } = input;
  for (const event of input.tick?.events ?? []) {
    const fact = event.arcRelayFact;
    if (!fact) continue;
    let point: { x: number; y: number } | null = null;
    let accent = '#f5f8fb';
    let secondaryAccent: string | null = null;
    let kind: 'birth' | 'pickup' | 'steal' | 'drop' | 'bank' | 'pulse' | null = null;
    if (fact.kind === 'core-born') {
      point = centre(input, fact.position);
      kind = 'birth';
    } else if (fact.kind === 'core-picked-up') {
      const previousTeam = previousOwnerTeam(input, fact.coreId);
      point = actorCentre(input, fact.carrierActor) ?? centre(input, fact.position);
      accent = input.accentFor(fact.carrierActor.unitKey);
      kind = previousTeam !== null && previousTeam !== fact.carrierActor.teamId
        ? 'steal'
        : 'pickup';
      if (kind === 'steal') secondaryAccent = teamAccent(input, previousTeam!);
    } else if (fact.kind === 'core-dropped') {
      point = centre(input, fact.position);
      kind = 'drop';
    } else if (fact.kind === 'core-banked') {
      point = centre(input, fact.position);
      accent = teamAccent(input, fact.teamId);
      kind = 'bank';
    } else if (fact.kind === 'pulse') {
      const target = state.reactors.find(
        (reactor) => reactor.teamId !== fact.teamId,
      );
      if (target) point = centre(input, target.position);
      accent = teamAccent(input, fact.teamId);
      kind = 'pulse';
    }
    if (!point || !kind) continue;

    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    ctx.strokeStyle = withAlpha(accent, (1 - fraction) * 0.96);
    ctx.fillStyle = withAlpha(accent, (1 - fraction) * 0.15);
    ctx.shadowColor = accent;
    ctx.shadowBlur = Math.max(8, tile * 0.35);
    ctx.lineWidth = Math.max(2.5, tile * 0.075);
    if (kind === 'birth') {
      for (let ring = 0; ring < 3; ring += 1) {
        const progress = Math.max(0, Math.min(1, fraction * 1.4 - ring * 0.14));
        ctx.beginPath();
        ctx.arc(point.x, point.y, tile * (0.18 + progress * 0.7), 0, Math.PI * 2);
        ctx.stroke();
      }
    } else if (kind === 'pickup') {
      const radius = tile * (0.95 - fraction * 0.62);
      ctx.beginPath();
      ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(point.x, point.y, tile * (0.12 + fraction * 0.16), 0, Math.PI * 2);
      ctx.fill();
    } else if (kind === 'steal') {
      const radius = tile * (0.9 - fraction * 0.45);
      if (secondaryAccent) {
        ctx.strokeStyle = withAlpha(secondaryAccent, (1 - fraction) * 0.7);
        ctx.beginPath();
        ctx.arc(point.x, point.y, tile * (0.34 + fraction * 0.58), 0, Math.PI * 2);
        ctx.stroke();
        ctx.strokeStyle = withAlpha(accent, (1 - fraction) * 0.96);
      }
      ctx.setLineDash([tile * 0.16, tile * 0.08]);
      ctx.lineDashOffset = fraction * tile * 1.4;
      ctx.beginPath();
      ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.beginPath();
      ctx.arc(point.x, point.y, tile * (0.28 + 0.15 * fraction), 0, Math.PI * 2);
      ctx.fill();
    } else if (kind === 'drop') {
      ctx.strokeStyle = `rgba(238, 248, 252, ${(1 - fraction) * 0.92})`;
      for (let ring = 0; ring < 2; ring += 1) {
        ctx.beginPath();
        ctx.arc(
          point.x,
          point.y,
          tile * (0.28 + fraction * 0.72 + ring * 0.13),
          0,
          Math.PI * 2,
        );
        ctx.stroke();
      }
      for (let ray = 0; ray < 6; ray += 1) {
        const angle = ray * Math.PI / 3;
        const inner = tile * (0.18 + fraction * 0.2);
        const outer = tile * (0.35 + fraction * 0.56);
        ctx.beginPath();
        ctx.moveTo(point.x + Math.cos(angle) * inner, point.y + Math.sin(angle) * inner);
        ctx.lineTo(point.x + Math.cos(angle) * outer, point.y + Math.sin(angle) * outer);
        ctx.stroke();
      }
    } else if (kind === 'bank') {
      const radius = tile * (0.38 + 0.52 * fraction);
      ctx.beginPath();
      for (let corner = 0; corner < 6; corner += 1) {
        const angle = corner * Math.PI / 3;
        const x = point.x + Math.cos(angle) * radius;
        const y = point.y + Math.sin(angle) * radius;
        if (corner === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      }
      ctx.closePath();
      ctx.stroke();
      for (let pip = 0; pip < 3; pip += 1) {
        ctx.beginPath();
        ctx.arc(
          point.x + (pip - 1) * tile * 0.2,
          point.y,
          tile * (0.05 + 0.035 * (1 - fraction)),
          0,
          Math.PI * 2,
        );
        ctx.fill();
      }
    } else {
      const flash = Math.sin(fraction * Math.PI);
      ctx.lineWidth = Math.max(4, tile * 0.12);
      ctx.beginPath();
      ctx.arc(point.x, point.y, tile * (0.35 + fraction * 1.15), 0, Math.PI * 2);
      ctx.stroke();
      ctx.fillStyle = withAlpha('#ffffff', flash * 0.42);
      ctx.beginPath();
      ctx.arc(point.x, point.y, tile * (0.42 - fraction * 0.18), 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.restore();
  }
}

/** Fact-driven transient; the persistent chevron remains actor-renderer owned. */
function drawLevelUpBeats(input: ArcRelayVisualContext): void {
  const { ctx, tile, time } = input;
  const veterancy = arcVeterancyFor(input.replay);
  for (const pose of input.poses) {
    const beat = veterancy.levelUpAt(
      time,
      pose.teamId,
      pose.unitId,
      pose.lifeId,
    );
    if (!beat) continue;
    const progress = beat.age / LEVEL_UP_BEAT_TICKS;
    const point = centre(input, { x: pose.x, y: pose.y });
    const ringProgress = Math.min(1, beat.age / 0.42);
    const facetProgress = Math.max(0, Math.min(1, (beat.age - 0.16) / 0.64));
    const alpha = Math.max(0, 1 - progress ** 1.45);
    ctx.save();
    ctx.strokeStyle = `rgba(205, 160, 74, ${alpha * 0.82})`;
    ctx.fillStyle = `rgba(205, 160, 74, ${alpha * 0.28})`;
    ctx.lineWidth = Math.max(2, tile * 0.055);
    ctx.beginPath();
    ctx.arc(
      point.x,
      point.y,
      tile * (0.34 + ringProgress * 0.88),
      0,
      Math.PI * 2,
    );
    ctx.stroke();
    for (let index = 0; index < 3; index += 1) {
      const angle = -Math.PI / 2 + (index * Math.PI * 2) / 3;
      const radius = tile * (0.92 - facetProgress * 0.42);
      const x = point.x + Math.cos(angle) * radius;
      const y = point.y + Math.sin(angle) * radius;
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(angle + Math.PI / 2);
      ctx.beginPath();
      ctx.moveTo(0, -tile * 0.13);
      ctx.lineTo(tile * 0.12, tile * 0.11);
      ctx.lineTo(-tile * 0.12, tile * 0.11);
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
      ctx.restore();
    }
    ctx.restore();
  }
}

function previousOwnerTeam(
  input: ArcRelayVisualContext,
  coreId: ReplayArcCoreId,
): number | null {
  const before = input.tick?.before.mode;
  if (before?.kind === 'arc-relay' && 'visibleCores' in before) {
    const core = before.visibleCores.find(
      (candidate) => coreKey(candidate.coreId) === coreKey(coreId),
    );
    if (core?.carrierActor) return core.carrierActor.teamId;
  }
  const beforeTick = input.tick?.tick ?? 0;
  for (let index = beforeTick - 1; index >= 0; index -= 1) {
    for (const event of [...(input.replay.ticks[index]?.events ?? [])].reverse()) {
      const fact = event.arcRelayFact;
      if (!fact || !('coreId' in fact) || coreKey(fact.coreId) !== coreKey(coreId))
        continue;
      if (fact.kind === 'core-picked-up') return fact.carrierActor.teamId;
      if (fact.kind === 'core-handed-off') return fact.targetActor.teamId;
      if (fact.kind === 'core-relocated') return fact.carrierActor?.teamId ?? null;
      if (fact.kind === 'core-dropped') return fact.sourceActor.teamId;
      if (fact.kind === 'core-banked') return fact.teamId;
    }
  }
  return null;
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
