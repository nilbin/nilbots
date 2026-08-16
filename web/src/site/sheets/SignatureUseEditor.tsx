import ClassIcon from '../../components/ClassIcon';
import type { TacticalSheetCatalog } from '../api';
import {
  defaultSignatureTarget,
  signatureTargets,
  skillForClass,
  type MapPlanDocument,
  type RoleDefinition,
  type UseOrder,
  type UseTarget,
} from './sheetAuthoring';

export type SignatureMapFocus = {
  kind: 'route' | 'zone' | 'anchor';
  id: string;
};

/**
 * The one descriptor-driven signature editor used by direct Orders and linked
 * Play opportunities. It deliberately has no signature-id feature switches.
 */
export default function SignatureUseEditor({
  value,
  role,
  roles,
  catalog,
  mapPlan,
  onOpenMap,
  onChange,
}: {
  value: UseOrder;
  role: RoleDefinition;
  roles: RoleDefinition[];
  catalog: TacticalSheetCatalog;
  mapPlan: MapPlanDocument;
  onOpenMap: (focus?: SignatureMapFocus) => void;
  onChange: (next: UseOrder) => void;
}) {
  const skills = [...new Set(role.classes.map((classId) =>
    skillForClass(catalog, classId)).filter((entry): entry is string => Boolean(entry)))];
  const skill = skills.includes(value.skill) ? value.skill : skills[0] ?? '';
  const classInfo = catalog.classes.find((entry) => entry.signatureId === skill);
  const descriptor = classInfo?.signatureAuthoring;
  const namedTargets = signatureTargets(catalog, skill);
  const friendlyTargets = descriptor?.allowsFriendlyRole
    ? [
      ...(descriptor.allowsMostExperiencedAlly
        ? ['selector:most-experienced-ally']
        : []),
      ...roles.map((entry) => `role:${entry.id}`),
    ]
    : [];
  const targetChoices = [
    ...namedTargets.map((target) => `named:${target}`),
    ...friendlyTargets,
  ];
  const selectedTarget = encodeTarget(value.target);
  const roleTarget = typeof value.target === 'object' && 'role' in value.target
    ? value.target
    : null;
  const routeIds = Object.keys(mapPlan.routes);
  const forwardRole = Boolean(
    descriptor?.allowsRouteAheadRole && roleTarget?.aheadVia,
  );

  if (!descriptor || !classInfo) {
    return <div className="mt-4 rounded-sm border border-arena-hot/40 bg-arena-hot/5 p-3" role="alert">
      <p className="lab text-arena-hot">Signature unavailable</p>
      <p className="t-meta mt-1">This role has no catalog-backed signature authoring descriptor.</p>
    </div>;
  }

  return <div className="mt-4 rounded-sm border border-arena-material/30 bg-arena-material/5 p-3">
    <div className="flex items-center gap-3">
      <ClassIcon classId={classInfo.id} size={46} />
      <div className="min-w-0 flex-1">
        <p className="lab text-arena-material">{classInfo.signatureName}</p>
        <p className="t-meta mt-1">{classInfo.fantasy}</p>
        <p className="t-micro mt-1">
          {humanize(descriptor.category)} · {humanize(descriptor.argumentKind)} · range {descriptor.engagementRange} · cooldown {descriptor.cooldownTicks}
        </p>
      </div>
    </div>
    {skills.length > 1 && <ChoiceCards label="Which class acts?" value={skill} values={skills}
      labeler={(entry) => catalog.classes.find((candidate) => candidate.signatureId === entry)?.name ?? humanize(entry)}
      descriptions={Object.fromEntries(skills.map((entry) => [entry,
        catalog.classes.find((candidate) => candidate.signatureId === entry)?.signatureName ?? humanize(entry)]))}
      onChange={(nextSkill) => onChange({
        skill: nextSkill,
        target: defaultSignatureTarget(catalog, nextSkill),
      })} />}
    <ChoiceCards label="Skill target" value={selectedTarget} values={targetChoices}
      labeler={(entry) => entry === 'selector:most-experienced-ally'
        ? 'Most experienced ally'
        : entry.startsWith('role:')
          ? `Role · ${humanize(entry.slice(5))}`
          : humanize(entry.slice(6))}
      descriptions={{
        'selector:most-experienced-ally': 'Protect, repair, or relay through the live veteran.',
      }}
      onChange={(target) => onChange({
        ...value,
        skill,
        target: decodeTarget(target),
      })} />
    {descriptor.allowsRouteAheadRole && roleTarget && <div className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/55 p-3">
      <label className="flex min-h-11 items-center gap-3">
        <input type="checkbox" className="h-5 w-5 accent-[var(--color-arena-material)]"
          checked={forwardRole} disabled={routeIds.length === 0}
          onChange={(event) => onChange({
            ...value,
            skill,
            target: event.target.checked
              ? { role: roleTarget.role, aheadVia: routeIds[0], minimumForwardTiles: 4 }
              : { role: roleTarget.role },
          })} />
        <span><span className="lab block">Forward receiver only</span>
          <span className="t-micro mt-1 block">Use only when this role is measurably ahead on an authored route.</span></span>
      </label>
      {routeIds.length === 0 && <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
        onClick={() => onOpenMap()}>Draw the progress route first</button>}
      {forwardRole && <>
        <ChoiceCards label="Progress route" value={roleTarget.aheadVia!} values={routeIds}
          labeler={humanize}
          descriptions={Object.fromEntries(routeIds.map((id) => [id,
            'The receiver must be farther along this ordered route.']))}
          onChange={(aheadVia) => onChange({ ...value, skill, target: {
            ...roleTarget,
            aheadVia,
            minimumForwardTiles: roleTarget.minimumForwardTiles ?? 4,
          } })} />
        <label className="mt-3 block">
          <span className="lab mb-1 block">Minimum forward gain · {roleTarget.minimumForwardTiles ?? 4} tiles</span>
          <input type="range" className="w-full accent-[var(--color-arena-material)]"
            min={1} max={5} value={roleTarget.minimumForwardTiles ?? 4}
            onChange={(event) => onChange({ ...value, skill, target: {
              ...roleTarget,
              minimumForwardTiles: Number(event.target.value),
            } })} />
        </label>
        <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
          onClick={() => onOpenMap({ kind: 'route', id: roleTarget.aheadVia! })}>
          Inspect {humanize(roleTarget.aheadVia!)}
        </button>
      </>}
    </div>}
    {descriptor.allowsExactAnchor && <div className="mt-3">
      <ChoiceCards label="Exact aim or placement · optional" value={value.at ?? ''}
        values={['', ...Object.keys(mapPlan.anchors)]} emptyLabel="Aim toward selected target"
        descriptions={Object.fromEntries(Object.keys(mapPlan.anchors).map((id) => [id,
          `Use exactly at or toward ${humanize(id)}.`]))}
        onChange={(at) => {
          const next = { ...value, skill, target: value.target ?? defaultSignatureTarget(catalog, skill) };
          if (at) next.at = at;
          else delete next.at;
          onChange(next);
        }} />
      {value.at && <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
        onClick={() => onOpenMap({ kind: 'anchor', id: value.at! })}>
        Inspect {humanize(value.at)} on map
      </button>}
      <p className="t-micro mt-2">Exact geometry remains an ordinary authored anchor. Placement skills must still stage within a legal use position.</p>
    </div>}
    {descriptor.defaultCommitmentBehavior === 'preserve-by-default' && <p className="t-micro mt-3 rounded-sm border border-arena-edge bg-arena-panel/50 p-2">
      This signature normally preserves its authoritative tell or channel. A linked opportunity can scaffold an ordinary hold continuation; higher-priority Orders may still interrupt it.
    </p>}
    {skills.length > 1 && <p className="t-micro mt-2">Only bodies that own this skill take this order. Other classes in the role continue down the ladder.</p>}
  </div>;
}

