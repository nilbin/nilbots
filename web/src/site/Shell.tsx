import { useEffect } from 'react';
import {
  Link,
  NavLink,
  Outlet,
  useLocation,
  useNavigate,
} from 'react-router-dom';
import clsx from 'clsx';
import { useAuth } from './auth';
import Logo from '../components/Logo';
import {
  ArenaActionProvider,
  GlobalArenaAction,
} from './components/ArenaAction';
import NotificationCenter from './components/NotificationCenter';

export default function Shell() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <ArenaActionProvider>
      <div className="mx-auto flex min-h-screen w-full min-w-0 max-w-6xl flex-col px-4 pb-[calc(3.5rem+env(safe-area-inset-bottom))] min-[640px]:pb-0 md:px-6">
        <ScrollToHash />
        <a
          href="#main-content"
          className="btn sr-only z-50 focus:fixed focus:top-2 focus:left-2 focus:not-sr-only"
        >
          Skip to content
        </a>
        <NotificationCenter />
        <header className="flex flex-nowrap items-center gap-3.5 border-b border-arena-edge bg-arena-panel px-3.5 py-2.5 max-[359px]:gap-2 max-[359px]:px-2.5">
          <Link to="/" className="shrink-0 text-arena-material">
            <Logo size={17} />
          </Link>
          <nav
            className="ml-auto hidden flex-nowrap items-center gap-0.5 min-[640px]:flex"
            aria-label="Primary navigation"
          >
            <TopLink to="/">Rankings</TopLink>
            <TopLink to="/bots">Bots</TopLink>
            <TopLink to="/relay">Relay</TopLink>
            <TopLink to="/watch">Watch</TopLink>
            <TopLink to="/docs">Docs</TopLink>
          </nav>
          <div className="ml-auto flex min-w-0 items-center gap-2 max-[359px]:gap-1">
            <GlobalArenaAction />
            {user ? (
              <>
                <Link
                  to="/garage"
                  aria-label={`${user.displayName}'s Garage`}
                  className="t-meta inline-flex min-h-11 min-w-0 items-center truncate transition-colors hover:text-arena-text max-[359px]:shrink-0"
                >
                  <span className="sm:hidden">Garage</span>
                  <span className="hidden sm:inline">{user.displayName}</span>
                </Link>
                <button
                  type="button"
                  onClick={() => void logout().then(() => navigate('/'))}
                  className="btn min-h-11 shrink-0 text-arena-dim hover:text-arena-text max-[359px]:px-2"
                >
                  Sign out
                </button>
              </>
            ) : (
              <Link
                to="/login"
                className="btn shrink-0"
              >
                Sign in
              </Link>
            )}
          </div>
        </header>
        <main id="main-content" tabIndex={-1} className="min-w-0 flex-1 py-6">
          <Outlet />
        </main>
        <footer className="t-micro flex flex-wrap items-center gap-x-4 gap-y-2 border-t border-arena-edge py-3">
          <span>deterministic robot combat · every match is reproducible</span>
          <nav
            className="ml-auto flex items-center gap-3"
            aria-label="Secondary navigation"
          >
            <Link to="/store" className="text-link">
              Shop
            </Link>
            {user && (
              <Link to="/garage" className="text-link">
                Garage
              </Link>
            )}
          </nav>
        </footer>
        <nav
          className="fixed inset-x-0 bottom-0 z-40 grid grid-cols-4 border-t border-arena-edge bg-arena-panel pb-[env(safe-area-inset-bottom)] min-[640px]:hidden"
          aria-label="Primary navigation"
        >
          <MobileLink to="/">Rankings</MobileLink>
          <MobileLink to="/bots">Bots</MobileLink>
          <MobileLink to="/watch">Watch</MobileLink>
          <MobileLink to="/docs">Docs</MobileLink>
        </nav>
      </div>
    </ArenaActionProvider>
  );
}

/**
 * Native fragment scrolling runs before asynchronously queried content exists. This
 * observer gives links such as `/store#pack-x` and `/bots/x#submit` one reliable behavior
 * across client navigation and hard refresh without coupling Shell to either screen.
 */
function ScrollToHash() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    if (hash === '') {
      window.scrollTo({ top: 0, left: 0 });
      return;
    }
    let id: string;
    try {
      id = decodeURIComponent(hash.slice(1));
    } catch {
      return;
    }

    const reveal = () => {
      const target = document.getElementById(id);
      if (!target) return false;
      target.scrollIntoView({ block: 'start' });
      return true;
    };

    if (reveal()) return;
    const observer = new MutationObserver(() => {
      if (reveal()) observer.disconnect();
    });
    observer.observe(document.getElementById('main-content') ?? document.body, {
      childList: true,
      subtree: true,
    });
    const timeout = window.setTimeout(() => observer.disconnect(), 5_000);
    return () => {
      window.clearTimeout(timeout);
      observer.disconnect();
    };
  }, [hash, pathname]);

  return null;
}

function TopLink({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        clsx(
          'btn whitespace-nowrap',
          isActive
            ? 'btn-on'
            : 'border-transparent text-arena-dim hover:text-arena-text',
        )
      }
    >
      {children}
    </NavLink>
  );
}

function MobileLink({
  to,
  children,
}: {
  to: string;
  children: React.ReactNode;
}) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      className={({ isActive }) =>
        clsx(
          'lab flex min-h-12 items-center justify-center px-2 py-2.5 text-center',
          isActive &&
            'text-arena-text [box-shadow:inset_0_2px_0_var(--color-arena-text)]',
        )
      }
    >
      {children}
    </NavLink>
  );
}
