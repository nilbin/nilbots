import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';

import { API_BASE_URL } from '@/api/config';
import { setAccessTokenProvider } from '@/api/client';
import { clearSession, readSession, writeSession, type StoredSession } from '@/auth/tokens';

// Closes the browser tab that completed the redirect. Required at module scope by
// expo-web-browser, before any auth session starts.
WebBrowser.maybeCompleteAuthSession();

/**
 * Authorization Code + PKCE against the server's own OpenIddict, in the system browser.
 *
 * PKCE and not a client secret: a public client ships its secret to every device, where
 * it is not a secret. The server registers `nilbots-mobile` as a public client for the
 * same reason.
 *
 * The system browser and not a WebView: an embedded WebView can read everything typed
 * into it, which is exactly what a login form contains, and providers reject it for that
 * reason. `expo-auth-session` uses ASWebAuthenticationSession on iOS, which also means
 * an existing browser session can sign the player in without retyping anything.
 */

const CLIENT_ID = 'nilbots-mobile';
/** Must match the URI seeded in OpenIddictSetup exactly — a trailing slash fails. */
const REDIRECT_URI = 'nilbots://auth/callback';

/**
 * `offline_access` and nothing else.
 *
 * It is what mints a refresh token — without it every relaunch is a fresh login. And it
 * is the only scope the server registers (`OpenIddictSetup.RegisterScopes`), so asking
 * for `openid` or `profile` as well fails the authorize request outright with
 * `invalid_scope` rather than being quietly ignored. The app needs neither: it wants an
 * access token for `/api/bots/mine`, not an id_token or profile claims.
 */
const SCOPES = ['offline_access'];

/** Renew this far before expiry so a request in flight never races the deadline. */
const RENEW_MARGIN_MS = 60_000;

type AuthState = {
  status: 'loading' | 'signed-out' | 'signed-in';
  signIn: () => Promise<void>;
  signOut: () => Promise<void>;
  /** Set when a sign-in attempt failed, cleared when another begins. */
  error: string | null;
};

const AuthContext = createContext<AuthState | null>(null);

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside an AuthProvider');
  return context;
}

const discovery: AuthSession.DiscoveryDocument = {
  authorizationEndpoint: `${API_BASE_URL}/connect/authorize`,
  tokenEndpoint: `${API_BASE_URL}/connect/token`,
};

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthState['status']>('loading');
  const [error, setError] = useState<string | null>(null);

  // The session is a ref, not state: `api/client` reads it on every request, and a
  // re-render per token refresh would remount screens mid-request for no visible reason.
  // `status` is the part the UI actually needs.
  const session = useRef<StoredSession | null>(null);

  const store = useCallback(async (next: StoredSession | null) => {
    session.current = next;
    if (next) await writeSession(next);
    else await clearSession();
    setStatus(next ? 'signed-in' : 'signed-out');
  }, []);

  /**
   * Hand `api/client` a valid access token, renewing first if this one is spent.
   *
   * Refreshes are deduplicated through `inFlight`: a screen mounting three queries at
   * once would otherwise fire three refreshes, and OpenIddict rotates refresh tokens, so
   * the second and third would present one that had just been consumed and sign the
   * player out.
   */
  const inFlight = useRef<Promise<string | null> | null>(null);
  const accessToken = useCallback(async (): Promise<string | null> => {
    const current = session.current;
    if (!current) return null;
    const fresh = !current.expiresAt || current.expiresAt - RENEW_MARGIN_MS > Date.now();
    if (fresh) return current.accessToken;
    if (!current.refreshToken) {
      await store(null);
      return null;
    }
    if (inFlight.current) return inFlight.current;

    inFlight.current = (async () => {
      try {
        const renewed = await AuthSession.refreshAsync(
          { clientId: CLIENT_ID, refreshToken: current.refreshToken },
          discovery,
        );
        const next = toStored(renewed, current.refreshToken);
        await store(next);
        return next.accessToken;
      } catch {
        // A refresh token that will not exchange is revoked, expired or already rotated.
        // Nothing to retry — sign out so the UI offers a login rather than failing every
        // request silently.
        await store(null);
        return null;
      } finally {
        inFlight.current = null;
      }
    })();
    return inFlight.current;
  }, [store]);

  // Registered before the first render so no query can fire without it.
  useEffect(() => {
    setAccessTokenProvider(accessToken);
  }, [accessToken]);

  useEffect(() => {
    void (async () => {
      session.current = await readSession();
      setStatus(session.current ? 'signed-in' : 'signed-out');
    })();
  }, []);

  const signIn = useCallback(async () => {
    setError(null);
    try {
      const request = new AuthSession.AuthRequest({
        clientId: CLIENT_ID,
        redirectUri: REDIRECT_URI,
        scopes: SCOPES,
        usePKCE: true,
        responseType: AuthSession.ResponseType.Code,
      });

      const result = await request.promptAsync(discovery);
      if (result.type !== 'success') {
        // Dismissing the browser is a choice, not a failure — saying "sign-in failed"
        // for a deliberate cancel is a lie the player can see through.
        if (result.type === 'error') setError(result.error?.message ?? 'Sign-in failed.');
        return;
      }

      const exchanged = await AuthSession.exchangeCodeAsync(
        {
          clientId: CLIENT_ID,
          redirectUri: REDIRECT_URI,
          code: result.params.code,
          // Proves this app started the flow it is now finishing.
          extraParams: { code_verifier: request.codeVerifier ?? '' },
        },
        discovery,
      );
      await store(toStored(exchanged));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
    }
  }, [store]);

  const signOut = useCallback(async () => {
    await store(null);
  }, [store]);

  const value = useMemo<AuthState>(
    () => ({ status, signIn, signOut, error }),
    [status, signIn, signOut, error],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/**
 * A refresh response may omit the refresh token when the server is not rotating it, in
 * which case the previous one stays valid — dropping it there would strand the session
 * at the next expiry.
 */
function toStored(
  response: AuthSession.TokenResponse,
  previousRefreshToken?: string,
): StoredSession {
  return {
    accessToken: response.accessToken,
    refreshToken: response.refreshToken ?? previousRefreshToken,
    expiresAt: response.expiresIn
      ? (response.issuedAt ?? Math.floor(Date.now() / 1000)) * 1000 + response.expiresIn * 1000
      : undefined,
  };
}
