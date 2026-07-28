import { useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import AppearanceFields, {
  appearanceSelectionOwned,
} from '../components/AppearanceFields';
import ArenaAction from '../components/ArenaAction';
import BotIdentity from '../components/BotIdentity';
import FirstRun from '../components/FirstRun';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import { rosterBotSupportsLegacyDuel } from '../botContractProfiles';
import { useBots, useCreateBot, useMyBots } from '../queries';
import { errorMessage } from '../errorMessage';
import { useCosmeticCatalog } from '../cosmetics';
import CosmeticUnlocks from '../components/CosmeticUnlocks';

/// The player dashboard: my bots + create a new one.
function CliAccess() {
  return (
    <section className="panel pad max-w-xl">
      <h2 className="lab mb-2">Cli access</h2>
      <p className="t-meta">
        Develop locally and submit from your terminal:{' '}
        <code className="val">nilbots register</code> opens this site in your browser to
        create an account and sign you in securely (OAuth + PKCE), then{' '}
        <code className="val">nilbots submit</code> creates your bot and uploads it for the official
        server build and reports whether your local artifact matches it bit-for-bit.
      </p>
    </section>
  );
}

export default function GaragePage() {
  const { user, loading } = useAuth();
  // Creating a bot navigates to it, so this list never needs a manual refresh — but an
  // account with none is polled by the hook, because the first bot usually arrives from a
  // terminal rather than from this page.
  const {
    data: bots = null,
    error: botsError,
    refetch: refetchBots,
  } = useMyBots(Boolean(user));
  const {
    data: roster = null,
    error: rosterError,
    refetch: refetchRoster,
  } = useBots(Boolean(user));
  const [name, setName] = useState('');
  const [accent, setAccent] = useState('#22d3ee');
  const [lookId, setLookId] = useState('vanguard');
  const [projectileLookId, setProjectileLookId] = useState('pulse-bolt');
  const { catalog, error: catalogError } = useCosmeticCatalog();
  const navigate = useNavigate();
  const creation = useCreateBot();

  if (loading) return <LoadingState label="Loading your account…" />;
  if (!user) {
    return (
      <Navigate
        to={`/login?returnUrl=${encodeURIComponent('/garage')}`}
        replace
      />
    );
  }
  if (botsError)
    return <ErrorState error={botsError} onRetry={() => void refetchBots()} />;
  if (bots === null) return <LoadingState label="Loading your bots…" />;
  // An empty garage is not a garage with a notice in it: for an account that has never
  // shipped a bot, the page's entire job is the hand-off to the CLI, so the appearance
  // catalog and the create form — both of which only make sense once you have something
  // to dress — stay off until there is one bot.
  if (bots.length === 0) return <FirstRun />;

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    const bot = await creation.mutateAsync({ name, accent, lookId, projectileLookId });
    navigate(`/bots/${bot.id}`);
  };
  const selectionOwned = appearanceSelectionOwned(
    catalog,
    lookId,
    projectileLookId,
  );
  const duelReadyIds = new Set(
    (roster ?? [])
      .filter(rosterBotSupportsLegacyDuel)
      .map((bot) => bot.id),
  );

  return (
    <div className="flex flex-col gap-8">
      <h1 className="type-display text-[30px]">Garage</h1>
      <section>
        <h2 className="lab mb-3">My bots</h2>
        {rosterError && (
          <div
            className="panel-quiet pad mb-3 flex flex-wrap items-center gap-2"
            role="alert"
          >
            <p className="t-meta min-w-0 grow text-arena-hot">
              Arena availability could not be loaded. Your bots are still available
              below.
            </p>
            <button
              type="button"
              onClick={() => void refetchRoster()}
              className="btn"
            >
              Try again
            </button>
          </div>
        )}
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {bots.map((bot) => {
            const look = botLook(bot.lookId);
            const projectile = projectileLook(bot.projectileLookId);
            const ready =
              bot.latestVersion?.isActive === true &&
              bot.latestVersion.status === 'Built' &&
              duelReadyIds.has(bot.id);
            return (
              <li
                key={bot.id}
                className="panel-quiet pad flex flex-wrap items-center gap-3"
              >
                <Link
                  to={`/bots/${bot.slug}`}
                  className="flex min-w-0 grow items-center gap-3 transition-opacity hover:opacity-80"
                >
                  <BotIdentity
                    name={bot.name}
                    accent={bot.accent}
                    lookId={bot.lookId}
                    size="md"
                    emphasized
                  />
                  <ProjectilePreview
                    look={projectile}
                    accent={bot.accent}
                    className="h-6 w-10"
                  />
                  {/* Chassis and projectile are names a player chose, not values a
                      machine wrote, so they are sans — mono is reserved. */}
                  <span className="t-micro">
                    {look.label} · {projectile.label}
                  </span>
                  <span className="val ml-auto">
                    {bot.latestVersion
                      ? `v${bot.latestVersion.versionNumber} ${bot.latestVersion.status.toLowerCase()}`
                      : 'no versions'}
                  </span>
                </Link>
                {ready && (
                  <ArenaAction
                    bot={{
                      id: bot.id,
                      slug: bot.slug,
                      name: bot.name,
                      accent: bot.accent,
                      lookId: bot.lookId,
                      isOwner: true,
                      ready,
                    }}
                    triggerLabel="Play"
                    className="shrink-0"
                  />
                )}
              </li>
            );
          })}
        </ul>
      </section>

      <CosmeticUnlocks catalog={catalog} accent={accent} error={catalogError} />

      <CliAccess />

      <section className="panel pad max-w-md">
        <h2 className="lab mb-3">New bot</h2>
        <form onSubmit={create} className="flex flex-col gap-3">
          <label className="t-meta flex flex-col gap-1">
            Name
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              minLength={2}
              maxLength={40}
              placeholder="Murder Roomba"
              className="field"
            />
          </label>
          <AppearanceFields
            catalog={catalog}
            accent={accent}
            lookId={lookId}
            projectileLookId={projectileLookId}
            accentLabel="Accent color"
            onAccentChange={setAccent}
            onLookChange={setLookId}
            onProjectileLookChange={setProjectileLookId}
          />
          <div className="panel-quiet pad flex flex-col gap-2">
            <p className="lab">Preview</p>
            <div className="flex flex-wrap items-center gap-3">
              <BotIdentity
                name={name.trim() || 'New bot'}
                accent={accent}
                lookId={lookId}
                size="sm"
                emphasized
              />
              <span className="t-meta flex items-center gap-2">
                <ProjectilePreview
                  look={projectileLook(projectileLookId)}
                  accent={accent}
                  className="h-7 w-16"
                />
                {projectileLook(projectileLookId).label}
              </span>
            </div>
          </div>
          {creation.isError && (
            <p className="t-body text-arena-hot">
              {errorMessage(creation.error, 'Failed to create bot.')}
            </p>
          )}
          {catalogError && <p className="t-body text-arena-hot">{catalogError}</p>}
          <button
            type="submit"
            disabled={
              creation.isPending ||
              !catalog ||
              !selectionOwned
            }
            className="btn btn-on mt-1 self-start disabled:opacity-40"
          >
            {creation.isPending ? 'Creating…' : 'Create bot'}
          </button>
        </form>
      </section>
    </div>
  );
}
