import { useId, useMemo } from 'react';
import type { BotDetail } from '../api';
import { errorMessage } from '../errorMessage';
import { eligibleLabsPlaylists } from '../labs';
import { useLabsCatalog } from '../queries';
import ArenaAction from './ArenaAction';

/**
 * Bot-page Labs discovery.
 *
 * Match setup belongs to the shared Play composer so this panel cannot drift from its
 * allowance, mutation, focus or error behavior. The panel answers only whether this
 * active generation has an experiment worth opening.
 */
export default function LabsPanel({ bot }: { bot: BotDetail }) {
  const catalog = useLabsCatalog(bot.isOwner);
  const playlists = useMemo(
    () =>
      catalog.data ? eligibleLabsPlaylists(bot, catalog.data) : [],
    [bot, catalog.data],
  );
  const headingId = useId();

  if (!bot.isOwner) return null;

  if (catalog.isPending) {
    return (
      <LabsState
        headingId={headingId}
        title="Checking experiments…"
        detail="Finding hosted game modes this active generation can run."
        status
      />
    );
  }

  if (catalog.error) {
    return (
      <LabsState
        headingId={headingId}
        title="Experiments unavailable"
        detail={errorMessage(
          catalog.error,
          'The Labs catalog could not be loaded.',
        )}
        error
        action={
          <button
            type="button"
            onClick={() => void catalog.refetch()}
            className="btn"
          >
            Try again
          </button>
        }
      />
    );
  }

  if (!catalog.data?.enabled) {
    return (
      <LabsState
        headingId={headingId}
        title="No experiments are running"
        detail="Labs will appear here when a hosted experimental game mode is available."
      />
    );
  }

  if (playlists.length === 0) {
    return (
      <LabsState
        headingId={headingId}
        title="No compatible experiment"
        detail="This active generation does not support any of the experiments running right now."
      />
    );
  }

  return (
    <section
      id="labs"
      aria-labelledby={headingId}
      className="panel pad flex flex-col gap-3"
    >
      <header>
        <p className="lab mb-1">Labs experiments · unranked</p>
        <h2 id={headingId} className="t-body font-semibold text-arena-text">
          {playlists.length === 1
            ? playlists[0].displayName
            : `${playlists.length} experiments available`}
        </h2>
        <p className="t-meta mt-1">
          Experimental two-bot matches do not move either bot&apos;s rating.
        </p>
      </header>

      {playlists.length > 1 && (
        <ul className="t-meta flex flex-wrap gap-x-3 gap-y-1">
          {playlists.map((playlist) => (
            <li key={playlist.playlistVersionId}>{playlist.displayName}</li>
          ))}
        </ul>
      )}

      <ArenaAction
        bot={{
          id: bot.id,
          slug: bot.slug,
          name: bot.name,
          accent: bot.accent,
          lookId: bot.lookId,
          isOwner: true,
        }}
        modes={['labs']}
        initialMode="labs"
        triggerLabel={
          playlists.length === 1 ? 'Run lab match' : 'Choose experiment'
        }
        className="self-start"
      />
    </section>
  );
}

function LabsState({
  headingId,
  title,
  detail,
  status = false,
  error = false,
  action,
}: {
  headingId: string;
  title: string;
  detail: string;
  status?: boolean;
  error?: boolean;
  action?: React.ReactNode;
}) {
  return (
    <section
      id="labs"
      aria-labelledby={headingId}
      className="panel pad flex flex-col gap-3"
    >
      <header>
        <p className="lab mb-1">Labs experiments · unranked</p>
        <h2 id={headingId} className="t-body font-semibold text-arena-text">
          {title}
        </h2>
      </header>
      <p
        className={`t-meta${error ? ' text-arena-hot' : ''}`}
        role={error ? 'alert' : status ? 'status' : undefined}
      >
        {detail}
      </p>
      {action}
    </section>
  );
}
