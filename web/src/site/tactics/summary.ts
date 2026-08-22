import { describePlace, WELL_OPTIONS, workshopLabel } from './places';
import type {
  Tactic, TacticCase, TacticConds, TacticGroup, TacticJob, TacticPhaseDef,
  TacticTransition,
} from './tacticsDocument';
import { effectiveJobResolution, jobOf } from './tacticsDocument';

/**
 * The natural-language readback: every Tactic card and wizard review reads its
 * whole operation back — phase by phase — as one paragraph. Ported from the
 * owner-iterated mock; verb agreement and place naming are the point.
 */

const NUMBER_WORDS = ['zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight'];

export function numberWord(value: number): string {
  return NUMBER_WORDS[value] ?? String(value);
}

function verb(word: string, plural: boolean): string {
  if (plural) return word;
  return /(?:ch|sh|s|x)$/.test(word) ? `${word}es` : `${word}s`;
}

/** "Flankers’" rather than "Flankers’s". */
export function possessive(name: string): string {
  return /s$/i.test(name.trim()) ? `${name}’` : `${name}’s`;
}

function inPlace(place: string): string {
  if (place === 'Mid') return 'at Mid';
  if (/(?:Well|Bank)$/.test(place)) return `at ${place}`;
  return /^(your|their|the|Mid)/i.test(place) && !/^behind/i.test(place)
    ? `in ${place}`
    : place;
}

function recoveryPhrase(job: TacticJob): string {
  if (job.recovery === 'heal-up') return ' — topping up fully at the heal tile';
  if (job.recovery === 'heal-tile') return ' — breaking for the heal tile when hurt';
  if (job.recovery === 'fight-on' && !['aggressive', 'steal'].includes(job.posture)) {
    return ' — no falling back';
  }
  return '';
}

function jobPlace(job: TacticJob): string {
  if (job.posts?.length) return describePlace(...job.posts[0].at);
  if (job.rect) {
    return describePlace(
      Math.round((job.rect[0] + job.rect[2]) / 2),
      Math.round((job.rect[1] + job.rect[3]) / 2),
    );
  }
  if (job.path?.length) return describePlace(...job.path[0]);
  if (job.point) return describePlace(...job.point);
  return 'their ground';
}

/** ", coming in by the drawn route" when a via is authored. */
function viaPhrase(job: TacticJob): string {
  return (job.via?.length ?? 0) > 1 ? ', coming in by the drawn route' : '';
}

/** ", answering attacks on the way" when a travel posture is authored. */
function travelPhrase(job: TacticJob): string {
  if (!job.travel) return '';
  if (job.travel === 'hold') return ', answering attacks on the way';
  if (job.travel === 'aggressive') return ', brawling their way in';
  if (job.travel === 'steal') return ', stealing on the way';
  if (job.travel === 'wait') return ', slipping in quietly';
  return '';
}

