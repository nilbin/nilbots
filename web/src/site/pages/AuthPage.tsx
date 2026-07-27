import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth';
import { errorMessage } from '../errorMessage';
import { useAuthProviders, useLogin, useRegister } from '../queries';

export default function AuthPage() {
  const [mode, setMode] = useState<'login' | 'register'>(() =>
    new URLSearchParams(window.location.search).get('mode') === 'register' ? 'register' : 'login',
  );
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const { refresh } = useAuth();
  const { data: providers } = useAuthProviders();
  // Carried through Google and back, so a player who was sent here from a protected page —
  // or from the CLI's and the app's /connect/authorize bounce — resumes where they were.
  const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
  const externalError = new URLSearchParams(window.location.search).get('error');
  const navigate = useNavigate();
  const register = useRegister();
  const login = useLogin();
  const active = mode === 'register' ? register : login;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (mode === 'register') await register.mutateAsync({ displayName, email, password });
    else await login.mutateAsync({ email, password });
    await refresh();
    const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/')) window.location.assign(returnUrl);
    else navigate('/garage');
  };

  return (
    <div className="mx-auto mt-10 max-w-sm">
      <div className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <div className="mb-5 flex gap-1 rounded-lg bg-arena-bg p-1 text-sm">
          {(['login', 'register'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={
                'flex-1 rounded-md py-1.5 transition-colors ' +
                (mode === m ? 'bg-arena-panel text-arena-text' : 'text-arena-dim')
              }
            >
              {m === 'login' ? 'Sign in' : 'Create account'}
            </button>
          ))}
        </div>
        <form onSubmit={submit} className="flex flex-col gap-3">
          {mode === 'register' && (
            <Field label="Display name">
              <input
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                required
                minLength={2}
                maxLength={40}
                className={inputClass}
              />
            </Field>
          )}
          <Field label="Email">
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className={inputClass}
            />
          </Field>
          <Field label="Password">
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              className={inputClass}
            />
          </Field>
          {externalError === 'email-taken' && (
            <p className="text-sm text-arena-accent">
              That address already has a nilbots account with a password. Sign in with it
              here, and Google will be linked for next time.
            </p>
          )}
          {externalError === 'google' && (
            <p className="text-sm text-arena-hot">Google sign-in did not complete.</p>
          )}
          {active.isError && (
            <p className="text-sm text-arena-hot">
              {errorMessage(active.error, 'Something went wrong.')}
            </p>
          )}
          <button
            type="submit"
            disabled={active.isPending}
            className="mt-2 rounded-md bg-arena-accent py-2 font-semibold text-arena-bg transition-opacity disabled:opacity-50"
          >
            {mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        {/* Rendered only where the server has credentials — a button that can only 404 is
            worse than no button. A plain link, not a fetch: the browser has to leave for
            Google and come back, which XHR cannot do. */}
        {providers?.google && (
          <>
            <div className="my-5 flex items-center gap-3">
              <span className="h-px flex-1 bg-arena-edge" />
              <span className="font-mono text-[10px] tracking-widest text-arena-dim">OR</span>
              <span className="h-px flex-1 bg-arena-edge" />
            </div>
            <a
              href={`/api/accounts/external/google${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`}
              className="flex items-center justify-center gap-2.5 rounded-md border border-arena-edge bg-arena-bg px-4 py-2 text-sm font-semibold text-arena-text transition-colors hover:border-arena-dim"
            >
              <GoogleMark />
              Continue with Google
            </a>
            <p className="mt-2 text-center text-xs text-arena-dim">
              Signs you in, or creates an account if you do not have one.
            </p>
          </>
        )}
      </div>
    </div>
  );
}

const inputClass =
  'rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm outline-none focus:border-arena-accent';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1 text-xs text-arena-dim">
      {label}
      {children}
    </label>
  );
}

/**
 * Google's mark, inline.
 *
 * Their brand guidelines require the four-colour G on a sign-in button, and inlining it
 * keeps the login page from depending on a network fetch to a third party — which is both
 * a render-blocking request and a tracking surface on the one page where a visitor has not
 * agreed to anything yet.
 */
function GoogleMark() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden className="size-4">
      <path fill="#4285F4" d="M23.5 12.3c0-.8-.1-1.6-.2-2.3H12v4.5h6.5a5.6 5.6 0 0 1-2.4 3.7v3h3.9c2.3-2.1 3.5-5.2 3.5-8.9Z" />
      <path fill="#34A853" d="M12 24c3.2 0 5.9-1.1 7.9-2.9l-3.9-3c-1.1.7-2.4 1.2-4 1.2-3.1 0-5.7-2.1-6.6-4.9H1.4v3.1A12 12 0 0 0 12 24Z" />
      <path fill="#FBBC05" d="M5.4 14.4a7.2 7.2 0 0 1 0-4.6V6.7H1.4a12 12 0 0 0 0 10.8l4-3.1Z" />
      <path fill="#EA4335" d="M12 4.8c1.8 0 3.3.6 4.6 1.8l3.4-3.4A12 12 0 0 0 1.4 6.7l4 3.1C6.3 6.9 8.9 4.8 12 4.8Z" />
    </svg>
  );
}
