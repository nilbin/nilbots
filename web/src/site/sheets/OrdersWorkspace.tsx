import { useState, type ReactNode } from 'react';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { TacticalSheetCatalog } from '../api';
import {
  ACTIONS,
  CONDITION_FACTS,
  CONDITION_OPERATORS,
  actionOf,
  conditionExpressionNames,
  conditionSubjectKind,
  freshOrder,
  identifierFromName,
  moveIntentKind,
  moveRole,
  renameCondition,
  renameRole,
  signatureTargets,
  skillForClass,
  uniqueId,
  type ConditionDefinition,
  type ConditionExpression,
  type MapPlanDocument,
  type MoveOrder,
  type MoveTarget,
  type OrderAction,
  type OrderNode,
  type OrderUrgency,
  type OrdersDocument,
  type RoleDefinition,
  type SheetIssue,
  type UseTarget,
} from './sheetAuthoring';
import { MapTargetPreview } from './MapPlanEditor';

type LibraryTool = 'conditions' | 'capacities' | 'shared' | null;
type PlotKind = 'route' | 'zone' | 'anchor';

const UPGRADE_COPY: Record<string, string> = {
  damage: 'Harder hits', vision: 'See threats sooner', reach: 'Longer action reach', vitality: 'Stay online longer',
};
const ACTION_COPY: Record<Exclude<OrderAction, 'branch' | 'shared'>, { glyph: string; label: string; description: string }> = {
  move: { glyph: '➜', label: 'Move', description: 'Go somewhere, patrol a route, or follow a role.' },
  fight: { glyph: '⚔', label: 'Fight', description: 'Choose targets and combat movement.' },
  use: { glyph: '✦', label: 'Use skill', description: 'Fire this role’s signature skill.' },
  handoff: { glyph: '⇢', label: 'Handoff', description: 'Pass a carried Core to an adjacent ally.' },
  drop: { glyph: '⬡', label: 'Drop Core', description: 'Release a Core toward your reactor.' },
};

export function SquadEditor({ catalog, orders, issues, onChange }: {
  catalog: TacticalSheetCatalog;
  orders: OrdersDocument;
  issues: SheetIssue[];
  onChange: (next: OrdersDocument) => void;
}) {
  const [openRole, setOpenRole] = useState(orders.roles[0]?.id ?? '');
  const countForClass = (classId: string) => orders.roles.reduce(
    (count, role) => count + role.classes.filter((id) => id === classId).length, 0,
  );
  const bodyCount = orders.roles.reduce((count, role) => count + role.classes.length, 0);
  const commit = (change: (next: OrdersDocument) => void) => {
    const next = structuredClone(orders); change(next); onChange(next);
  };

  return <section className="grid gap-3 xl:grid-cols-[minmax(0,1fr)_310px]" data-sheet-path="orders.roles">
    <div className="panel pad min-w-0">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto max-w-2xl">
          <p className="lab text-arena-material">The eight bodies</p>
          <h2 className="type-display mt-1 text-[22px]">Cast the squad by intent</h2>
          <p className="t-meta mt-1">Bodies sharing a role run the exact same ordered plan. Split them when their behavior should differ.</p>
        </div>
        <span className={clsx('pill', bodyCount === catalog.slotCount ? 'text-arena-ok' : 'text-arena-hot')}>
          {bodyCount}/{catalog.slotCount} assigned
        </span>
      </div>
      <div className="mt-4 grid gap-2 md:grid-cols-2">
        {orders.roles.map((role, roleIndex) => {
          const open = openRole === role.id;
          return <article key={role.id} className={clsx(
            'overflow-hidden rounded-sm border bg-arena-deep/25',
            open ? 'border-arena-material/65' : 'border-arena-edge',
          )} data-sheet-path={`orders.roles.${roleIndex}`}>
            <button type="button" className="flex min-h-16 w-full items-center gap-3 p-3 text-left"
              aria-expanded={open} onClick={() => setOpenRole(open ? '' : role.id)}>
              <span className="flex -space-x-1">{role.classes.map((classId, index) => <ClassIcon
                key={`${classId}:${index}`} classId={classId} size={36} label={`${humanize(role.id)} body ${index + 1}`} />)}</span>
              <span className="min-w-0 flex-1">
                <span className="type-display block truncate text-[17px]">{humanize(role.id)}</span>
                <span className="t-micro mt-0.5 block">{role.classes.length} {role.classes.length === 1 ? 'body' : 'bodies'} · {role.orders.length} top-level orders</span>
              </span>
              <span className="disclosure-chevron" aria-hidden="true">›</span>
            </button>
            {open && <div className="border-t border-arena-edge p-3">
              <label className="block">
                <span className="lab mb-1 block">Role call sign</span>
                <input className="field min-h-11" value={role.id} maxLength={24}
                  onChange={(event) => {
                    const nextId = identifierFromName(event.target.value, role.id);
                    if (nextId === role.id || orders.roles.some((entry) => entry.id === nextId)) return;
                    commit((next) => renameRole(next, role.id, nextId));
                    setOpenRole(nextId);
                  }} />
              </label>
              <p className="lab mb-2 mt-4">Class bodies</p>
              <div className="space-y-2">{role.classes.map((classId, slot) => {
                const current = catalog.classes.find((entry) => entry.id === classId);
                return <details key={`${classId}:${slot}`} className="rounded-sm border border-arena-edge bg-arena-panel/70">
                  <summary className="flex min-h-12 cursor-pointer items-center gap-2 px-2.5">
                    <ClassIcon classId={classId} size={34} />
                    <span className="min-w-0 flex-1"><b className="t-body block">{current?.name ?? classId}</b><span className="t-micro block truncate">{current?.signatureName}</span></span>
                    <span className="disclosure-chevron">›</span>
                  </summary>
                  <div className="grid grid-cols-2 gap-1.5 border-t border-arena-edge p-2 sm:grid-cols-3">
                    {catalog.classes.map((entry) => {
                      const atCap = countForClass(entry.id) >= catalog.maximumCopiesPerClass && entry.id !== classId;
                      return <button key={entry.id} type="button" className="choice-card min-h-16"
                        data-selected={entry.id === classId} disabled={!entry.unlocked || atCap}
                        onClick={() => commit((next) => { next.roles[roleIndex].classes[slot] = entry.id; })}>
                        <span className="flex items-center gap-2"><ClassIcon classId={entry.id} size={30} decorative /><span className="t-body truncate">{entry.name}</span></span>
                        <span className="t-micro mt-1 block truncate">{atCap ? 'Copy cap reached' : entry.signatureName}</span>
                      </button>;
                    })}
                  </div>
                </details>;
              })}</div>
              <div className="mt-2 flex gap-2">
                <button type="button" className="btn min-h-11 flex-1" disabled={bodyCount >= catalog.slotCount}
                  onClick={() => commit((next) => {
                    const replacement = catalog.classes.find((entry) => entry.unlocked
                      && countForClass(entry.id) < catalog.maximumCopiesPerClass)?.id ?? 'kestrel';
                    next.roles[roleIndex].classes.push(replacement);
                  })}>＋ Add body</button>
                <button type="button" className="btn min-h-11 flex-1" disabled={role.classes.length <= 1}
                  onClick={() => commit((next) => { next.roles[roleIndex].classes.pop(); })}>Remove last</button>
              </div>
              <p className="lab mb-2 mt-4">Upgrade rhythm</p>
              <div className="space-y-1.5">{role.upgradePath.map((upgrade, index) => <div key={`${upgrade}:${index}`}
                className="flex min-h-11 items-center gap-2 rounded-sm border border-arena-edge px-2">
                <span className="pill">{index + 1}</span><span className="t-body flex-1">{humanize(upgrade)}</span>
                <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={index === 0}
                  onClick={() => commit((next) => swap(next.roles[roleIndex].upgradePath, index, index - 1))}>↑</button>
                <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={index === role.upgradePath.length - 1}
                  onClick={() => commit((next) => swap(next.roles[roleIndex].upgradePath, index, index + 1))}>↓</button>
                <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={role.upgradePath.length <= 1}
                  onClick={() => commit((next) => next.roles[roleIndex].upgradePath.splice(index, 1))}>×</button>
              </div>)}</div>
              {role.upgradePath.length < 4 && <div className="mt-2 grid grid-cols-2 gap-1.5">
                {Object.keys(UPGRADE_COPY).map((upgrade) => <button key={upgrade} type="button" className="choice-card min-h-14"
                  onClick={() => commit((next) => next.roles[roleIndex].upgradePath.push(upgrade as RoleDefinition['upgradePath'][number]))}>
                  <span className="t-body block">＋ {humanize(upgrade)}</span><span className="t-micro block">{UPGRADE_COPY[upgrade]}</span>
                </button>)}
              </div>}
              <div className="mt-4 flex flex-wrap gap-2 border-t border-arena-edge pt-3">
                <button type="button" className="btn min-h-11" disabled={roleIndex === 0}
                  onClick={() => commit((next) => moveRole(next, roleIndex, -1))}>Move earlier</button>
                <button type="button" className="btn min-h-11" disabled={roleIndex === orders.roles.length - 1}
                  onClick={() => commit((next) => moveRole(next, roleIndex, 1))}>Move later</button>
                <button type="button" className="btn min-h-11 text-arena-hot" disabled={orders.roles.length <= 1}
                  onClick={() => commit((next) => { next.roles.splice(roleIndex, 1); })}>Delete role</button>
              </div>
            </div>}
          </article>;
        })}
      </div>
      <button type="button" className="btn btn-secondary mt-3 min-h-11" disabled={orders.roles.length >= 8 || bodyCount >= catalog.slotCount}
        onClick={() => {
          const id = uniqueId('new-role', orders.roles.map((role) => role.id));
          commit((next) => next.roles.push({ id, classes: ['kestrel'], upgradePath: ['reach', 'vitality'], orders: [freshOrder('move', 'hold-centre')] }));
          setOpenRole(id);
        }}>＋ Add tactical role</button>
    </div>
    <aside className="panel pad h-fit xl:sticky xl:top-20">
      <p className="lab">Roster read</p>
      <div className="mt-3 grid grid-cols-4 gap-2">{orders.roles.flatMap((role) => role.classes.map((classId, index) => <div key={`${role.id}:${index}`} className="text-center">
        <ClassIcon classId={classId} size={46} label={`${humanize(role.id)} · ${humanize(classId)}`} />
        <span className="t-micro mt-1 block truncate">{humanize(role.id)}</span>
      </div>))}</div>
      <p className="t-micro mt-3 border-t border-arena-edge pt-3">Unit ids follow this source order. A body keeps its role through death and respawn.</p>
      <IssueList issues={issues.filter((issue) => issue.path.startsWith('orders.roles'))} />
    </aside>
  </section>;
}

