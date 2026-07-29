import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
import { botLook, presentationAccent, type BotLook } from './arenaThemes';

/**
 * Which artwork and which colour a stable unit wears, resolved in one place.
 *
 * Both renderers, the bot panel and the hosted bridge used to answer this independently —
 * `botLook(participant.lookId, visualIndex)` plus `participant.accent`, four times over.
 * That was fine while a replay's presentation came entirely from the submitting bot, and
 * it stopped being fine with generation-3 class arms: those replays carry
 * `header.presentation === null`, every participant arrives with the same default
 * `lookId` and the same accent, and the form catalog — striker/bulwark/fabricator, each
 * with its own turret — is the only thing in the document that distinguishes one machine
 * from another. Rendered from participant data alone, a bulwark, a striker and both teams
 * are the same picture.
 *
 * So resolution is layered, and every layer is data the replay actually carries:
 *
 * 1. **Authored per-form presentation** (`replay.forms[].lookId`, from the replay
 *    header's presentation section) always wins. When proper per-class art ships it
 *    lands here and everything below stops being reachable.
 * 2. **The class-form fallback below** — deterministic from the form ID, and clearly
 *    marked as a stand-in.
 * 3. **The submitting participant's own look**, which is what shipped duels use.
 * 4. The legacy slot look, for replay-v1 documents that predate look IDs.
 *
 * Accent follows the same principle: a participant accent that already tells the teams
 * apart is left exactly as authored, and the per-team palette is reached for only when
 * the accents in the document do not distinguish one team from another.
 */

/**
 * **Fallback art. Not a catalog, not a cosmetic, and not the eventual answer.**
 *
 * Generation-3 class rulesets name their forms `<family>-<role>` and
 * `<family>-<role>-turret`. None of those families has artwork yet, so every one of them
 * resolves to the same default chassis and a match between two classes is unreadable.
 * Mapping the family onto looks the catalog already ships is the smallest honest fix:
 * nothing is invented, nothing is baked into the replay, and the moment a replay carries
 * real per-form presentation this table is skipped entirely.
 *
 * The picks are about silhouette, not flavour. `needle` is a thin dart and reads as a
 * fast skirmisher; `aureate-warden` is broad and heavy; `mantis` carries forward limbs
 * and reads as a builder. The emplaced picks are the two radially symmetric chassis in
 * the library — a shape with no front, which is exactly what an objective-weight-zero
 * turret that sees and fires in every direction is.
 */
const CLASS_FORM_LOOK_FALLBACK: ReadonlyMap<
  string,
  { readonly mobile: string; readonly emplaced: string }
> = new Map([
  ['striker', { mobile: 'needle', emplaced: 'orbiter' }],
  ['bulwark', { mobile: 'aureate-warden', emplaced: 'bulwark' }],
  ['fabricator', { mobile: 'mantis', emplaced: 'orbiter' }],
]);

/**
 * Team colours used only when the replay's own accents cannot tell the teams apart.
 *
 * Ordered by team ID, and deliberately opening on the blue/orange pair: it survives the
 * common colour-vision deficiencies, which a cyan/green or a red/green pair does not.
 */
const TEAM_ACCENT_FALLBACK = [
  '#38bdf8',
  '#fb923c',
  '#a78bfa',
  '#4ade80',
  '#f472b6',
  '#facc15',
] as const;

const DEFAULT_ACCENT = '#38bdf8';

/**
 * The class family a generic form ID belongs to, or null when the ID is not shaped like
 * one. Legacy form IDs (`legacy-mobile`, `prime-mobile`, `child-mobile`, `turret`) have
 * no family here on purpose — they already render distinctly and must keep their pixels.
 */
export function classFamilyForForm(
  formId: string | null | undefined,
): string | null {
  if (!formId) return null;
  const family = formId.split('-', 1)[0];
  return CLASS_FORM_LOOK_FALLBACK.has(family) ? family : null;
}

/** Is this form ID one of the emplaced (turret) variants of a class family? */
export function isEmplacedFormId(formId: string | null | undefined): boolean {
  return typeof formId === 'string' && formId.endsWith('-turret');
}

/** The stand-in look for a class form, turret variants included, or null. */
export function fallbackLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  if (family === null) return null;
  const looks = CLASS_FORM_LOOK_FALLBACK.get(family)!;
  return isEmplacedFormId(formId) ? looks.emplaced : looks.mobile;
}

