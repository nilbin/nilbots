import { BrowserRouter, Route, Routes } from 'react-router-dom';
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
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<Shell />}>
            <Route index element={<ArenaPage />} />
            <Route path="/login" element={<AuthPage />} />
            <Route path="/bots" element={<BotsPage />} />
            <Route path="/bots/:botId" element={<BotDetailPage />} />
            <Route path="/garage" element={<GaragePage />} />
            <Route path="/matches/:matchId" element={<MatchPage />} />
            <Route path="/sets/:setId" element={<MatchSetPage />} />
            <Route path="/leaderboard" element={<LeaderboardPage />} />
            <Route path="/docs" element={<DocsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