export function OrdersWorkspace({ catalog, orders, mapPlan, issues, onChange, onOpenMap }: {
  catalog: TacticalSheetCatalog;
  orders: OrdersDocument;
  mapPlan: MapPlanDocument;
  issues: SheetIssue[];
  onChange: (next: OrdersDocument) => void;
  onOpenMap: (focus?: { kind: PlotKind; id: string }) => void;
}) {
  const [roleId, setRoleId] = useState(orders.roles[0]?.id ?? '');
  const [openPath, setOpenPath] = useState<string | null>(null);
  const [library, setLibrary] = useState<LibraryTool>(null);
  const roleIndex = Math.max(0, orders.roles.findIndex((role) => role.id === roleId));
  const role = orders.roles[roleIndex];
  const commit = (change: (next: OrdersDocument) => void) => {
    const next = structuredClone(orders); change(next); onChange(next);
  };

  if (!role) return null;
  return <section className="grid min-w-0 gap-3 xl:grid-cols-[250px_minmax(0,1fr)]">
    <aside className="panel h-fit overflow-hidden xl:sticky xl:top-20">
      <div className="border-b border-arena-edge p-3">
        <p className="lab text-arena-material">Role plans</p>
        <p className="t-micro mt-1">Read top to bottom. The first executable leaf wins.</p>
      </div>
      <div className="flex gap-1 overflow-x-auto p-2 xl:flex-col">{orders.roles.map((entry) => <button key={entry.id}
        type="button" className={clsx(
          'min-h-14 min-w-[180px] rounded-sm border p-2 text-left xl:min-w-0',
          entry.id === role.id ? 'border-arena-material/65 bg-arena-material/10' : 'border-transparent bg-arena-deep/25',
        )} onClick={() => { setRoleId(entry.id); setOpenPath(null); }}>
        <span className="flex items-center gap-2"><span className="flex -space-x-1">{entry.classes.map((classId, index) => <ClassIcon key={`${classId}:${index}`} classId={classId} size={28} decorative />)}</span><b className="t-body truncate">{humanize(entry.id)}</b></span>
        <span className="t-micro mt-1 block">{entry.orders.length} top-level orders</span>
      </button>)}</div>
      <div className="grid gap-1 border-t border-arena-edge p-2">
        <button type="button" className="btn min-h-11 justify-start" onClick={() => setLibrary('conditions')}>◇ Conditions <span className="pill ml-auto">{Object.keys(orders.conditions).length}</span></button>
        <button type="button" className="btn min-h-11 justify-start" onClick={() => setLibrary('capacities')}>⫶ Capacities <span className="pill ml-auto">{Object.keys(orders.capacities).length}</span></button>
        <button type="button" className="btn min-h-11 justify-start" onClick={() => setLibrary('shared')}>⧉ Shared orders <span className="pill ml-auto">{Object.keys(orders.sharedOrders).length}</span></button>
      </div>
    </aside>

    <div className="panel pad min-w-0" data-sheet-path={`orders.roles.${roleIndex}.orders`}>
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto">
          <p className="lab text-arena-material">{humanize(role.id)}</p>
          <h2 className="type-display mt-1 text-[22px]">What this role tries, in order</h2>
          <p className="t-meta mt-1">Conditions choose the moment. Nesting groups a short local plan. Combat and Core handling are ordinary orders.</p>
        </div>
        <span className="flex -space-x-1">{role.classes.map((classId, index) => <ClassIcon key={`${classId}:${index}`} classId={classId} size={42} />)}</span>
      </div>
      <OrderList
        orders={role.orders}
        path={`orders.roles.${roleIndex}.orders`}
        role={role}
        allOrders={orders}
        mapPlan={mapPlan}
        catalog={catalog}
        issues={issues}
        depth={0}
        openPath={openPath}
        setOpenPath={setOpenPath}
        onOpenMap={onOpenMap}
        onChange={(nextOrders) => commit((next) => { next.roles[roleIndex].orders = nextOrders; })}
      />
      <IssueList issues={issues.filter((issue) => issue.path.startsWith(`orders.roles.${roleIndex}`))} />
    </div>

    {library && <Modal title={library === 'conditions' ? 'Named conditions' : library === 'capacities' ? 'Shared capacity rules' : 'Shared exact orders'}
      description={library === 'conditions'
        ? 'Name a fact once, then combine those names explicitly with AND or OR on any order.'
        : library === 'capacities'
          ? 'One number limits bodies claiming the same resolved target. A shared name needs at least two consumers.'
          : 'Reuse an exact order tree. Shared orders have no parameters or per-role overrides.'}
      onClose={() => setLibrary(null)}>
      {library === 'conditions' && <ConditionsEditor orders={orders} mapPlan={mapPlan} issues={issues} onChange={onChange} />}
      {library === 'capacities' && <CapacitiesEditor orders={orders} issues={issues} onChange={onChange} />}
      {library === 'shared' && <SharedOrdersEditor catalog={catalog} orders={orders} mapPlan={mapPlan} issues={issues}
        onChange={onChange} onOpenMap={(focus) => { setLibrary(null); onOpenMap(focus); }} />}
    </Modal>}
  </section>;
}