/** One group's job in one phase, as a verb phrase. */
export function jobClause(job: TacticJob, plural = true): string {
  const v = (word: string) => verb(word, plural);
  const posture = job.posture;
  const via = viaPhrase(job);
  const adverb = {
    aggressive: ' aggressively',
    hold: '',
    steal: ' to steal',
    escort: '',
    observe: ', watching',
    wait: '',
  }[posture] ?? '';
  switch (job.kind) {
    case 'posts': {
      const place = jobPlace(job);
      const ways = job.posts?.some((post) => (post.via?.length ?? 0) > 1)
        ? ', coming in by drawn routes'
        : via;
      if (posture === 'hold') return `${v('hold')} posts at ${place}${ways}${travelPhrase(job)}${recoveryPhrase(job)}`;
      if (posture === 'observe') return `${v('watch')} from posts at ${place}${ways}${travelPhrase(job)}${recoveryPhrase(job)}`;
      return `${v('wait')} at posts near ${place}${ways}${travelPhrase(job)}${recoveryPhrase(job)}`;
    }
    case 'area': {
      const place = jobPlace(job);
      if (posture === 'hold') return `${v('hold')} ground ${inPlace(place)}${via}${travelPhrase(job)}${recoveryPhrase(job)}`;
      if (posture === 'observe') return `${v('keep')} watch ${inPlace(place)}${via}${travelPhrase(job)}${recoveryPhrase(job)}`;
      return `${v('stage')} ${inPlace(place)}${via}${travelPhrase(job)}${recoveryPhrase(job)}`;
    }
    case 'line':
      return `${v('picket')} a line across ${jobPlace(job)}${via}${recoveryPhrase(job)}`;
    case 'approach':
      return `${v('move')} up short of ${jobPlace(job)}${posture === 'steal' ? ', ready to take the Core' : ', quietly'}${via}${recoveryPhrase(job)}`;
    case 'converge': {
      const target = job.point ? describePlace(...job.point) : 'the target';
      // "converge behind their lines", never "converge on behind their lines".
      const joined = /^behind/i.test(target) ? target : `on ${target}`;
      return `${v('converge')} ${joined}${adverb}${viaPhrase(job)}${travelPhrase(job)}${recoveryPhrase(job)}`;
    }
    case 'advance':
      return `${v('push')} ${job.path && job.path.length > 1 ? `toward ${describePlace(...job.path[job.path.length - 1])}` : 'forward'}${adverb}${recoveryPhrase(job)}`;
    case 'patrol':
      return `${v('walk')} their patrol loop${adverb}${recoveryPhrase(job)}`;
    case 'return':
      return `${v('run')} the Core home to your reactor${viaPhrase(job)}`;
    case 'escort':
      return `${v('screen')} the carrier the whole way`;
    case 'train':
      // No drawn ground to name: the Workshop resolves its own training tile.
      return `${v('train')} at ${workshopLabel(job.workshop)}${travelPhrase(job)}${recoveryPhrase(job)}`;
    default:
      return `${v('keep')} at it`;
  }
}

/** The group's whole arc across the phases: "hold posts at Mid, then
 * converge on their Well aggressively, then run the Core home". */
export function groupClause(
  tactic: Tactic,
  group: TacticGroup,
  standalone = false,
): string {
  const count = group.slots.length;
  const plural = count !== 1;
  const subject = standalone
    ? `${numberWord(count).replace(/^\w/, (letter) => letter.toUpperCase())} bot${plural ? 's' : ''}`
    : '';
  const parts: string[] = [];
  tactic.phases.forEach((phase, index) => {
    const job = phase.jobs[group.id];
    if (!job || (index > 0 && job.kind === 'none')) return;
    parts.push(jobClause(job, plural));
  });
  if (parts.length === 0) parts.push(verb('keep', plural) + ' at it');
  return `${subject} ${parts.join(', then ')}`.trim();
}

