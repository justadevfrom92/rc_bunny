import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { api } from './api';

export interface Me {
  id: number;
  name: string;
  email: string;
  role: string;
  permissions: string[];
}

interface Auth {
  me: Me | null | undefined; // undefined = still checking
  setMe: (me: Me | null) => void;
  can: (permission: string) => boolean;
  refresh: () => void;
}

const AuthContext = createContext<Auth>(null!);
export const useAuth = () => useContext(AuthContext);

/**
 * Knows who is signed in and what they can do. It re-checks when the tab gets focus, every minute, and whenever the
 * server says 401/403, so if someone's access changes, their menus and buttons change too. The server checks every call.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null | undefined>(undefined);

  const refresh = useCallback(() => {
    api.get<Me>('/api/me').then(setMe, () => setMe(null));
  }, []);

  useEffect(() => {
    refresh();
    const timer = setInterval(refresh, 60_000);
    window.addEventListener('focus', refresh);
    window.addEventListener('access-changed', refresh);
    return () => {
      clearInterval(timer);
      window.removeEventListener('focus', refresh);
      window.removeEventListener('access-changed', refresh);
    };
  }, [refresh]);

  const can = (permission: string) => !!me?.permissions.includes(permission);
  return <AuthContext.Provider value={{ me, setMe, can, refresh }}>{children}</AuthContext.Provider>;
}

/** Shows the page only to people with the permission. */
export function RequirePermission({ permission, children }: { permission: string; children: ReactNode }) {
  const { can } = useAuth();
  if (!can(permission)) return <p className="notice">You don't have access to this page. Ask an administrator if you need it.</p>;
  return <>{children}</>;
}
