import { createContext, useContext, useEffect, useState } from 'react';

type User = { id: number; email: string; name: string };

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

  useEffect(() => {
    const id = localStorage.getItem('dc.userId');
    const email = localStorage.getItem('dc.email');
    const name = localStorage.getItem('dc.name');
    if (id && email) {
      const parsedId = Number(id);
      if (!Number.isNaN(parsedId) && parsedId > 0) {
        setUserState({ id: parsedId, email, name: name ?? email });
      }
    }
    setReady(true);
  }, []);

  const setUser = (value: User) => {
    setUserState(value);
    localStorage.setItem('dc.userId', value.id.toString());
    localStorage.setItem('dc.email', value.email);
    localStorage.setItem('dc.name', value.name);
  };

  const clearUser = () => {
    setUserState(null);
    localStorage.removeItem('dc.userId');
    localStorage.removeItem('dc.email');
    localStorage.removeItem('dc.name');
  };

  return <AuthContext.Provider value={{ user, setUser, clearUser, ready }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
