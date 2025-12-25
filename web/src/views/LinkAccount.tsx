import { useState } from 'react';
import LinkLegacyAccount from '../components/LinkLegacyAccount';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

export default function LinkAccount() {
  const { user, setUser } = useAuth();
  const isDev = import.meta.env.DEV;
  const [displayName, setDisplayName] = useState(user?.name ?? '');
  const [savingName, setSavingName] = useState(false);
  const [nameMessage, setNameMessage] = useState<string | null>(null);
  const [nameError, setNameError] = useState<string | null>(null);
  const nameMatchesEmail = !!user && user.name === user.email;

  const saveDisplayName = async () => {
    setNameError(null);
    setNameMessage(null);
    const trimmed = displayName.trim();
    if (!trimmed) {
      setNameError('Display name is required.');
      return;
    }
    if (!user) {
      setNameError('User not loaded yet.');
      return;
    }
    setSavingName(true);
    try {
      const result = await AuthApi.updateProfile(trimmed);
      setUser({ ...user, name: result.displayName ?? trimmed });
      setNameMessage('Display name saved.');
    } catch (err: any) {
      setNameError(err.message ?? 'Failed to save display name');
    } finally {
      setSavingName(false);
    }
  };

  if (isDev) {
    return (
      <div className="page" style={{ maxWidth: 560, margin: '80px auto' }}>
        <h1>Link account</h1>
        <p className="muted">Linking is only available in production.</p>
      </div>
    );
  }

  return (
    <div className="page" style={{ maxWidth: 560, margin: '80px auto' }}>
      <h1>Link your legacy account</h1>
      <p className="muted">
        Sign in with your previous DocControl credentials to bring your existing data into this Microsoft login.
      </p>
      <div className="card" style={{ marginTop: 16 }}>
        <h3>Set your display name</h3>
        <p className="muted">
          This is how your name appears in DocControl{ nameMatchesEmail ? '. Right now it matches your email.' : '.'}
        </p>
        {nameMessage && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{nameMessage}</div>}
        {nameError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{nameError}</div>}
        <div className="stack">
          <label>Display name</label>
          <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Your name" />
          <div className="row" style={{ gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            <button type="button" onClick={saveDisplayName} disabled={savingName || !displayName.trim()}>
              {savingName ? 'Saving...' : 'Save display name'}
            </button>
          </div>
        </div>
      </div>
      <div className="card" style={{ marginTop: 16 }}>
        <LinkLegacyAccount />
      </div>
    </div>
  );
}