export function condClauses(conds: TacticConds | undefined): string[] {
  if (!conds) return [];
  const out: string[] = [];
  if (conds.groupsSet) {
    const slack = conds.groupsSet === true ? 0 : conds.groupsSet.allowedLate;
    out.push(slack === 0 ? 'the full formation is ready'
      : slack === 1 ? 'the formation is ready, allowing one late bot'
        : 'the formation is ready, allowing two late bots');
  }
  if (conds.held) out.push(`${conds.held.ticks} ticks have passed here`);
  if (conds.healed) out.push(`everyone is healed to ${conds.healed.percent}%`);
  if (conds.enemiesAt) {
    const count = zonesOf(conds.enemiesAt).length;
    const enemies = conds.enemiesAt.count === 1
      ? 'one enemy was'
      : `${conds.enemiesAt.count} enemies were`;
    out.push(`at least ${enemies} recently seen across ${count === 1 ? 'this area' : `these ${count} areas`}`);
  }
  if (conds.contact) out.push('an enemy makes contact');
  if (conds.coreLoose) {
    const well = WELL_OPTIONS.find((value) => value.id === conds.coreLoose!.well);
    out.push(`a Core is up for grabs at ${well?.label ?? 'a Well'}`);
  }
  if (conds.coreSoon) {
    const well = WELL_OPTIONS.find((value) => value.id === conds.coreSoon!.well);
    out.push(`${well?.label ?? 'a Well'} births its next Core within ${conds.coreSoon.ticks} ticks`);
  }
  if (conds.enemiesDown) {
    out.push(`at least ${numberWord(conds.enemiesDown.count)} enem${conds.enemiesDown.count === 1 ? 'y is' : 'ies are'} down`);
  }
  if (conds.carrier) out.push('we hold a Core');
  if (conds.losses) {
    out.push(`we have lost ${numberWord(conds.losses.count)} bot${conds.losses.count === 1 ? '' : 's'}`);
  }
  if (conds.killsWithin) {
    out.push(`we destroyed ${numberWord(conds.killsWithin.count)} enem${conds.killsWithin.count === 1 ? 'y' : 'ies'} within ${conds.killsWithin.ticks} ticks`);
  }
  if (conds.lossesWithin) {
    out.push(`we lost ${numberWord(conds.lossesWithin.count)} bot${conds.lossesWithin.count === 1 ? '' : 's'} within ${conds.lossesWithin.ticks} ticks`);
  }
  if (conds.kills) {
    out.push(`${numberWord(conds.kills.count)} enem${conds.kills.count === 1 ? 'y is' : 'ies are'} destroyed`);
  }
  if (conds.cleared) {
    const count = zonesOf(conds.cleared).length;
    out.push(`${count === 1 ? 'this area was' : `all ${count} areas were`} fully checked within 20 ticks and no enemy is known to remain`);
  }
  if (conds.notFighting) {
    const count = zonesOf(conds.notFighting).length;
    out.push(`no friendly is currently fighting in ${count === 1 ? 'this area' : `these ${count} areas`}`);
  }
  if (conds.coreAcquired) {
    const well = typeof conds.coreAcquired === 'object'
      ? WELL_OPTIONS.find((value) => value.id === (conds.coreAcquired as { well: string }).well)
      : null;
    out.push(well
      ? `${possessive(well.label)} Core is taken this phase`
      : 'a Core is taken this phase');
  }
  if (conds.coreBanked) out.push('a Core is banked');
  if (conds.coreLost) out.push('the Core is lost');
  if (conds.reached) out.push('the destination is reached');
  if (conds.observed) out.push('the ground has been watched long enough');
  // Workshop rows read the public landmark signal, never a fogged body.
  if (conds.workshopReady) {
    out.push(`${workshopLabel(conds.workshopReady.workshop)} is ready`);
  }
  if (conds.workshopTraining) {
    const where = workshopLabel(conds.workshopTraining.workshop);
    const team = conds.workshopTraining.team ?? 'any';
    out.push(team === 'self'
      ? `we are training at ${where}`
      : team === 'enemy'
        ? `an enemy is training at ${where}`
        : `someone is training at ${where}`);
  }
  if (conds.workshopContested) {
    out.push(`enemies are standing in ${workshopLabel(conds.workshopContested.workshop)}`);
  }
  if (conds.workshopDormant) {
    out.push(`${workshopLabel(conds.workshopDormant.workshop)} is recharging`);
  }
  if (conds.workshopDone) {
    const count = conds.workshopDone.count ?? 1;
    out.push(`we have finished ${numberWord(count)} training${count === 1 ? '' : 's'} at ${workshopLabel(conds.workshopDone.workshop)} this phase`);
  }
  if (conds.veteranLevel) {
    out.push(`bot ${conds.veteranLevel.slot + 1} reaches veteran level ${conds.veteranLevel.level}`);
  }
  return out;
}

