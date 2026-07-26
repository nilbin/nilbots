import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { ApiError } from './api';
import { AuthProvider } from './auth';
import Shell from './Shell';
import ArenaPage from './pages/ArenaPage';
import AuthPage from './pages/AuthPage';
import BotsPage from './pages/BotsPage';
import BotDetailPage from './pages/BotDetailPage';
import GaragePage from './pages/GaragePage';
import MatchPage from './pages/MatchPage';
import MatchSetPage from './pages/MatchSetPage';
import LeaderboardPage from './pages/LeaderboardPage';
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
            <Route index element={<ArenaPage />} />
            <Route path="/login" element={<AuthPage />} />
            <Route path="/bots" element={<BotsPage />} />
            <Route path="/bots/:botKey" element={<BotDetailPage />} />
            <Route path="/garage" element={<GaragePage />} />
            <Route path="/matches/:matchId" element={<MatchPage />} />
            <Route path="/sets/:setId" element={<MatchSetPage />} />
            <Route path="/leaderboard" element={<LeaderboardPage />} />
            <Route path="/docs" element={<DocsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
    </QueryClientProvider>
  );
}
