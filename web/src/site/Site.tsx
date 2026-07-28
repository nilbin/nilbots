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
import ShopPage from './pages/ShopPage';
import BotsPage from './pages/BotsPage';
import BotAppearancePage from './pages/BotAppearancePage';
import BotDetailPage from './pages/BotDetailPage';
import GaragePage from './pages/GaragePage';
import MatchPage from './pages/MatchPage';
import MatchSetPage from './pages/MatchSetPage';
import SeasonPage from './pages/SeasonPage';
import DocsPage from './pages/DocsPage';

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
                  <TitledPage title="Season">
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
                path="/bots"
                element={
                  <TitledPage title="Bots">
                    <BotsPage />
                  </TitledPage>
                }
              />
              <Route
                path="/bots/:botKey/appearance"
                element={
                  <TitledPage title="Bot appearance">
                    <BotAppearancePage />
                  </TitledPage>
                }
              />
              <Route
                path="/bots/:botKey/play"
                element={<LegacyBotPlayRedirect />}
              />
              <Route
                path="/bots/:botKey"
                element={
                  <TitledPage title="Bot">
                    <BotDetailPage />
                  </TitledPage>
                }
              />
              <Route
                path="/garage"
                element={
                  <TitledPage title="Garage">
                    <GaragePage />
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
                path="/sets/:setId"
                element={
                  <TitledPage title="Ranked set">
                    <MatchSetPage />
                  </TitledPage>
                }
              />
              {/* The ladder used to live here and be called the leaderboard. Old links
                  and bookmarks still resolve rather than 404. */}
              <Route path="/leaderboard" element={<Navigate to="/" replace />} />
              <Route path="/looks" element={<Navigate to="/store" replace />} />
              <Route
                path="/store"
                element={
                  <TitledPage title="Shop">
                    <ShopPage />
                  </TitledPage>
                }
              />
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
  return <Navigate to={`/bots/${botKey ?? ''}`} replace />;
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
