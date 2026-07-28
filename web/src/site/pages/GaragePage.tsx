import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import {
  botLook,
  botLookOptions,
  projectileLook,
  projectileLookOptions,
} from '../../render/arenaThemes';
import BotIdentity from '../components/BotIdentity';
import FirstRun from '../components/FirstRun';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import { useCreateBot, useMyBots } from '../queries';
import { errorMessage } from '../errorMessage';
import {
  BOT_LOOK_KIND,
  cosmeticItem,
  PROJECTILE_LOOK_KIND,
  useCosmeticCatalog,
} from '../cosmetics';
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

const looks = botLookOptions();
const projectileLooks = projectileLookOptions();

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
  const [name, setName] = useState('');
  const [accent, setAccent] = useState('#22d3ee');
  const [lookId, setLookId] = useState('vanguard');
  const [projectileLookId, setProjectileLookId] = useState('pulse-bolt');
  const { catalog, error: catalogError } = useCosmeticCatalog();
  const navigate = useNavigate();
  const creation = useCreateBot();

  if (loading) return <LoadingState label="Loading your account…" />;
  if (!user) {
    navigate('/login');
    return null;
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
  const selectedLookOwned =
    cosmeticItem(catalog, BOT_LOOK_KIND, lookId)?.owned === true;
  const selectedProjectileOwned =
    cosmeticItem(catalog, PROJECTILE_LOOK_KIND, projectileLookId)?.owned ===
    true;
  const selectLook = (nextLookId: string) => {
    setLookId(nextLookId);
    const defaultProjectile = botLook(nextLookId).defaultProjectileLookId;
    if (
      defaultProjectile &&
      cosmeticItem(
        catalog,
        PROJECTILE_LOOK_KIND,
        defaultProjectile,
      )?.owned === true
    )
      setProjectileLookId(defaultProjectile);
  };

  return (
    <div className="flex flex-col gap-8">
      <section>
        <h2 className="lab mb-3">My bots</h2>
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {bots.map((bot) => {
            const look = botLook(bot.lookId);
            const projectile = projectileLook(bot.projectileLookId);
            return (
              <li key={bot.id}>
                <Link
                  to={`/bots/${bot.slug}`}
                  className="panel-quiet pad flex items-center gap-3 transition-colors hover:border-arena-edge2"
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
              className="t-body rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-arena-text outline-none focus:border-arena-accent"
            />
          </label>
          <label className="t-meta flex items-center gap-3">
            Accent color
            <input
              type="color"
              value={accent}
              onChange={(e) => setAccent(e.target.value)}
              className="h-8 w-14 cursor-pointer rounded-md border border-arena-edge bg-arena-bg"
            />
            <span className="val">{accent}</span>
          </label>
          <label className="t-meta flex flex-col gap-1">
            Chassis
            <select
              value={lookId}
              onChange={(event) => selectLook(event.target.value)}
              className="t-body rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-arena-text outline-none focus:border-arena-accent"
            >
              {looks.map((look) => (
                <option
                  key={look.id}
                  value={look.id}
                  disabled={
                    cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.owned !== true
                  }
                >
                  {look.label}
                  {cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.owned
                    ? ''
                    : ` — locked: ${
                        cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.unlock
                          ?.hint ?? 'Unlock required'
                      }`}
                </option>
              ))}
            </select>
          </label>
          <label className="t-meta flex flex-col gap-1">
            Projectile
            <select
              value={projectileLookId}
              onChange={(event) => setProjectileLookId(event.target.value)}
              className="t-body rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-arena-text outline-none focus:border-arena-accent"
            >
              {projectileLooks.map((look) => (
                <option
                  key={look.id}
                  value={look.id}
                  disabled={
                    cosmeticItem(
                      catalog,
                      PROJECTILE_LOOK_KIND,
                      look.id,
                    )?.owned !== true
                  }
                >
                  {look.label}
                  {cosmeticItem(catalog, PROJECTILE_LOOK_KIND, look.id)?.owned
                    ? ''
                    : ` — locked: ${
                        cosmeticItem(
                          catalog,
                          PROJECTILE_LOOK_KIND,
                          look.id,
                        )?.unlock?.hint ?? 'Unlock required'
                      }`}
                </option>
              ))}
            </select>
          </label>
          <div className="t-meta flex items-center gap-3 rounded-[3px] border border-arena-edge bg-arena-bg/60 px-3 py-2">
            <ProjectilePreview
              look={projectileLook(projectileLookId)}
              accent={accent}
              className="h-7 w-16"
            />
            {projectileLook(projectileLookId).label}
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
              !selectedLookOwned ||
              !selectedProjectileOwned
            }
            className="mt-1 self-start rounded-md bg-arena-accent px-4 py-2 text-sm font-semibold text-arena-bg disabled:cursor-not-allowed disabled:opacity-40"
          >
            {creation.isPending ? 'Creating…' : 'Create bot'}
          </button>
        </form>
      </section>
    </div>
  );
}
