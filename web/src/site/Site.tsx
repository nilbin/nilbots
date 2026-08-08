import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import {
  BrowserRouter,
  Link,
  Navigate,
  Route,
  Routes,
  useParams,
} from 'react-router-dom';
import { ApiError } from './api';
import { AuthProvider } from './auth';
import Shell from './Shell';
import ArenaPage from './pages/ArenaPage';
import AuthPage from './pages/AuthPage';
import BotsPage from './pages/BotsPage';
import BotDetailPage from './pages/BotDetailPage';
import MatchPage from './pages/MatchPage';
import MatchSetPage from './pages/MatchSetPage';
import DocsPage from './pages/DocsPage';
import ArcRelayPage from './pages/ArcRelayPage';
import SeasonPage from './pages/SeasonPage';

export default function Site() {
  // Per-mount rather than module level: a module-level client survives hot reloads and
  // carries stale query state across them.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // The arena feed and ladder move on the server's schedule, not the reader's,
            // so a brief cache avoids refetching everything on every navigation.
            staleTime: 30_000,
            // A 4xx is an answer, not a hiccup — retrying a 404 for a mistyped id just
            // delays telling the reader it does not exist. Everything else is transient:
            // a restarting server, a dropped connection.
            retry: (count, error) =>
              !(error instanceof ApiError && error.status >= 400 && error.status < 500) &&
              count < 1,
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route element={<Shell />}>
              <Route
                index
                element={
                  <TitledPage title="Season 1">
                    <SeasonPage />
                  </TitledPage>
                }
              />
              <Route
                path="/watch"
                element={
                  <TitledPage title="Watch">
                    <ArenaPage />
                  </TitledPage>
                }
              />
              <Route
                path="/login"
                element={
                  <TitledPage title="Sign in">
                    <AuthPage />
                  </TitledPage>
                }
              />
              <Route
                path="/archive/bots"
                element={
                  <TitledPage title="Legacy archive">
                    <BotsPage />
                  </TitledPage>
                }
              />
              <Route
                path="/archive/bots/:botKey/appearance"
                element={<LegacyBotPlayRedirect />}
              />
              <Route
                path="/archive/bots/:botKey/play"
                element={<LegacyBotPlayRedirect />}
              />
              <Route
                path="/archive/bots/:botKey"
                element={
                  <TitledPage title="Legacy record">
                    <BotDetailPage />
                  </TitledPage>
                }
              />
              <Route
                path="/entrants"
                element={
                  <TitledPage title="Entrants">
                    <ArcRelayPage />
                  </TitledPage>
                }
              />
              <Route
                path="/entrants/sheets/new"
                element={
                  <TitledPage title="Sheet workshop">
                    <ArcRelayPage view="sheets" />
                  </TitledPage>
                }
              />
              <Route
                path="/entrants/sheets/:sheetId/edit"
                element={
                  <TitledPage title="Sheet workshop">
                    <SheetEditorRoute />
                  </TitledPage>
                }
              />
              <Route
                path="/matches/:matchId"
                element={
                  <TitledPage title="Match">
                    <MatchPage />
                  </TitledPage>
                }
              />
              <Route
                path="/archive/sets/:setId"
                element={
                  <TitledPage title="Legacy set">
                    <MatchSetPage />
                  </TitledPage>
                }
              />
              {/* The ladder used to live here and be called the leaderboard. Old links
                  and bookmarks still resolve rather than 404. */}
              <Route path="/archive/rankings" element={<Navigate to="/archive/bots" replace />} />
              <Route path="/relay" element={<Navigate to="/" replace />} />
              <Route path="/relay/sheets" element={<Navigate to="/entrants/sheets/new" replace />} />
              <Route path="/rankings" element={<Navigate to="/" replace />} />
              <Route path="/leaderboard" element={<Navigate to="/" replace />} />
              <Route path="/bots" element={<Navigate to="/archive/bots" replace />} />
              <Route path="/bots/:botKey/*" element={<LegacyBotPlayRedirect />} />
              <Route path="/sets/:setId" element={<LegacySetRedirect />} />
              <Route path="/garage" element={<Navigate to="/entrants" replace />} />
              <Route path="/looks" element={<Navigate to="/archive/bots" replace />} />
              <Route path="/store" element={<Navigate to="/entrants" replace />} />
              <Route
                path="/docs"
                element={
                  <TitledPage title="Docs">
                    <DocsPage />
                  </TitledPage>
                }
              />
              <Route
                path="*"
                element={
                  <TitledPage title="Not found">
                    <NotFoundPage />
                  </TitledPage>
                }
              />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}

/** The short-lived review route now returns to the bot instead of becoming a dead link. */
function LegacyBotPlayRedirect() {
  const { botKey } = useParams<{ botKey: string }>();
  return <Navigate to={`/archive/bots/${botKey ?? ''}`} replace />;
}

function LegacySetRedirect() {
  const { setId } = useParams<{ setId: string }>();
  return <Navigate to={`/archive/sets/${setId ?? ''}`} replace />;
}

function SheetEditorRoute() {
  const { sheetId } = useParams<{ sheetId: string }>();
  return <ArcRelayPage view="sheets" sheetId={sheetId ?? null} />;
}

function TitledPage({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  useEffect(() => {
    const nextTitle = `${title} · nilbots`;
    document.title = nextTitle;
    return () => {
      if (document.title === nextTitle) document.title = 'nilbots';
    };
  }, [title]);
  return <>{children}</>;
}

function NotFoundPage() {
  return (
    <section className="panel pad mx-auto max-w-xl text-center">
      <p className="lab mb-2">404</p>
      <h1 className="type-display text-[26px]">That route is outside the arena</h1>
      <p className="t-meta mt-2">
        The page may have moved, or the address may be incomplete.
      </p>
      <Link to="/" className="btn btn-on mt-4 inline-flex">
        Return to Season
      </Link>
    </section>
  );
}
