import { Link, useLocation, useParams } from 'react-router-dom';
import AppearanceEditor from '../components/AppearanceEditor';
import BotIdentity from '../components/BotIdentity';
import { ErrorState, LoadingState } from '../components/StateView';
import { ApiError } from '../api';
import { useAuth } from '../auth';
import { useBot } from '../queries';
import { internalReturnTarget } from '../returnTarget';

export default function BotAppearancePage() {
  const { botKey } = useParams<{ botKey: string }>();
  const location = useLocation();
  const { data: bot, error, refetch } = useBot(botKey);
  const { user, loading: authLoading } = useAuth();
  const missing = error instanceof ApiError && error.status === 404;

  if (missing) {
    return (
      <section className="panel pad mx-auto max-w-xl">
        <h1 className="type-display text-[24px]">No bot called “{botKey}”</h1>
        <p className="t-meta mt-2">
          It may have been renamed or never existed.{' '}
          <Link to="/archive/bots" className="text-link">
            Browse every bot
          </Link>
          .
        </p>
      </section>
    );
  }
  if (error) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!bot) return <LoadingState label="Loading the bot's wardrobe…" />;
  if (authLoading) return <LoadingState label="Checking who owns this look…" />;

  if (!bot.isOwner) {
    const returnUrl = `/archive/bots/${bot.slug}/appearance`;
    return (
      <section className="panel pad mx-auto max-w-xl">
        <h1 className="type-display text-[24px]">
          {user ? 'That look belongs to its owner' : 'Sign in to edit appearance'}
        </h1>
        <p className="t-meta mt-2">
          {user
            ? `You can inspect ${bot.name}, but only its owner can change what it wears.`
            : `If ${bot.name} is yours, sign in and you will return to its appearance picker.`}
        </p>
        <span className="mt-4 flex flex-wrap gap-2">
          {!user && (
            <Link
              to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}
              className="btn btn-strong"
            >
              Sign in and return
            </Link>
          )}
          <Link to={`/archive/bots/${bot.slug}`} className="btn">
            Return to {bot.name}
          </Link>
        </span>
      </section>
    );
  }
  const botPath = `/archive/bots/${bot.slug}`;
  const returnTarget = internalReturnTarget(location.state, {
    to: botPath,
    label: bot.name,
  });

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="lab mb-2">Appearance</p>
          <h1>
            <BotIdentity
              name={bot.name}
              accent={bot.accent}
              lookId={bot.lookId}
              size="lg"
              emphasized
              nameClassName="type-display"
            />
          </h1>
          <p className="t-meta mt-2 max-w-[62ch]">
            Preview every chassis and projectile. Locked looks stay visible with
            the way to earn or buy them.
          </p>
        </div>
        <span className="flex flex-wrap gap-2">
          <Link
            to={returnTarget.to}
            className="btn inline-flex min-h-11 items-center"
          >
            ← {returnTarget.label}
          </Link>
          {returnTarget.to !== botPath && (
            <Link
              to={botPath}
              className="btn inline-flex min-h-11 items-center"
            >
              Bot page
            </Link>
          )}
          {returnTarget.to !== '/garage' && (
            <Link
              to="/garage"
              className="btn inline-flex min-h-11 items-center"
            >
              Garage
            </Link>
          )}
        </span>
      </header>

      <AppearanceEditor
        bot={bot}
        botKey={botKey!}
        entitlementRevision={
          bot.versions.filter((version) => version.status === 'Built').length
        }
      />
    </div>
  );
}
