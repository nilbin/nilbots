import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
import {
  presentationAccent,
  presentationBotLook,
  presentationProjectileLook,
  type BotLook,
  type ProjectileLook,
} from './arenaThemes';

/**
 * Which artwork and which colour a stable unit wears, resolved in one place.
 *
 * Both renderers, the bot panel and the hosted bridge used to answer this independently —
 * `botLook(participant.lookId, visualIndex)` plus `participant.accent`, four times over.
 * That was fine while a replay's presentation came entirely from the submitting bot, and
 * it stopped being fine with generation-3 class arms: those replays carry
 * `header.presentation === null`, every participant arrives with the same default
 * `lookId` and the same accent, and the form catalog — striker/bulwark/fabricator plus
 * their class-owned forms — is the only thing in the document that distinguishes one
 * machine from another. Rendered from participant data alone, a bulwark, a striker and
 * both teams are the same picture.
 *
 * So resolution is layered, and every layer is data the replay actually carries:
 *
 * 1. **Authored per-form presentation** (`replay.forms[].lookId`, from the replay
 *    header's presentation section) always wins. Once current class contracts author
 *    these IDs, the compatibility mapping below stops being reachable.
 * 2. **The class-form presentation below** — deterministic from the form ID for the Labs
 *    replays that predate authored presentation metadata.
 * 3. **The submitting participant's own look**, which is what shipped duels use.
 * 4. The legacy slot look, for replay-v1 documents that predate look IDs.
 *
 * Accent follows the same principle: a participant accent that already tells the teams
 * apart is left exactly as authored, and the per-team palette is reached for only when
 * the accents in the document do not distinguish one team from another.
 */

/**
 * Real class-owned presentation, separate from player cosmetics.
 *
 * The current Labs replay writer carries no per-form presentation block, so this is the
 * compatibility bridge from its exact form IDs to the selected class identities. The
 * assets live in the internal class roots: they can render without appearing in the
 * appearance editor or becoming equippable on an unrelated class. A future replay that
 * authors form presentation still wins at layer one above.
 *
 * Prime and child bodies deliberately share one chassis per class. Fabrication creates a
 * separate identical Lattice Loom; it never grows a child on the source body. Only
 * Bulwark owns an emplacement. The two directional stances are distinct third bodies:
 * Trident Wasp Volley carries the three launch lanes, and Aegis Tortoise Shell ends its
 * physical guard at the protected quadrant's exact ±45° edges.
 */
const CLASS_FORM_PRESENTATION: ReadonlyMap<
  string,
  {
    readonly mobile: string;
    readonly projectile: string;
    readonly emplaced?: string;
    readonly stance?: string;
  }
> = new Map([
  [
    'striker',
    {
      mobile: 'trident-wasp',
      projectile: 'trident-spark',
      stance: 'trident-wasp-volley',
    },
  ],
  [
    'bulwark',
    {
      mobile: 'aegis-tortoise',
      projectile: 'rebound-diamond',
      emplaced: 'aegis-tortoise-turret',
      stance: 'aegis-tortoise-shell',
    },
  ],
  [
    'fabricator',
    {
      mobile: 'lattice-loom',
      projectile: 'lattice-rivet',
    },
  ],
  ...[
    'kestrel',
    'palisade',
    'towline',
    'patchbay',
    'lantern',
    'mortar',
    'minesmith',
    'hush',
    'relay',
    'switchback',
    'longshot',
    'mason',
    'sunder',
    'repulsor',
    'veil',
    'nest',
  ].map(
    (classId) =>
      [
        classId,
        { mobile: `arc-${classId}`, projectile: 'arc-pulse' },
      ] as const,
  ),
]);

const ARC_RELAY_FORM_PREFIX = 'arc-body-';

/**
 * The same-life stances the class-skill kit adds, keyed by the token the engine appends
 * to the source form ID (`striker-prime` → `striker-prime-volley-stance`).
 *
 * Read off the form ID because that is the only thing in the document that says which
 * stance this is: the contract's own `projectileGuard` and `volley` blocks sit on the
 * form and attack profile, and neither survives into the version-neutral model. The
 * token is contract-visible and exact, and an unknown one simply is not a stance here —
 * it falls through to the emplacement treatment rather than being guessed at.
 */
export type StanceKind = 'volley' | 'aegis';

const STANCE_TOKENS: readonly (readonly [string, StanceKind])[] = [
  ['volley-stance', 'volley'],
  ['aegis-shell', 'aegis'],
];

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
  const family = formId.startsWith(ARC_RELAY_FORM_PREFIX)
    ? formId.slice(ARC_RELAY_FORM_PREFIX.length)
    : formId.split('-', 1)[0];
  return CLASS_FORM_PRESENTATION.has(family) ? family : null;
}

/** Is this form ID one of the emplaced (turret) variants of a class family? */
export function isEmplacedFormId(formId: string | null | undefined): boolean {
  return typeof formId === 'string' && formId.endsWith('-turret');
}

