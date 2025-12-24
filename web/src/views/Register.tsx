import { type FormEvent, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

export default function Register() {
  const { user, setUser, clearUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [mode, setMode] = useState<'register' | 'login' | 'setPassword'>('register');
  const [emailInput, setEmailInput] = useState<HTMLInputElement | null>(null);
  const [mfaInput, setMfaInput] = useState<HTMLInputElement | null>(null);
  const [email, setEmail] = useState(user?.email ?? '');
  const [name, setName] = useState(user?.name ?? '');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [skipRedirect, setSkipRedirect] = useState(false);

  useEffect(() => {
    if (user && !skipRedirect) {
      navigate(user.mfaEnabled ? '/projects' : '/mfa', { replace: true });
    }
  }, [user, navigate, skipRedirect]);

  const switchMode = (next: 'register' | 'login' | 'setPassword') => {
    setMode(next);
    setError(null);
    setLoading(false);
    setMfaCode('');
    setPassword('');
    setConfirmPassword('');
    if (next === 'login') {
      setName('');
      emailInput?.focus();
    } else if (next === 'register') {
      mfaInput?.blur();
    }
  };

  const registerUser = async (targetOverride?: string) => {
    if (!email.trim()) {
      setError('Email is required');
      emailInput?.focus();
      return;
    }
    if (!password.trim()) {
      setError('Password is required');
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const registered = await AuthApi.register(email.trim(), name.trim(), password);
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
    if (!password.trim()) {
      setError('Password is required');
      return;
    }

    setLoading(true);
    setError(null);
    setSkipRedirect(true);
    try {
      const registered = await AuthApi.login(email.trim(), password);
      setUser({
        id: registered.id,
        email: registered.email,
        name: registered.displayName ?? registered.email,
        mfaEnabled: registered.mfaEnabled,
      });
      if (registered.mfaEnabled) {
        if (!mfaCode.trim()) {
          setError('Enter your 6-digit MFA code');
          mfaInput?.focus();
          clearUser();
          return;
        }
        const result = await AuthApi.verifyMfa(mfaCode.trim());
        setUser({
          id: registered.id,
          email: registered.email,
          name: registered.displayName ?? registered.email,
          mfaEnabled: result.mfaEnabled,
        });
        const target = (location.state as { from?: string } | null)?.from ?? '/projects';
        navigate(target, { replace: true });
      } else {
        navigate('/mfa', { replace: true });
      }
    } catch (err: any) {
      const message = err.message ?? 'Failed to log in';
      if (message.includes('Password not set')) {
        switchMode('setPassword');
        setError('Password not set. Set your password to continue.');
      } else {
        setError(message);
        clearUser();
      }
    } finally {
      setSkipRedirect(false);
      setLoading(false);
    }
  };

  const setInitialPassword = async () => {
    if (!email.trim()) {
      setError('Email is required');
      emailInput?.focus();
      return;
    }
    if (!password.trim()) {
      setError('Password is required');
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);
    setError(null);
    setSkipRedirect(true);
    try {
      const registered = await AuthApi.setInitialPassword(email.trim(), password);
      setUser({
        id: registered.id,
        email: registered.email,
        name: registered.displayName ?? registered.email,
        mfaEnabled: registered.mfaEnabled,
      });
      if (registered.mfaEnabled) {
        if (!mfaCode.trim()) {
          setError('Enter your 6-digit MFA code');
          mfaInput?.focus();
          clearUser();
          return;
        }
        const result = await AuthApi.verifyMfa(mfaCode.trim());
        setUser({
          id: registered.id,
          email: registered.email,
          name: registered.displayName ?? registered.email,
          mfaEnabled: result.mfaEnabled,
        });
        const target = (location.state as { from?: string } | null)?.from ?? '/projects';
        navigate(target, { replace: true });
      } else {
        navigate('/mfa', { replace: true });
      }
    } catch (err: any) {
      setError(err.message ?? 'Failed to set password');
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
    } else if (mode === 'login') {
      await loginUser();
    } else {
      await setInitialPassword();
    }
  };

  return (
    <div className="page" style={{ maxWidth: 520, margin: '80px auto' }}>
      <h1>
        {mode === 'register' ? 'Create your account' : mode === 'login' ? 'Log in' : 'Set your password'}
      </h1>
      <p className="muted">
        {mode === 'register'
          ? 'Register once, then create and manage your projects under this account.'
          : mode === 'login'
            ? 'Enter your account email, password, and 2FA code to sign in.'
            : 'This account does not have a password yet. Set one to continue.'}
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

        {(mode === 'register' || mode === 'login' || mode === 'setPassword') && (
          <>
            <label>Password</label>
            <input
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              type="password"
              placeholder="Password"
            />
          </>
        )}

        {(mode === 'register' || mode === 'setPassword') && (
          <>
            <label>Confirm password</label>
            <input
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              type="password"
              placeholder="Confirm password"
            />
          </>
        )}

        {(mode === 'login' || mode === 'setPassword') && (
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
          {loading
            ? mode === 'register'
              ? 'Registering...'
              : mode === 'login'
                ? 'Logging in...'
                : 'Saving...'
            : mode === 'register'
              ? 'Register'
              : mode === 'login'
                ? 'Log in'
                : 'Set password'}
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
