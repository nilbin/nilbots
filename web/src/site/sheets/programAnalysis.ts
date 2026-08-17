import type {
  ConditionDefinition,
  ConditionExpression,
  OrderNode,
  SheetDraft,
} from './sheetAuthoring.ts';
import type { LinkedPlay, PlayProjection } from './workshopTypes.ts';

export interface ProgramRoleState {
  carryingCoreValue?: 1 | 2 | 4;
  healthPercent?: number;
  /**
   * Optional authoring lens only: the named Program parent is already latched.
   * Its entry gate is therefore not re-evaluated, matching OrderTreeRuntime.
   */
  activeProgramId?: string;
}

export interface ProgramPriorityRow {
  playId: string;
  playName: string;
  projectionId: string;
  status: 'eligible' | 'ineligible' | 'depends';
  reason: string;
  handlesCarriedCore: boolean;
  localCustody: boolean;
}

export interface ProgramPriorityRead {
  rows: ProgramPriorityRow[];
  controller: ProgramPriorityRow | null;
  handler: ProgramPriorityRow | null;
  warning?: string;
}

type Truth = true | false | 'depends';

/**
 * Explain the real materialization order without creating another evaluator.
 * Only facts supplied by the selected hypothetical state are resolved; every
 * live-world fact remains explicitly dependent on the match.
 */
export function analyzeProgramPriority(
  draft: SheetDraft,
  roleId: string,
  state: ProgramRoleState,
): ProgramPriorityRead {
  const rows: ProgramPriorityRow[] = [];
  for (const play of draft.workshop.plays) {
    for (const projection of play.projections.filter((entry) => entry.roleId === roleId)) {
      const roots = projection.orders;
      const activeHostIds = new Set((play.coreCustody?.bindings ?? [])
        .filter((binding) => binding.projectionId === projection.id)
        .flatMap((binding) => binding.hostOrderIds));
      const rootReads = roots.map((order) => evaluateOrder(
        order,
        draft,
        state,
        state.activeProgramId === play.id
          && [...activeHostIds].some((hostId) => containsOrderId(order, hostId)),
      ));
      const eligible = rootReads.find((read) => read.truth === true);
      const possible = eligible ?? rootReads.find((read) => read.truth === 'depends');
      const localCustody = (play.coreCustody?.bindings ?? []).some((binding) =>
        binding.projectionId === projection.id
        && binding.kind === 'carrier-return'
        && (!state.carryingCoreValue
          || possibleCoreValues(binding.coreBankValue).includes(state.carryingCoreValue)));
      const handlesCarriedCore = localCustody || roots.some((order) => containsBank(order));
      rows.push({
        playId: play.id,
        playName: play.name,
        projectionId: projection.id,
        status: eligible ? 'eligible' : possible ? 'depends' : 'ineligible',
        reason: eligible?.reason ?? possible?.reason
          ?? (rootReads.map((read) => read.reason).filter(Boolean).join('; ')
            || 'No executable Instruction for this state.'),
        handlesCarriedCore,
        localCustody,
      });
    }
  }
  const controller = rows.find((row) => row.status === 'eligible')
    ?? rows.find((row) => row.status === 'depends') ?? null;
  const handler = rows.find((row) => row.handlesCarriedCore
    && row.status !== 'ineligible') ?? null;
  let warning: string | undefined;
  if (state.carryingCoreValue && !handler) {
    warning = `No compatible Core handling covers a ${state.carryingCoreValue}-point carried Core.`;
  } else if (state.carryingCoreValue && controller && handler
      && controller.playId !== handler.playId) {
    warning = `${controller.playName} is evaluated before ${handler.playName} and may pre-empt its Core handling.`;
  }
  return { rows, controller, handler, warning };
}

export function analyzeCoreContinuation(
  draft: SheetDraft,
  roleId: string,
  value: 1 | 2 | 4,
  activeProgramId?: string,
): ProgramPriorityRead {
  return analyzeProgramPriority(draft, roleId, {
    carryingCoreValue: value,
    ...(activeProgramId ? { activeProgramId } : {}),
  });
}

export function possibleCoreValues(filter?: {
  operator: 'equals' | 'at-least' | 'at-most'; value: number;
}): Array<1 | 2 | 4> {
  const values: Array<1 | 2 | 4> = [1, 2, 4];
  if (!filter) return values;
  return values.filter((value) => filter.operator === 'equals'
    ? value === filter.value
    : filter.operator === 'at-least'
      ? value >= filter.value : value <= filter.value);
}