function ChoiceCards({
  label,
  value,
  values,
  descriptions = {},
  emptyLabel = 'None',
  labeler = humanize,
  onChange,
}: {
  label: string;
  value: string;
  values: readonly string[];
  descriptions?: Record<string, string>;
  emptyLabel?: string;
  labeler?: (value: string) => string;
  onChange: (value: string) => void;
}) {
  return <fieldset className="mt-3">
    <legend className="lab mb-2">{label}</legend>
    <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">
      {values.map((candidate) => <button key={candidate || 'empty'} type="button"
        className="choice-card min-h-12" data-selected={value === candidate}
        aria-pressed={value === candidate} onClick={() => onChange(candidate)}>
        <b className="t-body block">{candidate ? labeler(candidate) : emptyLabel}</b>
        {descriptions[candidate] && <span className="t-micro mt-1 block">{descriptions[candidate]}</span>}
      </button>)}
    </div>
  </fieldset>;
}

function encodeTarget(value: UseTarget): string {
  if (typeof value === 'string') return `named:${value}`;
  return 'role' in value ? `role:${value.role}` : `selector:${value.selector}`;
}

function decodeTarget(value: string): UseTarget {
  if (value.startsWith('named:')) return value.slice(6);
  if (value.startsWith('role:')) return { role: value.slice(5) };
  return { selector: 'most-experienced-ally' };
}

function humanize(value: string) {
  return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}