/**
 * Which same-life stance a form ID names, or null when it names no stance.
 *
 * Deliberately independent of the class family: a stance token is the whole claim, so a
 * future family reusing one renders like the stance it is rather than like nothing.
 */
export function stanceKindForForm(
  formId: string | null | undefined,
): StanceKind | null {
  if (typeof formId !== 'string') return null;
  for (const [token, kind] of STANCE_TOKENS)
    if (formId.endsWith(`-${token}`)) return kind;
  return null;
}

/** The class-owned look for a class form, turret and stance variants included, or null. */
export function fallbackLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  if (family === null) return null;
  const looks = CLASS_FORM_PRESENTATION.get(family)!;
  if (stanceKindForForm(formId) !== null)
    return looks.stance ?? looks.mobile;
  return isEmplacedFormId(formId)
    ? (looks.emplaced ?? looks.mobile)
    : looks.mobile;
}

/** The class family's emplaced look, whichever form was asked about. */
export function fallbackEmplacedLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  return family === null
    ? null
    : (CLASS_FORM_PRESENTATION.get(family)!.emplaced ?? null);
}

/** The class family's stance look, or null when it has no stance. */
export function fallbackStanceLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  return family === null
    ? null
    : (CLASS_FORM_PRESENTATION.get(family)!.stance ?? null);
}

/** The paired class-owned projectile, or null for a non-class form. */
export function fallbackProjectileLookIdForForm(
  formId: string | null | undefined,
): string | null {
  const family = classFamilyForForm(formId);
  return family === null
    ? null
    : CLASS_FORM_PRESENTATION.get(family)!.projectile;
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

/** The projectile ID the replay itself authored for a form, when it authored one. */
function authoredProjectileLookIdForForm(
  replay: ReplayModel,
  formId: string | null | undefined,
): string | null {
  if (!formId) return null;
  return (
    replay.forms.find((form) => form.formId === formId)
      ?.projectileLookId ?? null
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
  return presentationBotLook(
    resolved,
    visualIndexForUnit(replay, unitKey),
  );
}

/** The look a unit's emplaced (turret) form wears, or null when it has no class family. */
export function unitEmplacedLook(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): BotLook | null {
  const effectiveFormId =
    formId ?? defaultFormIdForUnit(replay, unitKey);
  if (classFamilyForForm(effectiveFormId) === null) return null;
  const emplacedFormId = isEmplacedFormId(effectiveFormId)
    ? effectiveFormId
    : `${effectiveFormId}-turret`;
  const resolved =
    authoredLookIdForForm(replay, emplacedFormId) ??
    fallbackEmplacedLookIdForForm(effectiveFormId);
  return resolved === null
    ? null
    : presentationBotLook(
        resolved,
        visualIndexForUnit(replay, unitKey),
      );
}

/**
 * The stance form a unit can enter from a given form, read out of the replay's own
 * catalog rather than assembled from a naming rule.
 *
 * The engine names a stance form `<source form>-<stance token>`, so the catalog entry is
 * findable by prefix — and finding it, rather than composing the string, is what keeps a
 * unit whose ruleset carries no stance from being handed one. The 2.5D renderer needs
 * this because it builds a unit's bodies once, before the life has worn any of them.
 */
export function stanceFormForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): { readonly formId: string; readonly kind: StanceKind } | null {
  const effectiveFormId =
    formId ?? defaultFormIdForUnit(replay, unitKey);
  if (!effectiveFormId) return null;
  const own = stanceKindForForm(effectiveFormId);
  if (own !== null) return { formId: effectiveFormId, kind: own };
  for (const form of replay.forms) {
    if (!form.formId.startsWith(`${effectiveFormId}-`)) continue;
    const kind = stanceKindForForm(form.formId);
    if (kind !== null) return { formId: form.formId, kind };
  }
  return null;
}

/** The look a unit's stance form wears, or null when its ruleset gives it no stance. */
export function unitStanceLook(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): BotLook | null {
  const stance = stanceFormForUnit(replay, unitKey, formId);
  if (stance === null) return null;
  const resolved =
    authoredLookIdForForm(replay, stance.formId) ??
    fallbackStanceLookIdForForm(stance.formId);
  return resolved === null
    ? null
    : presentationBotLook(
        resolved,
        visualIndexForUnit(replay, unitKey),
      );
}

/**
 * The projectile a unit fires.
 *
 * An authored per-form projectile is exact replay data and wins. The class-owned pair is
 * next for old class replays whose presentation block is absent. Everywhere else the
 * participant's snapshotted cosmetic remains the fallback, preserving Duel playback.
 */
export function unitProjectileLook(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  formId?: string | null,
): ProjectileLook {
  const effectiveFormId =
    formId ?? defaultFormIdForUnit(replay, unitKey);
  const participant = participantForUnit(replay, unitKey);
  const resolved =
    authoredProjectileLookIdForForm(replay, effectiveFormId) ??
    fallbackProjectileLookIdForForm(effectiveFormId) ??
    participant?.projectileLookId ??
    undefined;
  return presentationProjectileLook(resolved);
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
