import { type FormEvent, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

export default function Register() {
  const { user, setUser, clearUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [mode, setMode] = useState<'register' | 'login'>('register');
  const [emailInput, setEmailInput] = useState<HTMLInputElement | null>(null);
  const [mfaInput, setMfaInput] = useState<HTMLInputElement | null>(null);
  const [email, setEmail] = useState(user?.email ?? '');
  const [name, setName] = useState(user?.name ?? '');
  const [mfaCode, setMfaCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [skipRedirect, setSkipRedirect] = useState(false);

  useEffect(() => {
    if (user && !skipRedirect) {
      navigate(user.mfaEnabled ? '/projects' : '/mfa', { replace: true });
    }
  }, [user, navigate, skipRedirect]);

  const switchMode = (next: 'register' | 'login') => {
    setMode(next);
    setError(null);
    setLoading(false);
    setMfaCode('');
    if (next === 'login') {
      setName('');
      emailInput?.focus();
    } else {
      mfaInput?.blur();
    }
  };

  const registerUser = async (targetOverride?: string) => {
    if (!email.trim()) {
      setError('Email is required');
      emailInput?.focus();
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const registered = await AuthApi.register(email.trim(), name.trim());
      setUser({
        id: registered.id,
        email: registered.email,
        name: registered.displayName ?? registered.email,
        mfaEnabled: registered.mfaEnabled,
      });
      const target = targetOverride ?? (location.state as { from?: string } | null)?.from ?? '/mfa';
      navigate(target, { replace: true });
    } catch (err: any) {
      setError(err.message ?? 'Failed to register');
    } finally {
      setLoading(false);
    }
  };

  const loginUser = async () => {
    if (!email.trim()) {
      setError('Email is required');
      emailInput?.focus();
      return;
    }
    if (!mfaCode.trim()) {
      setError('Enter your 6-digit MFA code');
      mfaInput?.focus();
      return;
    }

    setLoading(true);
    setError(null);
    setSkipRedirect(true);
    try {
      const registered = await AuthApi.register(email.trim(), email.trim());
      if (!registered.mfaEnabled) {
        setError('MFA is not set up for this account yet. Please complete registration to enable it.');
        clearUser();
        return;
      }
      setUser({
        id: registered.id,
        email: registered.email,
        name: registered.displayName ?? registered.email,
        mfaEnabled: registered.mfaEnabled,
      });
      const result = await AuthApi.verifyMfa(mfaCode.trim());
      setUser({
        id: registered.id,
        email: registered.email,
        name: registered.displayName ?? registered.email,
        mfaEnabled: result.mfaEnabled,
      });
      const target = (location.state as { from?: string } | null)?.from ?? '/projects';
      navigate(target, { replace: true });
    } catch (err: any) {
      setError(err.message ?? 'Failed to log in');
      clearUser();
    } finally {
      setSkipRedirect(false);
      setLoading(false);
    }
  };

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (mode === 'register') {
      await registerUser();
    } else {
      await loginUser();
    }
  };

  return (
    <div className="page" style={{ maxWidth: 520, margin: '80px auto' }}>
      <h1>{mode === 'register' ? 'Create your account' : 'Log in'}</h1>
      <p className="muted">
        {mode === 'register'
          ? 'Register once, then create and manage your projects under this account.'
          : 'Enter your account email and 2FA code to sign in.'}
      </p>

      <form className="card stack" style={{ marginTop: 16 }} onSubmit={onSubmit}>
        <label>Email</label>
        <input
          ref={setEmailInput}
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          type="email"
          placeholder="you@example.com"
        />

        {mode === 'register' && (
          <>
            <label>Display name</label>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="How should we show your name?"
            />
          </>
        )}

        {mode === 'login' && (
          <>
            <label>6-digit MFA code</label>
            <input
              ref={setMfaInput}
              value={mfaCode}
              onChange={(e) => setMfaCode(e.target.value)}
              placeholder="123456"
              inputMode="numeric"
            />
          </>
        )}

        <button type="submit" disabled={loading}>
          {loading ? (mode === 'register' ? 'Registering...' : 'Logging in...') : mode === 'register' ? 'Register' : 'Log in'}
        </button>
        {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      </form>

      <div className="card" style={{ marginTop: 12 }}>
        <p className="muted" style={{ margin: 0 }}>
          {mode === 'register' ? 'Already registered? ' : 'Need an account? '}
          <button
            type="button"
            onClick={() => switchMode(mode === 'register' ? 'login' : 'register')}
            style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0 }}
          >
            {mode === 'register' ? 'Log in' : 'Register'}
          </button>
          .
        </p>
      </div>
    </div>
  );
}