function OrderList({ orders: list, path, role, allOrders, mapPlan, catalog, issues, depth, openPath, setOpenPath, onOpenMap, onChange }: {
  orders: OrderNode[];
  path: string;
  role: RoleDefinition;
  allOrders: OrdersDocument;
  mapPlan: MapPlanDocument;
  catalog: TacticalSheetCatalog;
  issues: SheetIssue[];
  depth: number;
  openPath: string | null;
  setOpenPath: (path: string | null) => void;
  onOpenMap: (focus?: { kind: PlotKind; id: string }) => void;
  onChange: (next: OrderNode[]) => void;
}) {
  const mutate = (change: (next: OrderNode[]) => void) => { const next = structuredClone(list); change(next); onChange(next); };
  const ids = allOrderIds(allOrders);
  return <div className={clsx('mt-4 space-y-2', depth > 0 && 'ml-2 border-l border-arena-material/30 pl-3')}>
    {list.map((node, index) => {
      const nodePath = `${path}.${index}`;
      const open = openPath === nodePath || Boolean(openPath?.startsWith(`${nodePath}.orders.`));
      return <OrderCard key={`${node.id ?? node.shared ?? 'branch'}:${index}`}
        node={node} path={nodePath} role={role} allOrders={allOrders} mapPlan={mapPlan}
        catalog={catalog} issues={issues} depth={depth} open={open}
        openPath={openPath}
        onToggle={() => setOpenPath(open ? null : nodePath)}
        onOpenPath={setOpenPath} onOpenMap={onOpenMap}
        onChange={(nextNode) => mutate((next) => { next[index] = nextNode; })}
        onMove={(delta) => mutate((next) => swap(next, index, index + delta))}
        onDuplicate={() => mutate((next) => {
          const copy = structuredClone(node);
          if (copy.id) copy.id = uniqueId(`${copy.id}-copy`, ids);
          next.splice(index + 1, 0, copy);
        })}
        onDelete={() => mutate((next) => next.splice(index, 1))}
        first={index === 0} last={index === list.length - 1} />;
    })}
    <AddOrder depth={depth} allOrders={allOrders} role={role} catalog={catalog} onAdd={(node) => {
      mutate((next) => next.push(node));
      window.setTimeout(() => setOpenPath(`${path}.${list.length}`), 0);
    }} />
  </div>;
}

function OrderCard({ node, path, role, allOrders, mapPlan, catalog, issues, depth, open, openPath, onToggle, onOpenPath, onOpenMap, onChange, onMove, onDuplicate, onDelete, first, last, externalId }: {
  node: OrderNode;
  path: string;
  role: RoleDefinition;
  allOrders: OrdersDocument;
  mapPlan: MapPlanDocument;
  catalog: TacticalSheetCatalog;
  issues: SheetIssue[];
  depth: number;
  open: boolean;
  openPath: string | null;
  onToggle: () => void;
  onOpenPath: (path: string | null) => void;
  onOpenMap: (focus?: { kind: PlotKind; id: string }) => void;
  onChange: (next: OrderNode) => void;
  onMove: (delta: number) => void;
  onDuplicate: () => void;
  onDelete: () => void;
  first: boolean;
  last: boolean;
  externalId?: string;
}) {
  const action = actionOf(node);
  const meta = action && action !== 'branch' && action !== 'shared' ? ACTION_COPY[action] : null;
  const label = externalId ?? node.id ?? node.shared ?? 'Unnamed order';
  const localIssues = issues.filter((issue) => issue.path.startsWith(path));
  const change = (mutate: (next: OrderNode) => void) => { const next = structuredClone(node); mutate(next); onChange(next); };
  return <article className={clsx(
    'overflow-hidden rounded-sm border bg-arena-deep/25',
    open ? 'border-arena-material/70 shadow-[inset_3px_0_0_var(--color-arena-material)]' : 'border-arena-edge',
    localIssues.some((issue) => issue.severity === 'error') && 'border-arena-hot/60',
  )} data-sheet-path={path}>
    <div className="flex min-h-16 items-center gap-2 p-2">
      <span className="pill shrink-0">{externalId ? 'shared' : depth === 0 ? `${priorityFromPath(path)}` : '↳'}</span>
      <button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left" onClick={onToggle} aria-expanded={open}>
        <span className="flex size-9 shrink-0 items-center justify-center rounded-sm border border-arena-edge bg-arena-panel font-mono text-[18px] text-arena-material">
          {action === 'branch' ? '⌘' : action === 'shared' ? '⧉' : meta?.glyph ?? '?'}
        </span>
        <span className="min-w-0 flex-1"><b className="type-display block truncate text-[16px]">{humanize(label)}</b><span className="t-micro mt-0.5 block truncate">{orderSummary(node, allOrders)}</span></span>
        {node.urgency && <span className={clsx('pill shrink-0', node.urgency === 'urgent' && 'text-arena-hot', node.urgency === 'routine' && 'opacity-70')}>{node.urgency}</span>}
        {localIssues.length > 0 && <span className="pill shrink-0 text-arena-hot">{localIssues.length}</span>}
        <span className="disclosure-chevron shrink-0">›</span>
      </button>
      {!externalId && <div className="hidden gap-1 sm:flex">
        <button type="button" className="btn min-h-10 min-w-10 px-2" aria-label="Move order earlier" disabled={first} onClick={() => onMove(-1)}>↑</button>
        <button type="button" className="btn min-h-10 min-w-10 px-2" aria-label="Move order later" disabled={last} onClick={() => onMove(1)}>↓</button>
      </div>}
    </div>
    {open && <div className="border-t border-arena-edge p-3">
      {action === 'shared' ? <SharedReference node={node} allOrders={allOrders} onChange={onChange} /> : <>
        {!externalId && <label className="block"><span className="lab mb-1 block">Authored order name</span>
          <input className="field min-h-11" value={node.id ?? ''} maxLength={48}
            onChange={(event) => change((next) => { next.id = identifierFromName(event.target.value, next.id ?? 'order'); })} /></label>}
        <LifecycleEditor node={node} allOrders={allOrders} onChange={onChange} />
        {action === 'branch' && node.orders && <>
          <div className="mt-4 rounded-sm border border-arena-material/30 bg-arena-material/5 p-2">
            <p className="lab text-arena-material">Nested priority · first executable child wins</p>
            <OrderList orders={node.orders} path={`${path}.orders`} role={role} allOrders={allOrders}
              mapPlan={mapPlan} catalog={catalog} issues={issues} depth={depth + 1}
              openPath={openPath} setOpenPath={onOpenPath} onOpenMap={onOpenMap}
              onChange={(children) => change((next) => { next.orders = children; })} />
          </div>
        </>}
        {action === 'move' && <MoveEditor value={node.move!} role={role} allOrders={allOrders}
          mapPlan={mapPlan} catalog={catalog} onOpenMap={onOpenMap}
          onChange={(move) => change((next) => { next.move = move; })} />}
        {action === 'fight' && <FightEditor value={node.fight!}
          onChange={(fight) => change((next) => { next.fight = fight; })} />}
        {action === 'use' && <UseEditor node={node} role={role} allOrders={allOrders} catalog={catalog}
          mapPlan={mapPlan} onOpenMap={onOpenMap}
          onChange={(use) => change((next) => { next.use = use; })} />}
        {action === 'handoff' && <HandoffEditor value={node.handoff!} roles={allOrders.roles}
          onChange={(handoff) => change((next) => { next.handoff = handoff; })} />}
        {action === 'drop' && <div className="mt-4 rounded-sm border border-arena-material/35 bg-arena-material/8 p-3">
          <p className="type-display text-[16px]">Release the carried Core toward home</p>
          <p className="t-meta mt-1">The engine chooses the legal adjacent tile nearest your reactor. This body cannot reclaim that same Core until the relocation interval expires.</p>
        </div>}
        {action && action !== 'branch' && <CapacityField value={node.capacity} allOrders={allOrders}
          onChange={(capacity) => change((next) => {
            if (capacity === undefined) delete next.capacity; else next.capacity = capacity;
          })} />}
      </>}
      <IssueList issues={localIssues} />
      {!externalId && <div className="mt-4 flex flex-wrap gap-2 border-t border-arena-edge pt-3 sm:hidden">
        <button type="button" className="btn min-h-11" disabled={first} onClick={() => onMove(-1)}>Move earlier</button>
        <button type="button" className="btn min-h-11" disabled={last} onClick={() => onMove(1)}>Move later</button>
      </div>}
      {!externalId && <div className="mt-2 flex flex-wrap gap-2">
        <button type="button" className="btn min-h-11" onClick={onDuplicate}>Duplicate</button>
        <button type="button" className="btn min-h-11 text-arena-hot" onClick={onDelete}>Delete</button>
      </div>}
    </div>}
  </article>;
}

