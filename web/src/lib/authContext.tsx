import { createContext, useContext, useEffect, useState } from 'react';
import { AuthApi } from './api';

type User = { id: number; email: string; name: string; mfaEnabled: boolean; needsLink?: boolean };

type AuthContextValue = {
  user: User | null;
  setUser: (user: User) => void;
  clearUser: () => void;
  ready: boolean;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUserState] = useState<User | null>(null);
  const [ready, setReady] = useState(false);
  const isDev = import.meta.env.DEV;

  useEffect(() => {
    if (!isDev) return;
    const id = localStorage.getItem('dc.userId');
    const email = localStorage.getItem('dc.email');
    const name = localStorage.getItem('dc.name');
    const mfa = localStorage.getItem('dc.mfa');
    if (id && email) {
      const parsedId = Number(id);
      if (!Number.isNaN(parsedId) && parsedId > 0) {
        setUserState({ id: parsedId, email, name: name ?? email, mfaEnabled: mfa === 'true', needsLink: false });
      }
    }
  }, [isDev]);

  useEffect(() => {
    if (isDev) {
      if (!user || ready) {
        if (!ready) setReady(true);
        return;
      }
      const sync = async () => {
        try {
          const me = await AuthApi.me();
          const current = { id: me.userId, email: me.email, name: me.displayName, mfaEnabled: me.mfaEnabled, needsLink: false };
          setUser(current);
        } catch {
          clearUser();
        } finally {
          setReady(true);
        }
      };
      void sync();
      return;
    }

    if (ready) return;
    const syncSwa = async () => {
      try {
        const res = await fetch('/.auth/me');
        if (!res.ok) {
          setReady(true);
          return;
        }
        const payload = await res.json();
        const principal = payload?.clientPrincipal;
        if (!principal?.userDetails) {
          setReady(true);
          return;
        }
        const me = await AuthApi.me();
        setUserState({
          id: me.userId,
          email: me.email,
          name: me.displayName,
          mfaEnabled: true,
          needsLink: !me.hasPassword,
        });
      } catch {
        setUserState(null);
      } finally {
        setReady(true);
      }
    };
    void syncSwa();
  }, [isDev, ready, user]);

  const setUser = (value: User) => {
    setUserState(value);
    if (isDev) {
      localStorage.setItem('dc.userId', value.id.toString());
      localStorage.setItem('dc.email', value.email);
      localStorage.setItem('dc.name', value.name);
      localStorage.setItem('dc.mfa', value.mfaEnabled ? 'true' : 'false');
    }
  };

  const clearUser = () => {
    setUserState(null);
    if (isDev) {
      localStorage.removeItem('dc.userId');
      localStorage.removeItem('dc.email');
      localStorage.removeItem('dc.name');
      localStorage.removeItem('dc.mfa');
    }
  };

  return <AuthContext.Provider value={{ user, setUser, clearUser, ready }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
