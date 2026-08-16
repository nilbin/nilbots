import { Link } from 'react-router-dom';
import clsx from 'clsx';
import EntrantCrest from '../../components/EntrantCrest';
import ClassIcon from '../../components/ClassIcon';
import { useAuth } from '../auth';
import type {
  ArcRelayEntrant,
  TacticalSheetCatalog,
} from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import {
  useArcRelayCrestOptions,
  useArcRelayEntrants,
  useArcRelayLadderOptIn,
  useArcRelaySeason,
  useSetArcRelayCrest,
  useTacticalSheetCatalog,
} from '../queries';
import TacticalSheetEditor from '../sheets/TacticalSheetEditor';

export default function ArcRelayPage({
  view = 'overview',
  sheetId = null,
}: {
  view?: 'overview' | 'sheets';
  sheetId?: string | null;
}) {
  const { user, loading } = useAuth();
  const catalogQuery = useTacticalSheetCatalog(user !== null);
  const entrantsQuery = useArcRelayEntrants(user !== null);
  const seasonQuery = useArcRelaySeason();

  if (loading) return <LoadingState label="Opening tactical sheets…" />;
  if (!user) {
    return (
      <section className="panel pad mx-auto max-w-xl text-center">
        <p className="lab mb-2">Season 1</p>
        <h1 className="type-display text-[28px]">Build an entrant. Watch it compete.</h1>
        <p className="t-meta mx-auto mt-3 max-w-md">
          Sign in to author a tactical sheet, run an immediate practice match,
          inspect the replay and return to the same draft.
        </p>
        <Link
          to={`/login?returnUrl=${encodeURIComponent(view === 'sheets' ? '/entrants/sheets/new' : '/entrants')}`}
          className="btn btn-primary mt-5 min-h-11"
        >
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
  if (view === 'overview' && entrantsQuery.isError) {
    return <ErrorState error={entrantsQuery.error} onRetry={() => void entrantsQuery.refetch()} />;
  }
  if (view === 'overview' && entrantsQuery.isPending) {
    return <LoadingState label="Loading entrants…" />;
  }

  const catalog = catalogQuery.data;
  return (
    <div className="mx-auto flex max-w-[1500px] flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="lab mb-2">{view === 'sheets' ? 'Sheet workshop' : 'Season roster'}</p>
          <h1 className="type-display text-[30px]">
            {view === 'sheets' ? 'Change. Run. Watch. Adjust.' : 'Build and manage entrants.'}
          </h1>
          <p className="t-meta mt-2 max-w-3xl">
            {view === 'sheets'
              ? 'Cast eight class bodies into roles, build map-first Programs on Tapline and compare what actually happened.'
              : 'Each tactical sheet is a Season entrant with its own crest, composition, match history and rating.'}
          </p>
        </div>
        <span className="pill">Tapline · Season map</span>
      </header>

      {view === 'overview' ? (
        <EntrantRoster
          entrants={entrantsQuery.data ?? []}
          baselineMatchesPerDay={seasonQuery.data?.baselineMatchesPerEntrantPerDay}
        />
      ) : (
        <>
          <TacticalSheetEditor catalog={catalog} initialSheetId={sheetId} />
          <details className="panel pad">
            <summary className="lab min-h-11 cursor-pointer">Class reference · {catalog.classes.length} launch classes</summary>
            <div className="mt-3"><ClassCatalog catalog={catalog} /></div>
          </details>
        </>
      )}
    </div>
  );
}

function EntrantRoster({ entrants, baselineMatchesPerDay }: {
  entrants: ArcRelayEntrant[];
  baselineMatchesPerDay?: number;
}) {
  const ladderOptIn = useArcRelayLadderOptIn();
  const crestOptions = useArcRelayCrestOptions();
  const setCrest = useSetArcRelayCrest();
  const sheets = entrants.filter((entrant) => entrant.kind === 'sheet');
  return <>
    <section className="panel panel-feature pad">
      <p className="lab text-arena-material">Ranked runs automatically</p>
      <div className="mt-2 grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
        <p className="t-meta max-w-3xl">
          Field up to three sheets once. The Season then finds similarly rated opponents
          in the background and schedules {baselineMatchesPerDay == null
            ? 'a bounded number of ranked games per sheet'
            : `up to ${baselineMatchesPerDay} ranked games per sheet`} in a rolling 24 hours.
          There is no ranked queue button.
        </p>
        <Link to="/entrants/sheets/new" className="btn btn-primary min-h-11">Create a sheet</Link>
      </div>
      <p className="t-micro mt-3 border-t border-arena-edge pt-3">
        Want an immediate game? Open a sheet and use <b>Practice &amp; watch</b>.
        Practice is unranked and never changes Season standing.
      </p>
    </section>

    <section className="panel overflow-hidden">
      <div className="pad flex flex-wrap items-end gap-3 border-b border-arena-edge">
        <div className="mr-auto">
          <p className="lab">Your sheets</p>
          <p className="t-micro mt-1">Edit strategy, choose a crest, and decide which sheets represent you in Season 1.</p>
        </div>
        <span className="pill">{sheets.filter((entry) => entry.ladderOptedIn).length}/3 fielded</span>
      </div>
      <div className="grid gap-2 p-3 md:grid-cols-2 xl:grid-cols-3">
        {sheets.map((entrant) => <article key={entrant.id}
          className="rounded-sm border border-arena-edge bg-arena-deep/40 p-3">
          <div className="flex items-center gap-3">
            <EntrantCrest crest={entrant.crest} size={52} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="type-display truncate text-[18px]">{entrant.name}</h3>
              </div>
              <p className="t-micro">{entrant.rating} rating · {entrant.rankedMatches} ranked · r{entrant.revision}</p>
              <p className={clsx(
                't-micro mt-1',
                entrant.status === 'suspended' && 'text-arena-hot',
                entrant.status === 'legacy' && 'text-arena-material',
                entrant.status !== 'suspended' && entrant.status !== 'legacy' && 'text-arena-material',
              )}>
                {entrantStatusLabel(entrant.status)}{entrant.suspensionReason ? ` · ${entrant.suspensionReason}` : ''}
              </p>
            </div>
          </div>
          <CompositionStrip entrant={entrant} />
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" className="btn min-h-11" disabled={crestOptions.isPending}
              onClick={() => crestOptions.mutate(entrant.id)}>Choose crest</button>
            <Link to={`/entrants/sheets/${entrant.id}/edit`} className="btn min-h-11">Edit sheet</Link>
            {entrant.status === 'ready' &&
              <button type="button" className={clsx('btn min-h-11', entrant.ladderOptedIn ? 'btn-on' : 'btn-primary')}
                disabled={ladderOptIn.isPending}
                onClick={() => ladderOptIn.mutate({ entrantId: entrant.id, optedIn: !entrant.ladderOptedIn })}>
                {entrant.ladderOptedIn ? 'Withdraw from ranked' : 'Field for ranked'}
              </button>}
          </div>
          {entrant.status === 'legacy' && <p className="t-micro mt-3 border-t border-arena-edge pt-3 text-arena-material">
            Earlier-map layout — readable history, but not admissible to Season 1.
            Open it in the workshop and replot routes, zones and anchors on Tapline;
            coordinates are never converted between maps.
          </p>}
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
        {sheets.length === 0 && <p className="t-meta p-2">Create and save your first tactical sheet.</p>}
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

function entrantStatusLabel(status: string): string {
  switch (status) {
    case 'ready': return 'Ready';
    case 'suspended': return 'Pairing suspended';
    case 'legacy': return 'Legacy layout';
    default: return status;
  }
}