function AddOrder({ depth, allOrders, role, catalog, onAdd }: {
  depth: number;
  allOrders: OrdersDocument;
  role: RoleDefinition;
  catalog: TacticalSheetCatalog;
  onAdd: (node: OrderNode) => void;
}) {
  const [open, setOpen] = useState(false);
  const ids = allOrderIds(allOrders);
  const signatureSkill = skillForClass(catalog, role.classes[0] ?? '') ?? 'vector-dash';
  return <div>
    <button type="button" className="btn btn-secondary min-h-11 w-full" onClick={() => setOpen((value) => !value)}>
      ＋ Add {depth === 0 ? 'priority order' : 'nested order'}
    </button>
    {open && <div className="mt-2 grid gap-1.5 sm:grid-cols-2 lg:grid-cols-3">
      {ACTIONS.map((action) => <button key={action} type="button" className="choice-card min-h-[76px]"
        onClick={() => { onAdd(freshOrder(action, uniqueId(action, ids), signatureSkill)); setOpen(false); }}>
        <span className="flex items-center gap-2"><span className="font-mono text-[19px] text-arena-material">{ACTION_COPY[action].glyph}</span><b className="type-display text-[15px]">{ACTION_COPY[action].label}</b></span>
        <span className="t-micro mt-1 block">{ACTION_COPY[action].description}</span>
      </button>)}
      {depth < 2 && <button type="button" className="choice-card min-h-[76px]" onClick={() => {
        onAdd({ id: uniqueId('new-play', ids), orders: [freshOrder('move', uniqueId('opening-move', ids))] }); setOpen(false);
      }}><b className="type-display block text-[15px]">⌘ Nested play</b><span className="t-micro mt-1 block">One lifecycle around a short local priority list.</span></button>}
      {Object.keys(allOrders.sharedOrders).map((id) => <button key={id} type="button" className="choice-card min-h-[76px]"
        onClick={() => { onAdd({ shared: id }); setOpen(false); }}><b className="type-display block text-[15px]">⧉ {humanize(id)}</b><span className="t-micro mt-1 block">Exact shared order</span></button>)}
    </div>}
  </div>;
}

function LifecycleEditor({ node, allOrders, onChange }: { node: OrderNode; allOrders: OrdersDocument; onChange: (next: OrderNode) => void }) {
  const change = (mutate: (next: OrderNode) => void) => { const next = structuredClone(node); mutate(next); onChange(next); };
  return <div className="mt-4 rounded-sm border border-arena-edge bg-arena-panel/45 p-3">
    <p className="lab">When this order owns the body</p>
    <UrgencyEditor value={node.urgency} onChange={(value) => change((next) => {
      if (value === undefined) delete next.urgency; else next.urgency = value;
    })} />
    <div className="mt-2 grid gap-2 lg:grid-cols-2">
      <ConditionExpressionEditor label="Enter / remain while" value={node.when} conditions={allOrders.conditions}
        onChange={(value) => change((next) => { if (value === undefined) delete next.when; else next.when = value; })} />
      <ConditionExpressionEditor label="Latch until" value={node.until} conditions={allOrders.conditions}
        onChange={(value) => change((next) => { if (value === undefined) delete next.until; else next.until = value; })} />
    </div>
    <div className="mt-2 grid gap-2 sm:grid-cols-2">
      <OptionalTicks label="Timeout" value={node.timeout} placeholder="No limit" min={1} max={1200}
        onChange={(value) => change((next) => { if (value === undefined) delete next.timeout; else next.timeout = value; })} />
      <OptionalTicks label="Cooldown after exit" value={node.cooldown} placeholder="No cooldown" min={0} max={1200}
        onChange={(value) => change((next) => { if (value === undefined) delete next.cooldown; else next.cooldown = value; })} />
    </div>
    <p className="t-micro mt-2">Without “until”, the entry condition stays live. With it, the order latches until that exit condition, timeout, success or death.</p>
  </div>;
}

const URGENCY_COPY: Array<{ value: OrderUrgency | undefined; label: string; description: string }> = [
  { value: undefined, label: 'Inherit', description: 'Use the parent play; Normal at the top level.' },
  { value: 'routine', label: 'Routine', description: 'Yields readily during ordinary positioning.' },
  { value: 'normal', label: 'Normal', description: 'The default weight for squad traffic.' },
  { value: 'urgent', label: 'Urgent', description: 'Gets scarce allied movement space first.' },
];

function UrgencyEditor({ value, onChange }: { value: OrderUrgency | undefined; onChange: (value: OrderUrgency | undefined) => void }) {
  return <fieldset className="mt-3">
    <legend className="t-micro">Movement urgency</legend>
    <div className="mt-1.5 grid grid-cols-2 gap-1.5 lg:grid-cols-4">
      {URGENCY_COPY.map((entry) => <button key={entry.value ?? 'inherit'} type="button"
        className="choice-card min-h-[72px] text-left" data-selected={value === entry.value}
        onClick={() => onChange(entry.value)}>
        <b className="type-display block text-[14px]">{entry.label}</b>
        <span className="t-micro mt-1 block">{entry.description}</span>
      </button>)}
    </div>
    <p className="t-micro mt-2">Urgency never changes which order wins. It only decides allied right-of-way after this order is already active; sustained blocking still earns a turn.</p>
  </fieldset>;
}

function ConditionExpressionEditor({ label, value, conditions, onChange }: {
  label: string; value: ConditionExpression | undefined; conditions: Record<string, ConditionDefinition>; onChange: (value: ConditionExpression | undefined) => void;
}) {
  const names = conditionExpressionNames(value);
  const connector = value && typeof value === 'object' && 'any' in value ? 'any' : 'all';
  const available = Object.keys(conditions).filter((id) => !names.includes(id));
  const setNames = (nextNames: string[], nextConnector = connector) => {
    if (nextNames.length === 0) onChange(undefined);
    else if (nextNames.length === 1) onChange(nextNames[0]);
    else onChange(nextConnector === 'any' ? { any: nextNames } : { all: nextNames });
  };
  return <div className="rounded-sm border border-arena-edge bg-arena-deep/25 p-2">
    <span className="t-micro block">{label}</span>
    <div className="mt-2 flex flex-wrap gap-1">{names.length === 0 && <span className="pill">always</span>}{names.map((name, index) => <span key={name} className="flex items-center gap-1 rounded-full border border-arena-edge bg-arena-panel px-2 py-1 t-micro">
      {index > 0 && <b className="text-arena-material">{connector.toUpperCase()}</b>} {humanize(name)}
      <button type="button" className="min-h-7 min-w-7" aria-label={`Remove ${name}`} onClick={() => setNames(names.filter((entry) => entry !== name))}>×</button>
    </span>)}</div>
    {names.length >= 2 && <div className="mt-2 flex rounded-sm border border-arena-edge p-1">
      {(['all', 'any'] as const).map((value) => <button key={value} type="button" className={clsx('btn min-h-10 flex-1', connector === value && 'btn-on')}
        onClick={() => setNames(names, value)}>{value === 'all' ? 'AND · all true' : 'OR · any true'}</button>)}
    </div>}
    {available.length > 0 && <details className="mt-2"><summary className="btn min-h-11 w-full cursor-pointer">＋ Add condition</summary>
      <div className="mt-1 grid max-h-48 grid-cols-2 gap-1 overflow-auto">{available.map((id) => <button key={id} type="button" className="choice-card min-h-11"
        onClick={() => setNames([...names, id])}>{humanize(id)}</button>)}</div>
    </details>}
  </div>;
}

