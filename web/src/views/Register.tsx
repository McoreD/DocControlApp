import { type FormEvent, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

export default function Register() {
  const { user, setUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [emailInput, setEmailInput] = useState<HTMLInputElement | null>(null);
  const [email, setEmail] = useState(user?.email ?? '');
  const [name, setName] = useState(user?.name ?? '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      navigate(user.mfaEnabled ? '/projects' : '/mfa', { replace: true });
    }
  }, [user, navigate]);

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

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    await registerUser();
  };

  const goToProjects = () => {
    if (user) {
      navigate(user.mfaEnabled ? '/projects' : '/mfa', { replace: true });
      return;
    }
    void registerUser('/projects');
  };

  return (
    <div className="page" style={{ maxWidth: 520, margin: '80px auto' }}>
      <h1>Create your account</h1>
      <p className="muted">Register once, then create and manage your projects under this account.</p>

      <form className="card stack" style={{ marginTop: 16 }} onSubmit={onSubmit}>
        <label>Email</label>
        <input
          ref={setEmailInput}
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          type="email"
          placeholder="you@example.com"
        />

        <label>Display name</label>
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="How should we show your name?"
        />

        <button type="submit" disabled={loading}>
          {loading ? 'Registering...' : 'Register'}
        </button>
        {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      </form>

      <div className="card" style={{ marginTop: 12 }}>
        <p className="muted" style={{ margin: 0 }}>
          Already registered?{' '}
          <button
            type="button"
            onClick={goToProjects}
            style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0 }}
          >
            Log in
          </button>
          .
        </p>
      </div>
    </div>
  );
}