function zonesOf(holder: { zones?: unknown[]; zone?: unknown }): unknown[] {
  return holder.zones ?? (holder.zone ? [holder.zone] : []);
}

export function orJoin(list: string[]): string {
  if (list.length <= 1) return list[0] ?? '';
  return `${list.slice(0, -1).join(', ')}, or ${list[list.length - 1]}`;
}

export function andJoin(list: string[]): string {
  if (list.length <= 1) return list[0] ?? '';
  return `${list.slice(0, -1).join(', ')} and ${list[list.length - 1]}`;
}

/** "every Group is in position and enemies gather at Mid" — one case.
 * Legacy trigger rows read as ordinary AND rows in the current editor; OR is
 * represented only by another case. Unless remains a veto if any row holds. */
export function caseClause(value: TacticCase): string {
  const positive = condClauses({ ...(value.triggers ?? {}), ...(value.required ?? {}) });
  const unless = condClauses(value.unless);
  let clause = andJoin(positive);
  if (unless.length && clause.length) {
    clause = `${clause} — unless ${orJoin(unless)}`;
  }
  return clause;
}

export function casesClause(cases: TacticCase[] | undefined): string | null {
  const clauses = (cases ?? [])
    .map(caseClause)
    .filter((clause) => clause.length > 0);
  if (clauses.length === 0) return null;
  return clauses.length === 1
    ? clauses[0]
    : orJoin(clauses.map((clause, index) =>
      clauses.length > 1 && index > 0 ? clause : clause));
}

export function startClause(tactic: Tactic): string | null {
  if (tactic.start.mode !== 'when') return null;
  const clause = casesClause(tactic.start.cases);
  return clause ? `Applies once ${clause}` : null;
}

export function abortClause(tactic: Tactic): string | null {
  const clause = casesClause(tactic.abort?.cases);
  return clause ? `Aborts if ${clause}` : null;
}

export function deliveryClause(
  tactic: Tactic,
): string | null {
  if (tactic.delivery.plan === 'none') return null;
  return tactic.delivery.plan === 'solo'
    ? 'If a Core lands in their hands, the carrier breaks off and banks it while the rest keep at it'
    : 'If a Core lands in their hands, the carrier banks it under escort';
}

export function subjectFor(tactic: Tactic, group: TacticGroup): string {
  if (tactic.groups.length === 1) return 'they';
  const sizes = tactic.groups.map((value) => value.slots.length);
  const unique = sizes.filter((size) => size === group.slots.length).length === 1;
  return unique && group.slots.length > 0
    ? `the ${numberWord(group.slots.length)}`
    : group.name;
}

/** A Group of one doing a job that belongs to a BODY rather than to ground
 * reads better under the bot's own name: "Gantry trains at the north
 * Workshop" beats "One bot trains…". Everything else keeps the counted
 * subject, because a Group is what the player actually assigned. */
function namedLead(
  group: TacticGroup,
  job: TacticJob | undefined,
  botName: (slot: number) => string | null,
): string | null {
  if (job?.kind !== 'train' || group.slots.length !== 1) return null;
  return botName(group.slots[0]);
}