function MoveEditor({ value, role, allOrders, mapPlan, catalog, onOpenMap, onChange }: {
  value: MoveOrder; role: RoleDefinition; allOrders: OrdersDocument; mapPlan: MapPlanDocument; catalog: TacticalSheetCatalog;
  onOpenMap: (focus?: { kind: PlotKind; id: string }) => void; onChange: (next: MoveOrder) => void;
}) {
  const kind = moveIntentKind(value);
  const routeIds = Object.keys(mapPlan.routes);
  const followRoleIds = allOrders.roles.filter((entry) => entry.id !== role.id).map((entry) => entry.id);
  const change = (mutate: (next: MoveOrder) => void) => { const next = structuredClone(value); mutate(next); onChange(next); };
  const setKind = (nextKind: 'to' | 'patrol' | 'follow') => {
    const next: MoveOrder = { posture: value.posture ?? 'line' };
    if (nextKind === 'to') next.to = { well: 'centre' };
    if (nextKind === 'patrol') next.patrol = routeIds[0] ?? '';
    if (nextKind === 'follow') next.follow = followRoleIds[0] ?? '';
    onChange(next);
  };
  const highlight = mapHighlight(value);
  return <div className="mt-4">
    <p className="lab mb-2">Movement intent</p>
    <div className="grid grid-cols-3 gap-1.5">
      {([['to', 'Go to', 'Hold at a destination'], ['patrol', 'Patrol', 'Repeat a plotted route'], ['follow', 'Follow', 'Track the nearest body in a role']] as const).map(([id, label, copy]) => <button key={id} type="button"
        className="choice-card min-h-[74px]" data-selected={kind === id} onClick={() => setKind(id)}>
        <b className="type-display block text-[15px]">{label}</b><span className="t-micro mt-1 block">{copy}</span>
      </button>)}
    </div>
    {kind === 'to' && <MoveDestination value={value.to ?? { well: 'centre' }} mapPlan={mapPlan} onChange={(to) => change((next) => { next.to = to; })} />}
    {kind === 'patrol' && (routeIds.length > 0
      ? <CardPicker label="Patrol route" value={value.patrol ?? ''} values={routeIds}
        onChange={(patrol) => change((next) => { next.patrol = patrol; })} />
      : <div className="mt-3 rounded-sm border border-arena-material/35 bg-arena-material/8 p-3">
        <p className="type-display text-[16px]">Plot a route before this patrol can run</p>
        <p className="t-meta mt-1">The unfinished patrol stays selected while you draw it; validation will keep the sheet from saving prematurely.</p>
        <button type="button" className="btn btn-secondary mt-3 min-h-11 w-full" onClick={() => onOpenMap()}>Open map and add route</button>
      </div>)}
    {kind === 'follow' && (followRoleIds.length > 0
      ? <CardPicker label="Body to follow" value={encodeFollowTarget(value.follow)}
        values={[...followRoleIds.map((id) => `role:${id}`), 'selector:most-experienced-ally']}
        labeler={(entry) => entry === 'selector:most-experienced-ally'
          ? 'Most experienced ally'
          : `Role · ${humanize(entry.slice(5))}`}
        descriptions={{ 'selector:most-experienced-ally': 'Rally around the live veteran; deterministic unit id breaks level ties.' }}
        onChange={(follow) => change((next) => { next.follow = decodeFollowTarget(follow); })} />
      : <p className="t-meta mt-3 rounded-sm border border-arena-edge bg-arena-panel/45 p-3">Add another role in Squad before choosing Follow.</p>)}
    {kind !== 'follow' && <CardPicker label="Optional ingress route" value={value.via ?? ''}
      values={['', ...Object.keys(mapPlan.routes)]} emptyLabel="Direct · no ingress"
      onChange={(via) => change((next) => { if (!via) delete next.via; else next.via = via; })} />}
    <CardPicker label="Pre-combat posture" value={value.posture ?? ''} values={['front', 'line', 'rear']} emptyLabel="No posture"
      descriptions={{ front: 'Press the front edge', line: 'Occupy the neutral line', rear: 'Stay behind the destination' }}
      onChange={(posture) => change((next) => { if (!posture) delete next.posture; else next.posture = posture as MoveOrder['posture']; })} />
    <div className="mt-3"><MapTargetPreview catalog={catalog} mapPlan={mapPlan} highlight={highlight}
      onOpenMap={highlight ? () => onOpenMap(highlight) : () => onOpenMap()} /></div>
  </div>;
}

function MoveDestination({ value, mapPlan, onChange }: { value: MoveTarget; mapPlan: MapPlanDocument; onChange: (value: MoveTarget) => void }) {
  const encoded = encodeMoveTarget(value);
  const values = [
    'dynamic:own-reactor', 'dynamic:nearest-heal-zone', 'dynamic:nearest-known-loose-core',
    'dynamic:highest-bank-value-known-loose-core',
    'well:north', 'well:centre', 'well:south', 'well:own', 'well:enemy',
    ...Object.keys(mapPlan.zones).map((id) => `zone:${id}`),
    ...Object.keys(mapPlan.anchors).map((id) => `anchor:${id}`),
    ...Object.keys(mapPlan.anchors).map((id) => `adjacentTo:${id}`),
  ];
  return <CardPicker label="Destination" value={encoded} values={values}
    labeler={(entry) => entry.startsWith('adjacentTo:')
      ? `Beside ${humanize(entry.split(':')[1])}`
      : humanize(entry.split(':')[1])}
    onChange={(entry) => onChange(decodeMoveTarget(entry))} />;
}

function FightEditor({ value, onChange }: { value: NonNullable<OrderNode['fight']>; onChange: (next: NonNullable<OrderNode['fight']>) => void }) {
  const change = (mutate: (next: NonNullable<OrderNode['fight']>) => void) => { const next = structuredClone(value); mutate(next); onChange(next); };
  const candidates = ['carrier', 'veteran', 'deployment', 'weakest', 'nearest', 'medic', 'marked'] as const;
  return <div className="mt-4">
    <p className="lab">Combat intent</p>
    <p className="t-micro mt-1">Target priorities are read left to right. Tap to add; arrows reorder.</p>
    <div className="mt-2 space-y-1.5">{value.targets.map((target, index) => <div key={target} className="flex min-h-11 items-center gap-2 rounded-sm border border-arena-edge bg-arena-panel/50 px-2">
      <span className="pill">{index + 1}</span><b className="t-body flex-1">{humanize(target)}</b>
      <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={index === 0} onClick={() => change((next) => swap(next.targets, index, index - 1))}>↑</button>
      <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={index === value.targets.length - 1} onClick={() => change((next) => swap(next.targets, index, index + 1))}>↓</button>
      <button type="button" className="btn min-h-9 min-w-9 px-2" disabled={value.targets.length <= 1} onClick={() => change((next) => next.targets.splice(index, 1))}>×</button>
    </div>)}</div>
    <div className="mt-2 flex flex-wrap gap-1.5">{candidates.filter((target) => !value.targets.includes(target)).map((target) => <button key={target}
      type="button" className="btn min-h-11" onClick={() => change((next) => next.targets.push(target))}>＋ {humanize(target)}</button>)}</div>
    <CardPicker label="How to move in the fight" value={value.style} values={['advance', 'hold', 'kite']}
      descriptions={{ advance: 'Close into legal range', hold: 'Fight from this ground', kite: 'Keep distance while firing' }}
      onChange={(style) => change((next) => { next.style = style as typeof next.style; })} />
    <label className="mt-3 block"><span className="lab mb-1 block">Chase limit · {value.chaseLimit} tiles</span>
      <input type="range" className="w-full accent-[var(--color-arena-material)]" min={0} max={12} value={value.chaseLimit}
        onChange={(event) => change((next) => { next.chaseLimit = Number(event.target.value); })} /></label>
  </div>;
}

function UseEditor({ node, role, allOrders, catalog, mapPlan, onOpenMap, onChange }: {
  node: OrderNode; role: RoleDefinition; allOrders: OrdersDocument; catalog: TacticalSheetCatalog;
  mapPlan: MapPlanDocument; onOpenMap: (focus?: { kind: PlotKind; id: string }) => void;
  onChange: (next: NonNullable<OrderNode['use']>) => void;
}) {
  const skills = [...new Set(role.classes.map((classId) =>
    skillForClass(catalog, classId))
    .filter((value): value is string => Boolean(value)))];
  const authoredSkill = node.use?.skill;
  const skill = authoredSkill && skills.includes(authoredSkill)
    ? authoredSkill
    : skills[0] ?? '';
  const classInfo = catalog.classes.find((entry) => entry.signatureId === skill);
  const namedTargets = signatureTargets(skill);
  const friendlySelectors = ['repair-beam', 'exchange', 'arc-toss'].includes(skill)
    ? ['selector:most-experienced-ally', ...allOrders.roles.map((entry) => `role:${entry.id}`)]
    : [];
  const targets = [...namedTargets.map((target) => `named:${target}`), ...friendlySelectors];
  const selectedTarget = encodeUseTarget(node.use?.target ?? namedTargets[0] ?? 'self');
  return <div className="mt-4 rounded-sm border border-arena-material/30 bg-arena-material/5 p-3">
    <div className="flex items-center gap-3"><ClassIcon classId={classInfo?.id ?? role.classes[0]} size={46} /><div><p className="lab text-arena-material">{classInfo?.signatureName ?? humanize(skill)}</p><p className="t-meta mt-1">{classInfo?.fantasy}</p></div></div>
    {skills.length > 1 && <CardPicker label="Which class acts?" value={skill} values={skills}
      labeler={(entry) => catalog.classes.find((candidate) => candidate.signatureId === entry)?.name ?? humanize(entry)}
      descriptions={Object.fromEntries(skills.map((entry) => [entry,
        catalog.classes.find((candidate) => candidate.signatureId === entry)?.signatureName ?? humanize(entry)]))}
      onChange={(nextSkill) => onChange({ skill: nextSkill, target: signatureTargets(nextSkill)[0] ?? 'self' })} />}
    <CardPicker label="Skill target" value={selectedTarget} values={targets}
      labeler={(entry) => entry === 'selector:most-experienced-ally'
        ? 'Most experienced ally'
        : entry.startsWith('role:') ? `Role · ${humanize(entry.slice(5))}` : humanize(entry.slice(6))}
      descriptions={{ 'selector:most-experienced-ally': 'Protect, repair, or relay through the live veteran.' }}
      onChange={(target) => onChange({ ...node.use, skill, target: decodeUseTarget(target) })} />
    {['hardlight-block', 'trip-node', 'sentinel-seed'].includes(skill) && <div className="mt-3">
      <CardPicker label="Exact placement · optional" value={node.use?.at ?? ''}
        values={['', ...Object.keys(mapPlan.anchors)]} emptyLabel="Aim toward selected target"
        descriptions={Object.fromEntries(Object.keys(mapPlan.anchors).map((id) => [id,
          `Build exactly at ${humanize(id)}; pair with a Beside destination below this order.`]))}
        onChange={(at) => onChange({ ...node.use, skill, target: node.use?.target ?? namedTargets[0] ?? 'self',
          ...(at ? { at } : { at: undefined }) })} />
      {node.use?.at && <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
        onClick={() => onOpenMap({ kind: 'anchor', id: node.use!.at! })}>Plot {humanize(node.use.at)} on map</button>}
      <p className="t-micro mt-2">The body must be beside this point when the skill runs. Add a lower Go to → Beside order using the same anchor; no duplicate staging anchor is needed.</p>
    </div>}
    {skills.length > 1 && <p className="t-micro mt-2">Only bodies that own this skill take this order. Other classes in the role continue down the ladder.</p>}
  </div>;
}

