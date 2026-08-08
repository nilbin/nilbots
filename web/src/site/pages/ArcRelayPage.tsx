import { useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import EntrantCrest from '../../components/EntrantCrest';
import ClassIcon from '../../components/ClassIcon';
import { useAuth } from '../auth';
import type {
  ArcRelayEntrant,
  TacticalSheetClass,
  TacticalSheetCatalog,
} from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import {
  useArcRelayCrestOptions,
  useArcRelayEntrants,
  useArcRelayLadder,
  useArcRelayLadderOptIn,
  useArcRelayPreflight,
  useCreateArcRelayMind,
  useLoadArcRelayMind,
  useReviseArcRelayMind,
  useSetArcRelayCrest,
  useTacticalSheetCatalog,
} from '../queries';
import TacticalSheetEditor from '../sheets/TacticalSheetEditor';

export default function ArcRelayPage() {
  const { user, loading } = useAuth();
  const catalogQuery = useTacticalSheetCatalog(user !== null);
  const entrantsQuery = useArcRelayEntrants(user !== null);
  const ladderQuery = useArcRelayLadder();

  if (loading) return <LoadingState label="Opening tactical sheets…" />;
  if (!user) {
    return (
      <section className="panel pad mx-auto max-w-xl text-center">
        <p className="lab mb-2">Arc Relay</p>
        <h1 className="type-display text-[28px]">Draw the plan. Run the match.</h1>
        <p className="t-meta mx-auto mt-3 max-w-md">
          Tactical sheets are server-saved entrants. Sign in to compose eight classes,
          author role doctrines, plot routes on the live map and trial a draft in seconds.
        </p>
        <Link to="/login?return=/relay" className="btn btn-on mt-5 inline-flex min-h-11">
          Sign in to author
        </Link>
      </section>
    );
  }
  if (catalogQuery.isError) {
    return <ErrorState error={catalogQuery.error} onRetry={() => void catalogQuery.refetch()} />;
  }
  if (catalogQuery.isPending || !catalogQuery.data) {
    return <LoadingState label="Loading tactical vocabulary and map…" />;
  }

  const catalog = catalogQuery.data;
  const templateClasses = templateComposition(catalog);
  return (
    <div className="mx-auto flex max-w-[1500px] flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="lab mb-2">Arc Relay · tactical workshop</p>
          <h1 className="type-display text-[30px]">Eight bodies. Conditional team strategy.</h1>
          <p className="t-meta mt-2 max-w-3xl">
            Choose the lineup, give each role an ordered doctrine, draw every route and zone,
            then compile and watch a trial. This editor emits only the current doctrine grammar.
          </p>
        </div>
        <span className="pill">{catalog.map.id}</span>
      </header>

      <EntrantRoster
        entrants={entrantsQuery.data ?? []}
        ladder={ladderQuery.data?.entrants ?? []}
        templateClasses={templateClasses}
        classes={catalog.classes}
      />
      <TacticalSheetEditor catalog={catalog} />
      <ClassCatalog catalog={catalog} />
    </div>
  );
}

function EntrantRoster({ entrants, ladder, templateClasses, classes }: {
  entrants: ArcRelayEntrant[];
  ladder: ArcRelayEntrant[];
  templateClasses: string[];
  classes: TacticalSheetClass[];
}) {
  const [name, setName] = useState('My relay mind');
  const [entryType, setEntryType] = useState('MyRelayMind');
  const [source, setSource] = useState(DEFAULT_MIND_SOURCE);
  const [composition, setComposition] = useState(templateClasses);
  const [editId, setEditId] = useState<string | null>(null);
  const [expectedRevision, setExpectedRevision] = useState(0);
  const create = useCreateArcRelayMind();
  const revise = useReviseArcRelayMind();
  const loadMind = useLoadArcRelayMind();
  const preflight = useArcRelayPreflight();
  const ladderOptIn = useArcRelayLadderOptIn();
  const crestOptions = useArcRelayCrestOptions();
  const setCrest = useSetArcRelayCrest();
  const submit = () => {
    const common = {
      name,
      entryType,
      files: [{ name: 'MyRelayMind.cs', content: source }],
      composition: { classIds: composition, adaptivePolicyId: null, adaptiveClassIds: [] },
    };
    if (editId) revise.mutate({ entrantId: editId, body: { ...common, expectedRevision } });
    else create.mutate({ ...common, crestVariant: 0 });
  };
  const beginRevision = (entrantId: string) => loadMind.mutate(entrantId, {
    onSuccess: (mind) => {
      setEditId(entrantId);
      setExpectedRevision(mind.entrant.revision);
      setName(mind.entrant.name);
      setEntryType(mind.entryType);
      setSource(mind.files[0]?.content ?? DEFAULT_MIND_SOURCE);
      setComposition([...mind.composition.classIds]);
      const editor = document.getElementById('mind-editor') as HTMLDetailsElement | null;
      if (editor) {
        editor.open = true;
        editor.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    },
  });
  return <>
    <section className="panel overflow-hidden">
      <div className="pad flex flex-wrap items-end gap-3 border-b border-arena-edge">
        <div className="mr-auto">
          <p className="lab">Your entrants</p>
          <p className="t-micro mt-1">Tactical sheets and custom minds share identity, crest and rating.</p>
        </div>
        <span className="pill">{entrants.filter((entry) => entry.ladderOptedIn).length}/3 fielded</span>
      </div>
      <div className="grid gap-2 p-3 md:grid-cols-2 xl:grid-cols-3">
        {entrants.map((entrant) => <article key={entrant.id}
          className="rounded-sm border border-arena-edge bg-arena-deep/40 p-3">
          <div className="flex items-center gap-3">
            <EntrantCrest crest={entrant.crest} size={52} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="type-display truncate text-[18px]">{entrant.name}</h3>
                <span className="pill ml-auto">{entrant.kind}</span>
              </div>
              <p className="t-micro">{entrant.rating} rating · {entrant.rankedMatches} ranked · r{entrant.revision}</p>
              <p className={clsx('t-micro mt-1', entrant.status === 'suspended' ? 'text-red-300' : 'text-arena-material')}>
                {entrant.status}{entrant.suspensionReason ? ` · ${entrant.suspensionReason}` : ''}
              </p>
            </div>
          </div>
          <CompositionStrip entrant={entrant} />
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" className="btn min-h-11" disabled={crestOptions.isPending}
              onClick={() => crestOptions.mutate(entrant.id)}>Choose crest</button>
            {entrant.kind === 'mind' && <button type="button" className="btn min-h-11"
              disabled={loadMind.isPending} onClick={() => beginRevision(entrant.id)}>Revise</button>}
            {entrant.kind === 'mind' && (entrant.status === 'required' || entrant.status === 'built') &&
              <button type="button" className="btn min-h-11" disabled={preflight.isPending}
                onClick={() => preflight.mutate(entrant.id)}>Run preflight</button>}
            {(entrant.kind === 'sheet' || entrant.status === 'passed') && entrant.status !== 'suspended' &&
              <button type="button" className={clsx('btn min-h-11', entrant.ladderOptedIn && 'btn-on')}
                disabled={ladderOptIn.isPending}
                onClick={() => ladderOptIn.mutate({ entrantId: entrant.id, optedIn: !entrant.ladderOptedIn })}>
                {entrant.ladderOptedIn ? 'Leave ladder' : 'Enter ladder'}
              </button>}
          </div>
          {crestOptions.data?.entrantId === entrant.id && (
            <div className="mt-3 flex flex-wrap gap-2 border-t border-arena-edge pt-3" aria-label="Crest choices">
              {crestOptions.data.options.map((crest) => <button key={crest.key} type="button"
                className="min-h-11 min-w-11 rounded-sm border border-arena-edge p-1 active:border-arena-material"
                aria-label={`Choose crest ${crest.variant}`} disabled={setCrest.isPending}
                onClick={() => setCrest.mutate({ entrantId: entrant.id, variant: crest.variant })}>
                <EntrantCrest crest={crest} size={38} />
              </button>)}
            </div>
          )}
        </article>)}
        {entrants.length === 0 && <p className="t-meta p-2">Save a tactical sheet or submit a mind to establish an entrant.</p>}
      </div>
    </section>

    <section className="panel pad">
      <details id="mind-editor">
        <summary className="min-h-11 cursor-pointer lab">{editId ? 'Revise custom mind' : 'Submit a custom mind'}</summary>
        <p className="t-meta mt-2 max-w-3xl">
          Sources follow the controlled server build path. The artifact runs only in the WASM sandbox;
          its eight-class declaration is validated and snapshotted beside every match.
        </p>
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <label><span className="t-micro block">Entrant name</span><input className="field min-h-11" value={name}
            maxLength={60} onChange={(event) => setName(event.target.value)} /></label>
          <label><span className="t-micro block">Entry type</span><input className="field min-h-11" value={entryType}
            onChange={(event) => setEntryType(event.target.value)} /></label>
        </div>
        <fieldset className="mt-3">
          <legend className="t-micro">Declared eight-body composition · two copies maximum</legend>
          <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
            {composition.map((classId, slot) => <label key={slot}>
              <span className="t-micro block">slot {slot + 1}</span>
              <select className="field min-h-11 py-1.5" value={classId} onChange={(event) => {
                const next = [...composition]; next[slot] = event.target.value; setComposition(next);
              }}>
                {classes.filter((entry) => entry.unlocked).map((entry) => {
                  const copies = composition.filter((value) => value === entry.id).length;
                  return <option key={entry.id} value={entry.id}
                    disabled={entry.id !== classId && copies >= 2}>{entry.name}</option>;
                })}
              </select>
            </label>)}
          </div>
        </fieldset>
        <label className="mt-3 block"><span className="t-micro block">C# source</span>
          <textarea className="field min-h-[260px] font-mono text-xs" spellCheck={false} value={source}
            onChange={(event) => setSource(event.target.value)} /></label>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <button type="button" className="btn btn-on min-h-11"
            disabled={create.isPending || revise.isPending || composition.length !== 8}
            onClick={submit}>{create.isPending || revise.isPending ? 'Submitting…' : editId ? 'Build revised mind' : 'Build mind entrant'}</button>
          {editId && <button type="button" className="btn min-h-11" onClick={() => {
            setEditId(null); setExpectedRevision(0); setName('My relay mind'); setEntryType('MyRelayMind');
            setSource(DEFAULT_MIND_SOURCE); setComposition(templateClasses);
          }}>New submission</button>}
          {(create.isError || revise.isError) && <span className="t-micro text-red-300">
            {(create.error ?? revise.error)?.message}</span>}
          {(create.isSuccess || revise.isSuccess) && <span className="t-micro text-emerald-300">
            Revision queued through the controlled toolchain; rating identity preserved.</span>}
        </div>
      </details>
    </section>

    <section className="panel overflow-hidden">
      <div className="pad border-b border-arena-edge">
        <p className="lab">Ranked Arc Relay</p>
        <p className="t-micro mt-1">Passive cross-account pairing · one rating surface for sheets and minds.</p>
      </div>
      <div className="divide-y divide-arena-edge">
        {ladder.map((entrant, index) => <div key={entrant.id} className="flex min-w-0 items-center gap-3 px-3 py-2.5">
          <span className="lab w-7 text-right">{index + 1}</span><EntrantCrest crest={entrant.crest} size={34} />
          <div className="min-w-0 flex-1"><p className="t-body truncate">{entrant.name}</p>
            <p className="t-micro">{entrant.kind} · {entrant.ownerDisplayName}</p></div>
          <CompositionStrip entrant={entrant} compact />
          <strong className="type-display text-[18px]">{entrant.rating}</strong>
        </div>)}
        {ladder.length === 0 && <p className="t-meta pad">The launch ladder is waiting for its first entrants.</p>}
      </div>
    </section>
  </>;
}

function CompositionStrip({ entrant, compact = false }: { entrant: ArcRelayEntrant; compact?: boolean }) {
  return <div className={clsx('flex min-w-0 gap-1', compact ? 'hidden max-w-[240px] lg:flex' : 'mt-3')}>
    {entrant.composition.map((slot) => <ClassIcon key={slot.slot}
      classId={slot.classId} label={`${slot.slot + 1}. ${slot.className}`} size={24} />)}
  </div>;
}

function ClassCatalog({ catalog }: { catalog: TacticalSheetCatalog }) {
  return <section>
    <p className="lab mb-2">Launch classes</p>
    <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
      {catalog.classes.map((entry) => <article key={entry.id}
        className={clsx('panel pad min-h-[142px]', !entry.unlocked && 'opacity-55')}>
        <div className="flex items-start gap-2"><h3 className="type-display text-[18px]">{entry.name}</h3>
          <span className="pill ml-auto">{entry.unlocked ? 'unlocked' : 'locked'}</span></div>
        <p className="lab mt-2 text-arena-material">{entry.signatureName}</p>
        <p className="t-micro mt-2">{entry.fantasy}</p>
      </article>)}
    </div>
  </section>;
}

function templateComposition(catalog: TacticalSheetCatalog): string[] {
  try {
    const value = JSON.parse(catalog.templatePlaybookJson) as { composition?: unknown };
    if (Array.isArray(value.composition))
      return value.composition.filter((entry): entry is string => typeof entry === 'string');
  } catch {
    // The server compiler guards this source. Fall through only for a partial deployment.
  }
  return catalog.classes.filter((entry) => entry.unlocked).slice(0, 8).map((entry) => entry.id);
}

const DEFAULT_MIND_SOURCE = `using BotArena.Sdk;

public sealed class MyRelayMind : IGenericMindBot
{
    public void StartMatch(MindStart start) { }

    public void Think(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
            body.Hold("author your Arc Relay plan here");
    }

    public void EndMatch(MindEnd end) { }
}`;
