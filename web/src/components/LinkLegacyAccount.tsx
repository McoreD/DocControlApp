import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

type Props = {
  onLinked?: () => void;
};

export default function LinkLegacyAccount({ onLinked }: Props) {
  const { setUser, user } = useAuth();
  const navigate = useNavigate();
  const [legacyEmail, setLegacyEmail] = useState('');
  const [legacyPassword, setLegacyPassword] = useState('');
  const [legacyMfa, setLegacyMfa] = useState('');
  const [linking, setLinking] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const linkAccount = async () => {
    setError(null);
    setMessage(null);
    if (!legacyEmail.trim() || !legacyPassword.trim() || !legacyMfa.trim()) {
      setError('Legacy email, password, and MFA code are required.');
      return;
    }
    setLinking(true);
    try {
      await AuthApi.linkLegacy(legacyEmail.trim(), legacyPassword, legacyMfa.trim());
      sessionStorage.removeItem('dc.skipLink');
      const me = await AuthApi.me();
      setUser({
        id: me.userId,
        email: me.email,
        name: me.displayName,
        mfaEnabled: me.mfaEnabled,
        needsLink: false,
      });
      setLegacyEmail('');
      setLegacyPassword('');
      setLegacyMfa('');
      setMessage('Account linked. Your legacy data is now attached to this Microsoft login.');
      onLinked?.();
    } catch (err: any) {
      setError(err.message ?? 'Failed to link account');
    } finally {
      setLinking(false);
    }
  };

  const skipLinking = () => {
    setError(null);
    setMessage(null);
    sessionStorage.setItem('dc.skipLink', 'true');
    if (user) {
      setUser({ ...user, needsLink: false });
    }
    navigate('/');
  };

  return (
    <div className="stack">
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <label>Legacy email</label>
      <input value={legacyEmail} onChange={(e) => setLegacyEmail(e.target.value)} type="email" />
      <label>Legacy password</label>
      <input value={legacyPassword} onChange={(e) => setLegacyPassword(e.target.value)} type="password" />
      <label>Legacy 2FA code</label>
      <input value={legacyMfa} onChange={(e) => setLegacyMfa(e.target.value)} inputMode="numeric" />
      <div className="row" style={{ gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <button type="button" onClick={linkAccount} disabled={linking}>
          {linking ? 'Linking...' : 'Link account'}
        </button>
        <button type="button" onClick={skipLinking} disabled={linking} style={{ background: '#334155', color: '#e2e8f0' }}>
          I don't have a legacy account
        </button>
      </div>
    </div>
  );
}