function HandoffEditor({ value, roles, onChange }: { value: NonNullable<OrderNode['handoff']>; roles: RoleDefinition[]; onChange: (next: NonNullable<OrderNode['handoff']>) => void }) {
  const encoded = typeof value.target === 'string' ? value.target : `role:${value.target.role}`;
  const values = ['nearest-ally-toward-own-reactor', 'nearest-empty-handed-ally', ...roles.map((role) => `role:${role.id}`)];
  return <div className="mt-4"><CardPicker label="Adjacent receiver" value={encoded} values={values}
    labeler={(entry) => entry.startsWith('role:') ? `Role · ${humanize(entry.slice(5))}` : humanize(entry)}
    descriptions={{
      'nearest-ally-toward-own-reactor': 'Advance the Core toward home',
      'nearest-empty-handed-ally': 'Pass to the closest legal ally',
    }}
    onChange={(target) => onChange({ target: target.startsWith('role:') ? { role: target.slice(5) } : target as typeof value.target })} />
    <p className="t-micro mt-2">The receiver does not need a matching order. Handoff never implies movement.</p>
  </div>;
}

function CapacityField({ value, allOrders, onChange }: { value: OrderNode['capacity']; allOrders: OrdersDocument; onChange: (value: OrderNode['capacity']) => void }) {
  const mode = value === undefined ? 'none' : typeof value === 'number' ? 'inline' : 'shared';
  return <div className="mt-4 rounded-sm border border-arena-edge bg-arena-panel/45 p-3">
    <p className="lab">Target capacity</p><p className="t-micro mt-1">Limit bodies claiming this same resolved Core, enemy, zone, route or other target.</p>
    <div className="mt-2 grid grid-cols-3 gap-1.5">{(['none', 'inline', 'shared'] as const).map((candidate) => <button key={candidate}
      type="button" className="choice-card min-h-12" data-selected={mode === candidate}
      disabled={candidate === 'shared' && Object.keys(allOrders.capacities).length === 0}
      onClick={() => onChange(candidate === 'none' ? undefined : candidate === 'inline' ? 1 : Object.keys(allOrders.capacities)[0])}>{humanize(candidate)}</button>)}</div>
    {mode === 'inline' && <label className="mt-2 block"><span className="t-micro mb-1 block">Bodies per resolved target</span><input type="number" className="field min-h-11" min={1} max={8} value={value as number}
      onChange={(event) => onChange(Number(event.target.value))} /></label>}
    {mode === 'shared' && <CardPicker label="Shared capacity" value={value as string} values={Object.keys(allOrders.capacities)} onChange={onChange} />}
  </div>;
}

function ConditionsEditor({ orders, mapPlan, issues, onChange }: { orders: OrdersDocument; mapPlan: MapPlanDocument; issues: SheetIssue[]; onChange: (next: OrdersDocument) => void }) {
  const ids = Object.keys(orders.conditions);
  const [selected, setSelected] = useState(ids[0] ?? '');
  const current = orders.conditions[selected];
  const commit = (change: (next: OrdersDocument) => void) => { const next = structuredClone(orders); change(next); onChange(next); };
  const add = () => {
    const id = uniqueId('new-condition', ids);
    commit((next) => { next.conditions[id] = { fact: 'self-health-percent', operator: 'at-most', value: 35 }; }); setSelected(id);
  };
  return <div className="grid gap-3 lg:grid-cols-[220px_minmax(0,1fr)]">
    <div><div className="flex gap-1 overflow-x-auto lg:flex-col">{ids.map((id) => <button key={id} type="button" className="choice-card min-h-12 min-w-[160px] lg:min-w-0"
      data-selected={id === selected} onClick={() => setSelected(id)}>{humanize(id)}</button>)}</div>
      <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full" onClick={add}>＋ New condition</button></div>
    {current ? <div className="rounded-sm border border-arena-edge p-3" data-sheet-path={`orders.conditions.${selected}`}>
      <label><span className="lab mb-1 block">Condition name</span><input className="field min-h-11" value={selected}
        onChange={(event) => {
          const id = identifierFromName(event.target.value, selected);
          if (id === selected || orders.conditions[id]) return;
          commit((next) => { next.conditions[id] = next.conditions[selected]; delete next.conditions[selected]; renameCondition(next, selected, id); }); setSelected(id);
        }} /></label>
      <p className="lab mb-2 mt-4">Observed fact</p>
      <div className="grid max-h-64 gap-1.5 overflow-auto sm:grid-cols-2 xl:grid-cols-3">{CONDITION_FACTS.map((fact) => <button key={fact} type="button"
        className="choice-card min-h-14" data-selected={current.fact === fact} onClick={() => commit((next) => {
          next.conditions[selected] = normalizedCondition(fact, next.conditions[selected], next, mapPlan);
        })}><b className="t-body block">{humanize(fact)}</b><span className="t-micro mt-1 block">{factScope(fact)}</span></button>)}</div>
      <p className="lab mb-2 mt-4">Comparison</p>
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-5">{CONDITION_OPERATORS.map((operator) => <button key={operator} type="button"
        className="choice-card min-h-12" data-selected={current.operator === operator}
        onClick={() => commit((next) => { next.conditions[selected].operator = operator; })}>{operatorLabel(operator)}</button>)}</div>
      <label className="mt-3 block max-w-xs"><span className="t-micro mb-1 block">Compare with</span><input type="number" className="field min-h-11" value={current.value}
        onChange={(event) => commit((next) => { next.conditions[selected].value = Number(event.target.value); })} /></label>
      <ConditionQualifiers condition={current} roles={orders.roles} mapPlan={mapPlan}
        onChange={(condition) => commit((next) => { next.conditions[selected] = condition; })} />
      <IssueList issues={issues.filter((issue) => issue.path.startsWith(`orders.conditions.${selected}`))} />
      <button type="button" className="btn mt-4 min-h-11 text-arena-hot" onClick={() => { commit((next) => { delete next.conditions[selected]; }); setSelected(ids.find((id) => id !== selected) ?? ''); }}>Delete condition</button>
    </div> : <p className="t-meta">Create a named condition to drive an order.</p>}
  </div>;
}

