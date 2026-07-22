import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import { useAuth } from './auth';
import Logo from '../components/Logo';

export default function Shell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <div className="mx-auto flex min-h-screen max-w-6xl flex-col px-4 md:px-6">
      <header className="flex flex-wrap items-center gap-x-6 gap-y-2 border-b border-arena-edge py-4">
        <Link to="/" className="text-xl">
          <Logo size={26} />
        </Link>
        <nav className="flex items-center gap-1 text-sm">
          <TopLink to="/">Arena</TopLink>
          <TopLink to="/bots">Bots</TopLink>
          <TopLink to="/leaderboard">Leaderboard</TopLink>
          <TopLink to="/docs">Docs</TopLink>
          {user && <TopLink to="/garage">My garage</TopLink>}
        </nav>
        <div className="ml-auto flex items-center gap-3 text-sm">
          {user ? (
            <>
              <span className="text-arena-dim">{user.displayName}</span>
              <button
                onClick={() => void logout().then(() => navigate('/'))}
                className="rounded-md border border-arena-edge px-3 py-1 text-arena-dim transition-colors hover:text-arena-text"
              >
                Sign out
              </button>
            </>
          ) : (
            <Link
              to="/login"
              className="rounded-md border border-arena-accent px-3 py-1 font-medium text-arena-accent transition-colors hover:bg-arena-accent/15"
            >
              Sign in
            </Link>
          )}
        </div>
      </header>
      <main className="flex-1 py-6">
        <Outlet />
      </main>
      <footer className="border-t border-arena-edge py-3 font-mono text-[11px] text-arena-dim">
        deterministic robot combat · every match is reproducible
      </footer>
    </div>
  );
}

function TopLink({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        clsx(
          'rounded-md px-3 py-1.5 transition-colors',
          isActive ? 'bg-arena-panel text-arena-text' : 'text-arena-dim hover:text-arena-text',
        )
      }
    >
      {children}
    </NavLink>
  );
}
