import { describePlace, WELL_OPTIONS } from './places';
import type {
  Tactic, TacticConds, TacticGroup, TacticOutcomes, TacticPhase,
} from './tacticsDocument';

/**
 * The natural-language readback: every Tactic card and wizard review reads its
 * whole operation back as one paragraph. Ported from the owner-iterated mock;
 * verb agreement and place naming are the point, so change wording here only
 * with the mock's sentences beside you.
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

function recoveryPhrase(phase: TacticPhase): string {
  if (phase.recovery === 'heal-tile') return ' — breaking for the heal tile when hurt';
  if (phase.recovery === 'fight-on' && !['aggressive', 'steal'].includes(phase.posture)) {
    return ' — no falling back';
  }
  return '';
}

function stagePlace(group: TacticGroup): string {
  const stage = group.stage;
  if (stage.posts?.length) return describePlace(...stage.posts[0].at);
  if (stage.rect) {
    return describePlace(
      Math.round((stage.rect[0] + stage.rect[2]) / 2),
      Math.round((stage.rect[1] + stage.rect[3]) / 2),
    );
  }
  if (stage.path?.length) return describePlace(...stage.path[0]);
  if (stage.point) return describePlace(...stage.point);
  return 'their ground';
}

export function stageClause(group: TacticGroup, plural = true): string {
  const stage = group.stage;
  const place = stagePlace(group);
  const posture = stage.posture;
  const v = (word: string) => verb(word, plural);
  const via = viaPhrase(stage);
  switch (stage.kind) {
    case 'posts':
      if (posture === 'hold') return `${v('hold')} posts at ${place}${via}${recoveryPhrase(stage)}`;
      if (posture === 'observe') return `${v('watch')} from posts at ${place}${via}${recoveryPhrase(stage)}`;
      return `${v('wait')} at posts near ${place}${via}${recoveryPhrase(stage)}`;
    case 'area':
      if (posture === 'hold') return `${v('hold')} ground ${inPlace(place)}${via}${recoveryPhrase(stage)}`;
      if (posture === 'observe') return `${v('keep')} watch ${inPlace(place)}${via}${recoveryPhrase(stage)}`;
      return `${v('stage')} ${inPlace(place)}${via}${recoveryPhrase(stage)}`;
    case 'line':
      return `${v('picket')} a line across ${place}${via}${recoveryPhrase(stage)}`;
    case 'approach':
      return `${v('move')} up short of ${place}${posture === 'steal' ? ', ready to take the Core' : ', quietly'}${via}${recoveryPhrase(stage)}`;
    default:
      return (group.act.kind === 'escort'
        ? `${v('form')} on the carrier`
        : `${v('start')} loose`) + recoveryPhrase(stage);
  }
}

/** ", coming in by the drawn route" when a via is authored. */
function viaPhrase(phase: TacticGroup['stage']): string {
  return (phase.via?.length ?? 0) > 1 ? ', coming in by the drawn route' : '';
}

export function actClause(group: TacticGroup, plural = true): string {
  const act = group.act;
  const v = (word: string) => verb(word, plural);
  const adverb = {
    aggressive: ' aggressively',
    hold: ` and ${v('hold')}`,
    steal: ' to steal',
    escort: '',
    observe: ', watching',
    wait: '',
  }[act.posture] ?? '';
  switch (act.kind) {
    case 'converge': {
      const target = act.point ? describePlace(...act.point) : 'the target';
      // "converge behind their lines", never "converge on behind their lines".
      const joined = /^behind/i.test(target) ? target : `on ${target}`;
      return `${v('converge')} ${joined}${adverb}${viaPhrase(act)}${recoveryPhrase(act)}`;
    }
    case 'advance':
      return `${v('push')} ${act.path && act.path.length > 1 ? `toward ${describePlace(...act.path[act.path.length - 1])}` : 'forward'}${adverb}${recoveryPhrase(act)}`;
    case 'patrol':
      return `${v('walk')} their patrol loop${adverb}${recoveryPhrase(act)}`;
    case 'return':
      return `${v('run')} the Core home to your reactor${viaPhrase(act)}`;
    case 'escort':
      return `${v('screen')} the carrier the whole way`;
    default:
      return `${v('keep')} at it`;
  }
}

export function groupClause(group: TacticGroup, standalone = false): string {
  const count = group.slots.length;
  const plural = count !== 1;
  const subject = standalone
    ? `${numberWord(count).replace(/^\w/, (letter) => letter.toUpperCase())} bot${plural ? 's' : ''}`
    : '';
  const stage = stageClause(group, plural);
  const act = group.act.kind === 'none' ? '' : `, then ${actClause(group, plural)}`;
  return `${subject} ${stage}${act}`.trim();
}

export function condClauses(conds: TacticConds | undefined): string[] {
  if (!conds) return [];
  const out: string[] = [];
  if (conds.enemiesAt) {
    const [x0, y0, x1, y1] = conds.enemiesAt.zone.rect;
    const place = describePlace(Math.round((x0 + x1) / 2), Math.round((y0 + y1) / 2));
    out.push(`at least ${conds.enemiesAt.count} enemies are known ${inPlace(place)}`);
  }
  if (conds.contact) out.push('an enemy makes contact');
  if (conds.coreLoose) {
    const well = WELL_OPTIONS.find((value) => value.id === conds.coreLoose!.well);
    out.push(`a Core is up for grabs at ${well?.label ?? 'a Well'}`);
  }
  if (conds.carrier) out.push('we hold a Core');
  if (conds.losses) {
    out.push(`we have lost ${numberWord(conds.losses.count)} bot${conds.losses.count === 1 ? '' : 's'}`);
  }
  return out;
}