export function summarize(
  tactic: Tactic,
  botName: (slot: number) => string | null,
): string {
  const opening = startClause(tactic);
  const sentences: string[] = [];

  // The first phase: every group's job, joined "while".
  const first = tactic.phases[0];
  if (first) {
    const jobs = tactic.groups.map((group, index) => {
      const count = group.slots.length;
      const job = first.jobs[group.id]
        ?? { kind: 'none' as const, posture: 'wait' as const, recovery: 'patch' as const };
      const lead = namedLead(group, job, botName)
        ?? (index === 0
          ? `${numberWord(count).replace(/^\w/, (letter) => letter.toUpperCase())} bot${count === 1 ? '' : 's'}`
          : numberWord(count));
      return `${lead} ${jobClause(job, count !== 1)}`;
    });
    sentences.push(jobs.length === 1
      ? `${jobs[0]}.`
      : `${jobs.slice(0, -1).join(', ')} while ${jobs[jobs.length - 1]}.`);
    const custody = custodyClause(tactic.phases[0]);
    if (custody) sentences.push(custody);
  }

  // Each later phase: its entry condition, its name, and what changes.
  tactic.phases.forEach((phase, index) => {
    if (index === 0) return;
    const previous = tactic.phases[index - 1];
    const rule = (previous.continue ?? [])[0];
    const when = rule ? caseClause(rule) : '';
    const movers = tactic.groups
      .filter((group) => (phase.jobs[group.id]?.kind ?? 'none') !== 'none')
      .map((group) =>
        `${namedLead(group, phase.jobs[group.id], botName) ?? subjectFor(tactic, group)} ${jobClause(phase.jobs[group.id], group.slots.length !== 1)}`);
    const change = movers.length > 0
      ? movers.join('; ')
      : 'everyone carries on';
    sentences.push(`${when ? `Once ${when} — ` : 'Then '}${phase.name}: ${change}.`);
    const custody = custodyClause(phase);
    if (custody) sentences.push(custody);
  });

  // How it ends: finish transitions, duration, abort.
  const finishes = (tactic.phases[tactic.phases.length - 1]?.continue ?? [])
    .map((rule) => caseClause(rule))
    .filter((clause) => clause.length > 0);
  if (finishes.length) {
    sentences.push(`Finish when ${orJoin(finishes)}${tactic.maxDuration && tactic.maxDuration !== 'none' ? ' — or the time limit expires' : ''}.`);
  } else if (tactic.maxDuration && tactic.maxDuration !== 'none') {
    sentences.push('Finish when the time limit expires.');
  } else {
    const last = tactic.phases[tactic.phases.length - 1];
    if (last && (last.continue ?? []).length === 0) {
      sentences.push('It holds there until a Tactic-wide abort or the match decides otherwise.');
    }
  }
  const abort = abortClause(tactic);
  if (abort) sentences.push(`${abort}.`);
  const delivery = deliveryClause(tactic);
  return `${opening ? `${opening}. ` : ''}${sentences.join(' ')}${delivery ? ` ${delivery}.` : ''}`;
}

/** A phase's Core custody override, as its own short sentence. */
function custodyClause(phase: TacticPhaseDef | undefined): string | null {
  const plan = phase?.custody?.plan;
  if (!plan) return null;
  const way = phase?.custody?.via?.length ? ' by the drawn way home' : '';
  if (plan === 'keep') return 'A carrier keeps the Core in hand here.';
  if (plan === 'escort') return `A carrier banks under escort${way}.`;
  return `A carrier breaks off and banks alone${way}.`;
}

/** Legacy alias kept for callers that read one phase's rule aloud. */
export function transitionClause(
  tactic: Tactic,
  rule: TacticTransition,
): string {
  const clause = caseClause(rule);
  const final = tactic.phases[tactic.phases.length - 1]?.continue?.includes(rule) ?? false;
  if (final) {
    return clause ? `Finishes when ${clause}` : 'Finishes';
  }
  return clause ? `Continues to the next phase once ${clause}` : 'Continues to the next phase';
}

/** The group's job in phase context — used by drawer section headers. */
export function jobLabelFor(tactic: Tactic, phaseIndex: number, groupId: string): string {
  const resolved = effectiveJobResolution(tactic, phaseIndex, groupId);
  const effective = resolved?.job ?? null;
  const raw = jobOf(tactic, phaseIndex, groupId);
  if (!raw) return 'no job';
  if (raw.kind === 'none' && effective && resolved) {
    const phase = tactic.phases[resolved.sourcePhaseIndex]?.name
      ?? `Phase ${resolved.sourcePhaseIndex + 1}`;
    return `carries on ${effective.kind} from ${phase}`;
  }
  return raw.kind;
}