/**
 * The stand-in look for a class family's emplaced form, whichever form was asked about.
 *
 * The 2.5D renderer builds a unit's turret once, from a form it may not be wearing yet,
 * so it needs the emplaced look of the family rather than of the current form.
 */
export function fallbackEmplacedLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  return family === null
    ? null
    : CLASS_FORM_LOOK_FALLBACK.get(family)!.emplaced;
}

/** The look ID the replay itself authored for a form, when it authored one. */
function authoredLookIdForForm(
  replay: ReplayModel,
  formId: string | null | undefined,
): string | null {
  if (!formId) return null;
  return (
    replay.forms.find((form) => form.formId === formId)?.lookId ?? null
  );
}

/**
 * The form a unit should be drawn as when the caller has no per-tick form in hand — its
 * authoritative starting form, which for a class arm fixes the family for the match.
 */
export function defaultFormIdForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): string | null {
  const unit = replay.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  return (
    unit?.initialFormId ??
    replay.initialWorld?.units.find(
      (candidate) => candidate.unitKey === unitKey,
    )?.defaultFormId ??
    null
  );
}

/** The look a unit wears, optionally for one specific effective form. */
export function unitLook(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): BotLook {
  const effectiveFormId =
    formId ?? defaultFormIdForUnit(replay, unitKey);
  const participant = participantForUnit(replay, unitKey);
  const resolved =
    authoredLookIdForForm(replay, effectiveFormId) ??
    fallbackLookIdForForm(effectiveFormId) ??
    participant?.lookId ??
    undefined;
  return botLook(resolved, visualIndexForUnit(replay, unitKey));
}

/** The look a unit's emplaced (turret) form wears, or null when it has no class family. */
export function unitEmplacedLook(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): BotLook | null {
  const effectiveFormId =
    formId ?? defaultFormIdForUnit(replay, unitKey);
  const family = classFamilyForForm(effectiveFormId);
  if (family === null) return null;
  const emplacedFormId = isEmplacedFormId(effectiveFormId)
    ? effectiveFormId
    : `${effectiveFormId}-turret`;
  const resolved =
    authoredLookIdForForm(replay, emplacedFormId) ??
    fallbackEmplacedLookIdForForm(effectiveFormId) ??
    undefined;
  return resolved === undefined
    ? null
    : botLook(resolved, visualIndexForUnit(replay, unitKey));
}

const teamAccentCache = new WeakMap<
  ReplayModel,
  ReadonlyMap<number, string> | null
>();

/**
 * A per-team accent, or null when the replay's own participant accents already separate
 * the teams and must be left alone.
 *
 * Two teams that both submitted `#22d3ee` are the case this exists for: the accent is the
 * one thing carried consistently through pips, vision halos, beams, impact light and the
 * pool under a bot, so when it collapses the whole arena collapses with it.
 */
function teamAccentOverride(
  replay: ReplayModel,
): ReadonlyMap<number, string> | null {
  const cached = teamAccentCache.get(replay);
  if (cached !== undefined) return cached;

  const byTeam = new Map<number, Set<string>>();
  for (const participant of replay.participants) {
    const accents =
      byTeam.get(participant.teamId) ?? new Set<string>();
    accents.add((participant.accent || DEFAULT_ACCENT).toLowerCase());
    byTeam.set(participant.teamId, accents);
  }
  const teamIds = [...byTeam.keys()].sort((left, right) => left - right);
  const distinguishes =
    teamIds.length > 1 &&
    teamIds.every((teamId) => byTeam.get(teamId)!.size === 1) &&
    new Set(teamIds.map((teamId) => [...byTeam.get(teamId)!][0])).size ===
      teamIds.length;

  const override = distinguishes
    ? null
    : new Map(
        teamIds.map((teamId, index) => [
          teamId,
          TEAM_ACCENT_FALLBACK[index % TEAM_ACCENT_FALLBACK.length],
        ]),
      );
  teamAccentCache.set(replay, override);
  return override;
}

/** The raw accent a unit is drawn in, before any background adaptation. */
export function unitAccent(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): string {
  const unit = replay.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  const participant = participantForUnit(replay, unitKey);
  const override =
    unit === undefined
      ? undefined
      : teamAccentOverride(replay)?.get(unit.teamId);
  return presentationAccent(
    unitLook(replay, unitKey, formId),
    override ?? participant?.accent ?? DEFAULT_ACCENT,
  );
}