export function orJoin(list: string[]): string {
  if (list.length <= 1) return list[0] ?? '';
  return `${list.slice(0, -1).join(', ')}, or ${list[list.length - 1]}`;
}

export function startClause(tactic: Tactic): string | null {
  if (tactic.start.mode !== 'when') return null;
  const conds = condClauses(tactic.start.conds);
  if (conds.length === 0) return null;
  return `Applies once ${orJoin(conds)}`;
}

export function releaseClause(tactic: Tactic): string {
  const release = tactic.release;
  const many = tactic.groups.length > 1;
  const groupsWord = tactic.groups.length === 2 ? 'both Groups'
    : tactic.groups.length === 3 ? 'all three Groups' : 'the Group';
  if (release.mode === 'immediate') return 'They go immediately';
  if (release.mode === 'ready') {
    return `They go once ${many ? `${groupsWord} are` : 'everyone is'} in position`;
  }
  const conds = condClauses(release.conds);
  if (conds.length === 0) {
    return `They go once ${many ? `${groupsWord} are` : 'everyone is'} in position`;
  }
  return `They go once ${many ? `${groupsWord} are` : 'everyone is'} ready and ${orJoin(conds)}`;
}

export function deliveryClause(
  tactic: Tactic,
  botName: (slot: number) => string | null,
): string | null {
  if (tactic.delivery.plan === 'none') return null;
  const names = (tactic.delivery.preferredSlots ?? [])
    .map(botName)
    .filter((name): name is string => Boolean(name));
  const preference = names.length ? ` — ${names.join(' and ')} by preference` : '';
  return tactic.delivery.plan === 'solo'
    ? `If a Core lands in their hands, the carrier breaks off and banks it while the rest keep at it${preference}`
    : `If a Core lands in their hands, the carrier banks it under escort${preference}`;
}

export function outcomeLabels(outcomes: TacticOutcomes): string[] {
  const out: string[] = [];
  if (outcomes.kills) {
    out.push(`${numberWord(outcomes.kills.count)} enem${outcomes.kills.count === 1 ? 'y' : 'ies'} destroyed`);
  }
  if (outcomes.losses) {
    out.push(`we have lost ${numberWord(outcomes.losses.count)} bot${outcomes.losses.count === 1 ? '' : 's'}`);
  }
  if (outcomes.cleared) {
    const [x0, y0, x1, y1] = outcomes.cleared.zone.rect;
    const place = outcomes.cleared.zone.name === 'mid' ? 'Mid'
      : describePlace(Math.round((x0 + x1) / 2), Math.round((y0 + y1) / 2));
    out.push(`${place.replace(/^\w/, (letter) => letter.toUpperCase())} cleared`);
  }
  if (outcomes.coreAcquired) out.push('a Core is acquired');
  if (outcomes.coreBanked) out.push('a Core is banked');
  if (outcomes.coreLost) out.push('the Core is lost');
  if (outcomes.reached) out.push('the destination is reached');
  if (outcomes.observed) out.push('the ground has been watched long enough');
  return out;
}

export function subjectFor(tactic: Tactic, group: TacticGroup): string {
  if (tactic.groups.length === 1) return 'they';
  const sizes = tactic.groups.map((value) => value.slots.length);
  const unique = sizes.filter((size) => size === group.slots.length).length === 1;
  return unique && group.slots.length > 0
    ? `the ${numberWord(group.slots.length)}`
    : group.name;
}

export function summarize(
  tactic: Tactic,
  botName: (slot: number) => string | null,
): string {
  const opening = startClause(tactic);
  const stages = tactic.groups.map((group, index) => {
    const count = group.slots.length;
    const lead = index === 0
      ? `${numberWord(count).replace(/^\w/, (letter) => letter.toUpperCase())} bot${count === 1 ? '' : 's'}`
      : numberWord(count);
    return `${lead} ${stageClause(group, count !== 1)}`;
  });
  const stageSentence = stages.length === 1
    ? `${stages[0]}.`
    : `${stages.slice(0, -1).join(', ')} while ${stages[stages.length - 1]}.`;

  const actors = tactic.groups.filter((group) => group.act.kind !== 'none');
  const holders = tactic.groups.filter((group) => group.act.kind === 'none');
  let middle = '';
  if (actors.length) {
    const actParts = actors.map((group) =>
      `${subjectFor(tactic, group)} ${actClause(group, group.slots.length !== 1)}`);
    const holdPart = holders.length && tactic.groups.length > 1
      ? ` while ${holders.map((group) => group.name).join(' and ')} keep${holders.length === 1 ? 's' : ''} holding`
      : '';
    const when = releaseClause(tactic)
      .replace(/^They go once /, 'Once ')
      .replace(/^They go immediately/, 'Immediately');
    middle = ` ${when}, ${actParts.join('; ')}${holdPart}.`;
  } else if (tactic.release.mode !== 'immediate') {
    middle = ` ${releaseClause(tactic)}.`;
  }

  const on = outcomeLabels(tactic.finish.outcomes);
  let finish = '';
  if (on.length) {
    finish = ` Finish when ${orJoin(on)}${tactic.finish.maxAttempt !== 'none' ? ' — or the attempt expires' : ''}.`;
  } else if (tactic.finish.maxAttempt !== 'none') {
    finish = ' Finish when the attempt expires.';
  }
  if (tactic.finish.outcomes.persistent) {
    finish += ' Otherwise it persists until a higher Tactic takes over.';
  }
  const delivery = deliveryClause(tactic, botName);
  return `${opening ? `${opening}. ` : ''}${stageSentence}${middle}${finish}${delivery ? ` ${delivery}.` : ''}`;
}