function ConditionQualifiers({ condition, roles, mapPlan, onChange }: { condition: ConditionDefinition; roles: RoleDefinition[]; mapPlan: MapPlanDocument; onChange: (next: ConditionDefinition) => void }) {
  const kind = conditionSubjectKind(condition.fact);
  const change = (mutate: (next: ConditionDefinition) => void) => { const next = structuredClone(condition); mutate(next); onChange(next); };
  return <div className="mt-3 grid gap-2 sm:grid-cols-2">
    {(kind === 'role' || kind === 'role-zone') && <CardPicker label="Role" value={condition.role ?? ''} values={roles.map((role) => role.id)} onChange={(role) => change((next) => { next.role = role; })} />}
    {(kind === 'zone' || kind === 'role-zone') && <CardPicker label="Map zone" value={condition.zone ?? ''} values={Object.keys(mapPlan.zones)} onChange={(zone) => change((next) => { next.zone = zone; })} />}
    {kind === 'well' && <CardPicker label="Well" value={condition.well ?? ''} values={['north', 'centre', 'south', 'own', 'enemy']} onChange={(well) => change((next) => { next.well = well as ConditionDefinition['well']; })} />}
    {['known-enemy-carriers', 'remembered-enemies-in-zone'].includes(condition.fact) && <OptionalTicks label="Knowledge freshness" value={condition.freshnessTicks} min={1} max={600} placeholder="ticks"
      onChange={(value) => change((next) => { next.freshnessTicks = value ?? 1; })} />}
  </div>;
}

function CapacitiesEditor({ orders, issues, onChange }: { orders: OrdersDocument; issues: SheetIssue[]; onChange: (next: OrdersDocument) => void }) {
  const ids = Object.keys(orders.capacities);
  const commit = (change: (next: OrdersDocument) => void) => { const next = structuredClone(orders); change(next); onChange(next); };
  return <div className="grid gap-2 sm:grid-cols-2">{ids.map((id) => <article key={id} className="rounded-sm border border-arena-edge p-3" data-sheet-path={`orders.capacities.${id}`}>
    <label><span className="lab mb-1 block">Capacity name</span><input className="field min-h-11" value={id} onChange={(event) => {
      const nextId = identifierFromName(event.target.value, id); if (nextId === id || orders.capacities[nextId]) return;
      commit((next) => { next.capacities[nextId] = next.capacities[id]; delete next.capacities[id]; rewriteCapacity(next, id, nextId); });
    }} /></label>
    <label className="mt-3 block"><span className="t-micro mb-1 block">Bodies per resolved target</span><input type="number" className="field min-h-11" min={1} max={8} value={orders.capacities[id]}
      onChange={(event) => commit((next) => { next.capacities[id] = Number(event.target.value); })} /></label>
    <IssueList issues={issues.filter((issue) => issue.path.startsWith(`orders.capacities.${id}`))} />
    <button type="button" className="btn mt-3 min-h-11 text-arena-hot" onClick={() => commit((next) => { delete next.capacities[id]; })}>Delete</button>
  </article>)}
  <button type="button" className="choice-card min-h-28" onClick={() => commit((next) => { next.capacities[uniqueId('shared-capacity', ids)] = 2; })}>
    <b className="type-display block text-[16px]">＋ Add capacity</b><span className="t-micro mt-1 block">Use it from at least two action leaves.</span>
  </button></div>;
}

function SharedOrdersEditor({ catalog, orders, mapPlan, issues, onChange, onOpenMap }: { catalog: TacticalSheetCatalog; orders: OrdersDocument; mapPlan: MapPlanDocument; issues: SheetIssue[]; onChange: (next: OrdersDocument) => void; onOpenMap: (focus?: { kind: PlotKind; id: string }) => void }) {
  const ids = Object.keys(orders.sharedOrders);
  const [selected, setSelected] = useState(ids[0] ?? '');
  const [adding, setAdding] = useState(false);
  const definition = orders.sharedOrders[selected];
  const exampleRole = orders.roles[0];
  const commit = (change: (next: OrdersDocument) => void) => { const next = structuredClone(orders); change(next); onChange(next); };
  const add = (definition: OrderNode) => {
    const id = uniqueId('shared-order', ids);
    commit((next) => { next.sharedOrders[id] = definition; });
    setSelected(id);
    setAdding(false);
  };
  return <div className="grid gap-3 lg:grid-cols-[220px_minmax(0,1fr)]">
    <div><div className="flex gap-1 overflow-x-auto lg:flex-col">{ids.map((id) => <button key={id} type="button" className="choice-card min-h-12 min-w-[170px] lg:min-w-0" data-selected={id === selected} onClick={() => setSelected(id)}>{humanize(id)}</button>)}</div>
      <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full" aria-expanded={adding}
        onClick={() => setAdding((value) => !value)}>＋ New shared order</button>
      {adding && <div className="mt-2 grid gap-1.5">
        {ACTIONS.map((action) => <button key={action} type="button" className="choice-card min-h-[68px] text-left"
          onClick={() => {
            const signatureSkill = skillForClass(catalog, exampleRole?.classes[0] ?? '') ?? 'vector-dash';
            const definition = freshOrder(action, 'new-order', signatureSkill);
            delete definition.id;
            add(definition);
          }}>
          <span className="flex items-center gap-2"><span className="font-mono text-[18px] text-arena-material">{ACTION_COPY[action].glyph}</span><b className="type-display text-[14px]">{ACTION_COPY[action].label}</b></span>
          <span className="t-micro mt-1 block">{ACTION_COPY[action].description}</span>
        </button>)}
        <button type="button" className="choice-card min-h-[68px] text-left" onClick={() => add({ orders: [freshOrder('move', 'opening-move')] })}>
          <b className="type-display block text-[14px]">⌘ Nested play</b><span className="t-micro mt-1 block">One lifecycle around a short local priority list.</span>
        </button>
      </div>}
    </div>
    {definition && exampleRole ? <div><label className="mb-2 block"><span className="lab mb-1 block">Shared order name</span><input className="field min-h-11" value={selected}
      onChange={(event) => { const id = identifierFromName(event.target.value, selected); if (id === selected || orders.sharedOrders[id]) return; commit((next) => { next.sharedOrders[id] = next.sharedOrders[selected]; delete next.sharedOrders[selected]; rewriteShared(next, selected, id); }); setSelected(id); }} /></label>
      <OrderCard node={definition} externalId={selected} path={`orders.sharedOrders.${selected}`} role={exampleRole}
        allOrders={orders} mapPlan={mapPlan} catalog={catalog} issues={issues} depth={0} open
        openPath={null}
        onToggle={() => undefined} onOpenPath={() => undefined} onOpenMap={onOpenMap}
        onChange={(node) => commit((next) => { next.sharedOrders[selected] = node; })}
        onMove={() => undefined} onDuplicate={() => undefined} onDelete={() => undefined} first last />
      <button type="button" className="btn mt-3 min-h-11 text-arena-hot" onClick={() => { commit((next) => { delete next.sharedOrders[selected]; }); setSelected(ids.find((id) => id !== selected) ?? ''); }}>Delete shared order</button>
    </div> : <p className="t-meta">Create an exact reusable order.</p>}
  </div>;
}

function SharedReference({ node, allOrders, onChange }: { node: OrderNode; allOrders: OrdersDocument; onChange: (next: OrderNode) => void }) {
  return <div><p className="t-meta">This priority slot expands one exact shared definition. Edit the definition from Shared orders; this reference has no overrides.</p>
    <CardPicker label="Shared order" value={node.shared ?? ''} values={Object.keys(allOrders.sharedOrders)} onChange={(shared) => onChange({ shared })} />
  </div>;
}

function CardPicker({ label, value, values, descriptions = {}, emptyLabel = 'None', labeler = humanize, onChange }: {
  label: string; value: string; values: readonly string[]; descriptions?: Record<string, string>; emptyLabel?: string;
  labeler?: (value: string) => string; onChange: (value: string) => void;
}) {
  return <fieldset className="mt-3"><legend className="lab mb-2">{label}</legend>
    <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">{values.map((candidate) => <button key={candidate || 'empty'} type="button"
      className="choice-card min-h-12" data-selected={value === candidate} onClick={() => onChange(candidate)}>
      <b className="t-body block">{candidate ? labeler(candidate) : emptyLabel}</b>{descriptions[candidate] && <span className="t-micro mt-1 block">{descriptions[candidate]}</span>}
    </button>)}</div>
  </fieldset>;
}

function OptionalTicks({ label, value, placeholder, min, max, onChange }: { label: string; value: number | undefined; placeholder: string; min: number; max: number; onChange: (value: number | undefined) => void }) {
  return <label><span className="t-micro mb-1 block">{label} · ticks</span><input type="number" inputMode="numeric" className="field min-h-11" min={min} max={max}
    value={value ?? ''} placeholder={placeholder} onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))} /></label>;
}

