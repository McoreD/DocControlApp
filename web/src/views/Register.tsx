import { FormEvent, useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

export default function Register() {
  const { user, setUser } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState(user?.email ?? '');
  const [name, setName] = useState(user?.name ?? '');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      navigate('/projects', { replace: true });
    }
  }, [user, navigate]);

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!email.trim()) {
      setError('Email is required');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const registered = await AuthApi.register(email.trim(), name.trim());
      setUser({ id: registered.id, email: registered.email, name: registered.displayName ?? registered.email });
      const target = (location.state as { from?: string } | null)?.from ?? '/projects';
      navigate(target, { replace: true });
    } catch (err: any) {
      setError(err.message ?? 'Failed to register');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page" style={{ maxWidth: 520, margin: '80px auto' }}>
      <h1>Create your account</h1>
      <p className="muted">Register once, then create and manage your projects under this account.</p>

      <form className="card stack" style={{ marginTop: 16 }} onSubmit={onSubmit}>
        <label>Email</label>
        <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" placeholder="you@example.com" />

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
            onClick={() => navigate('/projects')}
            style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0 }}
          >
            Go to your projects
          </button>
          .
        </p>
      </div>
    </div>
  );
}
