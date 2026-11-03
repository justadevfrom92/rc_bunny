import { createContext, useContext, useEffect, useRef, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Navigate, useLocation } from 'react-router-dom';
import { api, ApiError, post, queryClient, setKnownAccessVersion } from './api';
import type { Me } from './types';
import { useMeta } from './queries';

/*
  Client-side access checks.

  The server is the authority: every endpoint checks the person's current access (from a cache that is
  cleared the moment Sheriff changes it). The client mirrors it so people only see what they can use:
    - useCan('refunds')          → hide or disable a button
    - <RequirePerm perm="corral"> → a whole route; shows "No access" instead of the screen
    - menus and the launcher are built from me.apps
  The client copy stays fresh because:
    - "me" is refetched every 30 s and whenever the window regains focus
    - every API response carries X-Access-Version; a change refetches "me" immediately (api.ts)
    - a 403 missing_perm from any call also refetches "me"
  When access shrinks, cached data for apps the person lost is dropped from memory.
*/

const MeContext = createContext<Me | null>(null);

export function useMe(): Me {
  const me = useContext(MeContext);
  if (!me) throw new Error('useMe outside <MeProvider>');
  return me;
}

export function useCan(perm: string): boolean {
  return useContext(MeContext)?.perms.includes(perm) ?? false;
}

export function Can({ perm, children, fallback = null }: { perm: string; children: ReactNode; fallback?: ReactNode }) {
  return <>{useCan(perm) ? children : fallback}</>;
}

export const meQuery = {
  queryKey: ['me'],
  queryFn: () => api<Me>('/api/auth/me'),
  staleTime: 15_000,
  refetchInterval: 30_000,
  refetchOnWindowFocus: true,
  retry: false,
} as const;

/** Loads the signed-in person; sends anyone signed out to /login. */
export function MeProvider({ children }: { children: ReactNode }) {
  const location = useLocation();
  const qc = useQueryClient();
  const { data: me, error, isPending } = useQuery(meQuery);
  const lastPerms = useRef<string[] | null>(null);

  useEffect(() => {
    if (!me) return;
    setKnownAccessVersion(me.accessVersion);
    // Access shrank: forget anything cached for the apps they lost.
    const lost = (lastPerms.current ?? []).filter((p) => !me.perms.includes(p));
    lost.forEach((app) => qc.removeQueries({ queryKey: [app] }));
    lastPerms.current = me.perms;
  }, [me, qc]);

  if (isPending) return <div className="boot"><div className="spinner" /></div>;
  if (error instanceof ApiError && error.status === 401) {
    return <Navigate to={`/login?next=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  }
  if (error || !me) return <div className="boot"><p>Couldn't reach the server. <button onClick={() => window.location.reload()}>Try again</button></p></div>;

  return <MeContext.Provider value={me}>{children}</MeContext.Provider>;
}

/** Guards a route by app or right. Re-evaluates live: if access is removed while the page is open, it closes. */
export function RequirePerm({ perm, children }: { perm: string; children: ReactNode }) {
  const can = useCan(perm);
  return can ? <>{children}</> : <NoAccess perm={perm} />;
}

export function NoAccess({ perm }: { perm?: string }) {
  const meta = useMeta();
  const name = perm ? meta.data?.apps.find((a) => a.key === perm)?.name ?? meta.data?.rights[perm] ?? perm : null;
  return (
    <div className="panel empty">
      <h2>No access</h2>
      <p className="muted">
        {name ? <>You don't have <strong>{name}</strong> access.</> : <>You don't have access to this.</>} If you need it,
        ask someone with Sheriff (or your manager) to add it.
      </p>
    </div>
  );
}

/** Ends the session (or a "log in as") and wipes every cached response. */
export async function signOut() {
  await post('/api/auth/logout').catch(() => undefined);
  setKnownAccessVersion(null);
  queryClient.clear();
  window.location.assign('/login');
}

export async function returnToSelf() {
  await post('/api/auth/return');
  queryClient.clear();
  window.location.assign('/sheriff/users');
}

/** Any 403 missing_perm means our copy of "me" is out of date. */
export function handleForbidden(err: unknown) {
  if (err instanceof ApiError && err.code === 'missing_perm') void queryClient.invalidateQueries({ queryKey: ['me'] });
}