function Modal({ title, description, children, onClose }: { title: string; description: string; children: ReactNode; onClose: () => void }) {
  return <div className="fixed inset-0 z-[80] overflow-y-auto bg-black/75 p-2 sm:p-6" role="dialog" aria-modal="true" aria-label={title}
    onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section className="panel mx-auto max-w-6xl overflow-hidden">
      <header className="sticky top-0 z-10 flex items-start gap-3 border-b border-arena-edge bg-arena-panel p-3">
        <div className="mr-auto"><p className="lab text-arena-material">Tactical library</p><h2 className="type-display mt-1 text-[22px]">{title}</h2><p className="t-meta mt-1">{description}</p></div>
        <button type="button" className="btn min-h-11 min-w-11" aria-label="Close" onClick={onClose}>×</button>
      </header>
      <div className="p-3 sm:p-4">{children}</div>
    </section>
  </div>;
}

function IssueList({ issues }: { issues: SheetIssue[] }) {
  if (issues.length === 0) return null;
  return <ul className="mt-3 space-y-1 rounded-sm border border-arena-hot/35 bg-arena-hot/5 p-2 text-xs">{issues.map((issue, index) => <li key={`${issue.path}:${index}`}
    className={issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'}>{issue.message}</li>)}</ul>;
}

function allOrderIds(orders: OrdersDocument): string[] {
  const ids: string[] = Object.keys(orders.sharedOrders);
  const walk = (node: OrderNode) => { if (node.id) ids.push(node.id); node.orders?.forEach(walk); };
  orders.roles.forEach((role) => role.orders.forEach(walk));
  Object.values(orders.sharedOrders).forEach(walk);
  return ids;
}

function orderSummary(node: OrderNode, orders: OrdersDocument): string {
  if (node.shared) return orderSummary(orders.sharedOrders[node.shared] ?? {}, orders);
  if (node.orders) return `${node.orders.length} nested priorities${node.when ? ` · when ${conditionSummary(node.when)}` : ''}`;
  if (node.move) {
    const destination = node.move.patrol !== undefined
      ? node.move.patrol ? `patrol ${humanize(node.move.patrol)}` : 'choose a patrol route'
      : node.move.follow !== undefined
        ? node.move.follow
          ? typeof node.move.follow === 'string'
            ? `follow ${humanize(node.move.follow)}`
            : 'follow the most experienced ally'
          : 'choose a role to follow'
        : node.move.to
          ? `go to ${humanize(encodeMoveTarget(node.move.to).split(':')[1])}`
          : 'choose a destination';
    return `${destination}${node.when ? ` · when ${conditionSummary(node.when)}` : ''}`;
  }
  if (node.fight) return `${humanize(node.fight.style)} · ${node.fight.targets.map(humanize).join(' → ')}`;
  if (node.use) return node.use.at
    ? `${humanize(node.use.skill)} at ${humanize(node.use.at)}`
    : `${humanize(node.use.skill)} on ${useTargetLabel(node.use.target)}`;
  if (node.handoff) return `pass to ${humanize(typeof node.handoff.target === 'string' ? node.handoff.target : node.handoff.target.role)}`;
  if (node.drop) return 'release carried Core toward home';
  return 'Choose an action';
}

function conditionSummary(expression: ConditionExpression) {
  if (typeof expression === 'string') return humanize(expression);
  const connector = 'all' in expression ? ' AND ' : ' OR ';
  return conditionExpressionNames(expression).map(humanize).join(connector);
}

function encodeMoveTarget(value: MoveTarget): string {
  if (typeof value === 'string') return `dynamic:${value}`;
  if ('zone' in value) return `zone:${value.zone}`;
  if ('anchor' in value) return `anchor:${value.anchor}`;
  if ('adjacentTo' in value) return `adjacentTo:${value.adjacentTo}`;
  return `well:${value.well}`;
}

function decodeMoveTarget(value: string): MoveTarget {
  const [kind, id] = value.split(':');
  if (kind === 'dynamic') return id as MoveTarget;
  if (kind === 'zone') return { zone: id };
  if (kind === 'anchor') return { anchor: id };
  if (kind === 'adjacentTo') return { adjacentTo: id };
  return { well: id as 'north' | 'centre' | 'south' | 'own' | 'enemy' };
}

function encodeFollowTarget(value: MoveOrder['follow']): string {
  if (!value) return '';
  return typeof value === 'string' ? `role:${value}` : `selector:${value.selector}`;
}

function decodeFollowTarget(value: string): NonNullable<MoveOrder['follow']> {
  return value.startsWith('role:')
    ? value.slice(5)
    : { selector: 'most-experienced-ally' };
}

function encodeUseTarget(value: UseTarget): string {
  if (typeof value === 'string') return `named:${value}`;
  return 'role' in value ? `role:${value.role}` : `selector:${value.selector}`;
}

function decodeUseTarget(value: string): UseTarget {
  if (value.startsWith('named:')) return value.slice(6);
  if (value.startsWith('role:')) return { role: value.slice(5) };
  return { selector: 'most-experienced-ally' };
}

function useTargetLabel(value: UseTarget): string {
  if (typeof value === 'string') return humanize(value);
  return 'role' in value
    ? `role ${humanize(value.role)}`
    : 'most experienced ally';
}

function mapHighlight(move: MoveOrder): { kind: PlotKind; id: string } | null {
  if (move.patrol) return { kind: 'route', id: move.patrol };
  if (typeof move.to === 'object' && 'zone' in move.to) return { kind: 'zone', id: move.to.zone };
  if (typeof move.to === 'object' && 'anchor' in move.to) return { kind: 'anchor', id: move.to.anchor };
  if (typeof move.to === 'object' && 'adjacentTo' in move.to) return { kind: 'anchor', id: move.to.adjacentTo };
  return move.via ? { kind: 'route', id: move.via } : null;
}

function normalizedCondition(fact: string, previous: ConditionDefinition, orders: OrdersDocument, mapPlan: MapPlanDocument): ConditionDefinition {
  const next: ConditionDefinition = { fact, operator: previous.operator, value: previous.value };
  const kind = conditionSubjectKind(fact);
  if (kind === 'role' || kind === 'role-zone') next.role = previous.role ?? orders.roles[0]?.id;
  if (kind === 'zone' || kind === 'role-zone') next.zone = previous.zone ?? Object.keys(mapPlan.zones)[0];
  if (kind === 'well') next.well = previous.well ?? 'centre';
  if (['known-enemy-carriers', 'remembered-enemies-in-zone'].includes(fact)) next.freshnessTicks = previous.freshnessTicks ?? 60;
  if (fact === 'self-nearest-visible-enemy-distance') {
    next.operator = 'at-most';
    next.value = previous.fact === fact ? previous.value : 7;
  }
  if (['self-carrying', 'self-in-zone', 'signature-ready'].includes(fact)) {
    next.operator = 'equals';
    next.value = previous.fact === fact && previous.value === 0 ? 0 : 1;
  }
  return next;
}

function rewriteCapacity(orders: OrdersDocument, previous: string, nextId: string) {
  walk(orders, (node) => { if (node.capacity === previous) node.capacity = nextId; });
}

function rewriteShared(orders: OrdersDocument, previous: string, nextId: string) {
  walk(orders, (node) => { if (node.shared === previous) node.shared = nextId; }, false);
}

function walk(orders: OrdersDocument, visit: (node: OrderNode) => void, includeShared = true) {
  const descend = (node: OrderNode) => { visit(node); node.orders?.forEach(descend); };
  orders.roles.forEach((role) => role.orders.forEach(descend));
  if (includeShared) Object.values(orders.sharedOrders).forEach(descend);
}

function factScope(fact: string) {
  if (fact === 'self-nearest-visible-enemy-distance') return 'Steps for this body to join';
  if (fact.startsWith('self-') || fact === 'signature-ready') return 'This body';
  if (fact.startsWith('role-')) return 'A named role';
  if (fact.includes('-in-zone')) return 'A plotted zone';
  if (fact.startsWith('well-')) return 'A Core Well';
  return 'Whole team knowledge';
}

function operatorLabel(operator: string) {
  return operator === 'at-least' ? '≥ at least' : operator === 'at-most' ? '≤ at most'
    : operator === 'equals' ? '= equals' : operator === 'less-than' ? '< less than' : '> greater than';
}

function priorityFromPath(path: string) {
  const match = path.match(/\.(\d+)$/); return match ? Number(match[1]) + 1 : 1;
}

function swap<T>(values: T[], left: number, right: number) {
  if (left < 0 || right < 0 || left >= values.length || right >= values.length) return;
  [values[left], values[right]] = [values[right], values[left]];
}

function humanize(value: string) {
  return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}
