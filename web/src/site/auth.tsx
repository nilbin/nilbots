import { createContext, useContext } from 'react';
import { type Me } from './api';
import { useLogout, useMe } from './queries';

/**
 * Who is signed in.
 *
 * The session is a query like everything else — so it caches, dedupes across the six
 * components that read it, and can be invalidated by the writes that change it. What the
 * context still owns is the *shape*: `user`/`loading`/`refresh`/`logout` is what pages
 * consume, and they should not each learn a query key to ask "am I signed in".
 *
 * `loading` matters more than it looks. Several pages redirect anonymous visitors, and
 * treating "not loaded yet" as "not signed in" bounces a signed-in user to the login page
 * on every hard refresh.
 */
interface AuthState {
  user: Me | null;
  loading: boolean;
  refresh: () => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthState>({
  user: null,
  loading: true,
  refresh: async () => {},
  logout: async () => {},
});

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const me = useMe();
  const signOut = useLogout();

  const state: AuthState = {
    user: me.data ?? null,
    loading: me.isPending,
    refresh: async () => {
      await me.refetch();
    },
    logout: async () => {
      await signOut.mutateAsync();
    },
  };

  return <AuthContext.Provider value={state}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
