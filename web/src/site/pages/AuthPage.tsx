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
  const returnUrl = safeReturnUrl(
    new URLSearchParams(window.location.search).get('returnUrl'),
  );
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
    if (returnUrl) navigate(returnUrl);
    else navigate('/garage');
  };

  return (
    <div className="mx-auto mt-10 max-w-sm">
      <div className="panel pad">
        <h1 className="type-display mb-4 text-[24px]">Account</h1>
        <div
          className="panel-quiet mb-4 flex gap-1 p-1"
          role="group"
          aria-label="Account access"
        >
          {(['login', 'register'] as const).map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setMode(m)}
              aria-pressed={mode === m}
              className={
                'btn flex-1 ' +
                (mode === m
                  ? 'btn-on text-arena-text'
                  : 'border-transparent text-arena-dim')
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
                autoComplete="name"
                className="field"
              />
            </Field>
          )}
          <Field label="Email">
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
              className="field"
            />
          </Field>
          <Field label="Password">
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              minLength={8}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              className="field"
            />
          </Field>
          {externalError === 'email-taken' && (
            <p className="t-body text-arena-text">
              That address already has a nilbots account with a password. Sign in with it
              here, and Google will be linked for next time.
            </p>
          )}
          {externalError === 'google' && (
            <p className="t-body text-arena-hot">Google sign-in did not complete.</p>
          )}
          {active.isError && (
            <p className="t-body text-arena-hot">
              {errorMessage(active.error, 'Something went wrong.')}
            </p>
          )}
          <button
            type="submit"
            disabled={active.isPending}
            className="btn btn-on mt-1 w-full disabled:opacity-50"
          >
            {mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        {/* Rendered only where the server has credentials — a button that can only 404 is
            worse than no button. A plain link, not a fetch: the browser has to leave for
            Google and come back, which XHR cannot do. */}
        {providers?.google && (
          <>
            <div className="my-4 flex items-center gap-3">
              <span className="h-px flex-1 bg-arena-edge" />
              <span className="lab">Or</span>
              <span className="h-px flex-1 bg-arena-edge" />
            </div>
            <a
              href={`/api/accounts/external/google${returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : ''}`}
              className="btn flex w-full items-center justify-center gap-2.5"
            >
              <GoogleMark />
              Continue with Google
            </a>
            <p className="t-meta mt-2 text-center">
              Signs you in, or creates an account if you do not have one.
            </p>
          </>
        )}
      </div>
    </div>
  );
}

/** Mirror the backend's same-origin redirect rule before any auth flow receives it. */
function safeReturnUrl(candidate: string | null) {
  return candidate &&
    candidate.length > 1 &&
    candidate[0] === '/' &&
    candidate[1] !== '/' &&
    candidate[1] !== '\\'
    ? candidate
    : null;
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="t-meta flex flex-col gap-1">
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