export function projectionForRole(play: LinkedPlay, roleId: string): PlayProjection | undefined {
  return play.projections.find((entry) => entry.roleId === roleId);
}

function evaluateOrder(
  order: OrderNode,
  draft: SheetDraft,
  state: ProgramRoleState,
  assumeActive = false,
): { truth: Truth; reason: string } {
  const gate = assumeActive
    ? { truth: true as const, reason: 'The Program parent is already active; its entry trigger is not re-evaluated.' }
    : evaluateExpression(order.when, draft.orders.conditions, state);
  if (gate.truth === false) return gate;
  if (order.orders?.length) {
    const children = order.orders.map((child) => evaluateOrder(child, draft, state));
    // Source order is priority. An unresolved earlier child may win at runtime,
    // so a later unconditional continuation cannot be reported as controller.
    const possible = children.find((child) => child.truth !== false);
    if (!possible) return {
      truth: false,
      reason: `Program gate passes, but every nested Instruction is ineligible (${children.map((child) => child.reason).join('; ')}).`,
    };
    return gate.truth === 'depends' || possible.truth === 'depends'
      ? { truth: 'depends', reason: `${gate.reason}; then ${possible.reason}` }
      : { truth: true, reason: possible.reason };
  }
  return gate.truth === true
    ? { truth: true, reason: `Instruction ${order.id ?? 'action'} is eligible.` }
    : gate;
}

function evaluateExpression(
  expression: ConditionExpression | undefined,
  conditions: Record<string, ConditionDefinition>,
  state: ProgramRoleState,
): { truth: Truth; reason: string } {
  if (!expression || expression === 'always')
    return { truth: true, reason: 'No Condition blocks it.' };
  const kind = typeof expression === 'string' ? 'one'
    : 'all' in expression ? 'all' : 'any';
  const names = typeof expression === 'string' ? [expression]
    : 'all' in expression ? expression.all : expression.any;
  const reads = names.map((name) => evaluateCondition(name, conditions[name], state));
  if (kind === 'all') {
    const failed = reads.find((read) => read.truth === false);
    if (failed) return failed;
    const dependent = reads.find((read) => read.truth === 'depends');
    return dependent ?? { truth: true, reason: 'All Conditions pass.' };
  }
  if (kind === 'any') {
    const passed = reads.find((read) => read.truth === true);
    if (passed) return passed;
    const dependent = reads.find((read) => read.truth === 'depends');
    return dependent ?? { truth: false, reason: 'No alternative Condition passes.' };
  }
  return reads[0];
}

function evaluateCondition(
  name: string,
  condition: ConditionDefinition | undefined,
  state: ProgramRoleState,
): { truth: Truth; reason: string } {
  if (!condition)
    return { truth: false, reason: `Condition ${name} is missing.` };
  let actual: number | undefined;
  if (condition.fact === 'self-carrying')
    actual = state.carryingCoreValue ? 1 : 0;
  else if (condition.fact === 'self-carried-core-bank-value')
    actual = state.carryingCoreValue;
  else if (condition.fact === 'self-health-percent')
    actual = state.healthPercent;
  if (actual === undefined) {
    return {
      truth: 'depends',
      reason: `${humanize(name)} depends on live ${humanize(condition.fact)}.`,
    };
  }
  const passes = compare(actual, condition.operator, condition.value);
  return {
    truth: passes,
    reason: passes
      ? `${humanize(name)} passes (${actual} ${operator(condition.operator)} ${condition.value}).`
      : `${humanize(name)} fails (${actual} is not ${operator(condition.operator)} ${condition.value}).`,
  };
}

function containsBank(order: OrderNode): boolean {
  if (order.move?.to === 'own-reactor') return true;
  return order.orders?.some(containsBank) ?? false;
}

function containsOrderId(order: OrderNode, id: string): boolean {
  return order.id === id || (order.orders?.some((child) => containsOrderId(child, id)) ?? false);
}

function compare(actual: number, op: ConditionDefinition['operator'], expected: number): boolean {
  if (op === 'at-least') return actual >= expected;
  if (op === 'at-most') return actual <= expected;
  if (op === 'less-than') return actual < expected;
  if (op === 'greater-than') return actual > expected;
  return actual === expected;
}

function operator(value: ConditionDefinition['operator']): string {
  return ({
    'at-least': 'at least', 'at-most': 'at most', equals: 'equal to',
    'less-than': 'less than', 'greater-than': 'greater than',
  } as const)[value];
}

function humanize(value: string): string {
  return value.replaceAll('-', ' ');
}
