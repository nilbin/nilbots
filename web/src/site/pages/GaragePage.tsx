import { useEffect, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import {
  botLook,
  botLookOptions,
  projectileLook,
  projectileLookOptions,
} from '../../render/arenaThemes';
import AppearanceFields, {
  appearanceSelectionOwned,
} from '../components/AppearanceFields';
import ArenaAction from '../components/ArenaAction';
import BotIdentity from '../components/BotIdentity';
import FirstRun from '../components/FirstRun';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import {
  useCreateBot,
  useMyBots,
} from '../queries';
import { errorMessage } from '../errorMessage';
import {
  BOT_LOOK_KIND,
  cosmeticItem,
  PROJECTILE_LOOK_KIND,
  useCosmeticCatalog,
} from '../cosmetics';
import CosmeticUnlocks from '../components/CosmeticUnlocks';
import type { MyBot } from '../api';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

/// The player dashboard: my bots + create a new one.
function CliAccess() {
  return (
    <details className="panel-quiet max-w-2xl">
      <summary className="flex cursor-pointer list-none items-center gap-3 px-4 py-3">
        <span className="lab">CLI access</span>
        <span className="t-meta ml-auto">Develop and submit locally</span>
        <span aria-hidden className="text-arena-material">
          +
        </span>
      </summary>
      <p className="t-meta border-t border-arena-edge px-4 py-3">
        <code className="val">nilbots register</code> opens this site to create an
        account and sign you in securely. Then{' '}
        <code className="val">nilbots submit</code> uploads an official server build
        and verifies that it matches your local artifact bit-for-bit.
      </p>
    </details>
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
  const [name, setName] = useState('');
  const [accent, setAccent] = useState('#22d3ee');
  const [lookId, setLookId] = useState('');
  const [projectileLookId, setProjectileLookId] = useState('');
  const { catalog, error: catalogError } = useCosmeticCatalog();
  const navigate = useNavigate();
  const creation = useCreateBot();

  useEffect(() => {
    if (catalog === null) return;
    if (cosmeticItem(catalog, BOT_LOOK_KIND, lookId)?.owned !== true) {
      setLookId(
        botLookOptions().find(
          (look) => cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.owned,
        )?.id ?? '',
      );
    }
    if (
      cosmeticItem(catalog, PROJECTILE_LOOK_KIND, projectileLookId)?.owned !==
      true
    ) {
      setProjectileLookId(
        projectileLookOptions().find(
          (look) =>
            cosmeticItem(catalog, PROJECTILE_LOOK_KIND, look.id)?.owned,
        )?.id ?? '',
      );
    }
  }, [catalog, lookId, projectileLookId]);

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
  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-7">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="lab mb-2">Your workbench</p>
          <h1 className="type-display text-[30px]">Garage</h1>
          <p className="t-body mt-2 max-w-[58ch] text-arena-dim">
            Build, equip, and deploy your bots. The public directory is for
            comparison; this is where your fleet gets ready.
          </p>
        </div>
        <span className="flex flex-wrap gap-2">
          <Link
            to="/bots"
            className="btn inline-flex min-h-11 items-center"
          >
            Browse all bots
          </Link>
          <Link
            to="/store"
            className="btn inline-flex min-h-11 items-center"
          >
            Shop
          </Link>
        </span>
      </header>

      <section>
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="lab">Fleet bays</h2>
          <p className="t-micro">Play, inspect, or change a loadout directly.</p>
        </div>
        <ul className="grid grid-cols-1 gap-3 md:grid-cols-2">
          {bots.map((bot) => (
            <OwnedBotCard key={bot.id} bot={bot} />
          ))}
        </ul>
      </section>

      <details className="panel">
        <summary className="choice-card flex min-h-12 cursor-pointer list-none items-center gap-3 rounded-none border-0 px-4 py-3">
          <span>
            <span className="t-body block font-semibold text-arena-text">
              Unlock progress
            </span>
            <span className="t-micro block">
              Track what&apos;s close; browse every locked look from a bot&apos;s
              appearance picker.
            </span>
          </span>
          <span
            aria-hidden
            className="type-display ml-auto text-[24px] text-arena-material"
          >
            +
          </span>
        </summary>
        <div className="border-t border-arena-edge p-4">
          <CosmeticUnlocks
            catalog={catalog}
            accent={accent}
            error={catalogError}
          />
        </div>
      </details>

      <details className="panel max-w-2xl">
        <summary className="choice-card flex cursor-pointer list-none items-center gap-3 rounded-none border-0 px-4 py-3">
          <span>
            <span className="t-body block font-semibold text-arena-text">
              Create another bot
            </span>
            <span className="t-meta block">
              Name it and choose its first chassis and projectile.
            </span>
          </span>
          <span
            aria-hidden
            className="type-display ml-auto text-[24px] text-arena-material"
          >
            +
          </span>
        </summary>
        <form
          onSubmit={create}
          className="flex max-w-md flex-col gap-3 border-t border-arena-edge px-4 py-4"
        >
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
            className="btn btn-strong mt-1 min-h-11 self-start disabled:opacity-40"
          >
            {creation.isPending ? 'Creating…' : 'Create bot'}
          </button>
        </form>
      </details>

      <CliAccess />
    </div>
  );
}

function OwnedBotCard({ bot }: { bot: MyBot }) {
  const look = botLook(bot.lookId);
  const projectile = projectileLook(bot.projectileLookId);
  const cardAccent = playerAccent(bot.accent, 'panel');

  return (
    <li
      className="bot-workbench panel flex min-w-0 flex-col gap-4 p-4"
      style={styleVariables({ '--player-accent': cardAccent })}
    >
      <div className="flex min-w-0 items-start gap-3">
        <Link
          to={`/bots/${bot.slug}`}
          state={{ returnTo: '/garage', returnLabel: 'Garage' }}
          className="min-w-0 grow transition-opacity hover:opacity-80"
        >
          <BotIdentity
            name={bot.name}
            accent={bot.accent}
            lookId={bot.lookId}
            size="lg"
            emphasized
          />
        </Link>
        <span className="pill shrink-0">{generationStatus(bot)}</span>
      </div>

      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-3 border-y border-arena-edge py-3">
        <span className="min-w-0">
          <span className="lab block">Loadout</span>
          <span className="t-meta mt-1 block truncate">
            {look.label} · {projectile.label}
          </span>
        </span>
        <ProjectilePreview
          look={projectile}
          accent={bot.accent}
          className="h-9 w-20"
        />
      </div>

      <div className="mt-auto grid grid-cols-2 gap-2">
        <ArenaAction
          bot={{
            id: bot.id,
            slug: bot.slug,
            name: bot.name,
            accent: bot.accent,
            lookId: bot.lookId,
            isOwner: true,
          }}
          triggerLabel="Play"
          challengeContextRole="entrant"
          className="arena-card-play col-span-2"
        />
        <Link
          to={`/bots/${bot.slug}`}
          state={{ returnTo: '/garage', returnLabel: 'Garage' }}
          className="btn inline-flex min-h-11 items-center justify-center text-center"
        >
          Open bot
        </Link>
        <Link
          to={`/bots/${bot.slug}/appearance`}
          state={{ returnTo: '/garage', returnLabel: 'Garage' }}
          className="btn inline-flex min-h-11 items-center justify-center text-center"
          aria-label={`Change ${bot.name}'s appearance`}
        >
          Appearance
        </Link>
      </div>
    </li>
  );
}

function generationStatus(bot: MyBot): string {
  const latest = bot.latestVersion;
  if (latest === null) return 'No generation';
  if (latest.isActive) return `Active · gen ${latest.versionNumber}`;
  return `${latest.status} · gen ${latest.versionNumber}`;
}
