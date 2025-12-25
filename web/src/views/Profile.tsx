import { useEffect, useState } from 'react';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';
import LinkLegacyAccount from '../components/LinkLegacyAccount';

type SetupState = { secret: string; otpauthUrl: string } | null;

export default function Profile() {
  const { user, setUser } = useAuth();
  const isDev = import.meta.env.DEV;
  const authMode = isDev ? 'password' : (localStorage.getItem('dc.authMode') ?? 'microsoft');
  const [displayName, setDisplayName] = useState(user?.name ?? '');
  const [savingName, setSavingName] = useState(false);
  const [nameMessage, setNameMessage] = useState<string | null>(null);
  const [nameError, setNameError] = useState<string | null>(null);

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordMessage, setPasswordMessage] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [savingPassword, setSavingPassword] = useState(false);

  const [setup, setSetup] = useState<SetupState>(null);
  const [mfaCode, setMfaCode] = useState('');
  const [mfaMessage, setMfaMessage] = useState<string | null>(null);
  const [mfaError, setMfaError] = useState<string | null>(null);
  const [mfaLoading, setMfaLoading] = useState(false);

  const [swaPrincipal, setSwaPrincipal] = useState<string | null>(null);

  useEffect(() => {
    setDisplayName(user?.name ?? '');
  }, [user]);

  useEffect(() => {
    if (isDev || authMode === 'password') {
      setSwaPrincipal(null);
      return;
    }
    const load = async () => {
      try {
        const res = await fetch('/.auth/me');
        if (!res.ok) return;
        const payload = await res.json();
        const details = payload?.clientPrincipal?.userDetails as string | undefined;
        if (details) {
          setSwaPrincipal(details);
        }
      } catch {
        // Ignore.
      }
    };
    void load();
  }, [authMode, isDev]);

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

  const changePassword = async () => {
    setPasswordError(null);
    setPasswordMessage(null);
    if (!currentPassword.trim() || !newPassword.trim()) {
      setPasswordError('Current and new password are required.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError('New passwords do not match.');
      return;
    }
    setSavingPassword(true);
    try {
      await AuthApi.changePassword(currentPassword, newPassword);
      setPasswordMessage('Password updated.');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err: any) {
      setPasswordError(err.message ?? 'Failed to change password');
    } finally {
      setSavingPassword(false);
    }
  };

  const startMfa = async () => {
    setMfaError(null);
    setMfaMessage(null);
    setMfaLoading(true);
    try {
      const result = await AuthApi.startMfa();
      setSetup(result);
      setMfaMessage(user?.mfaEnabled ? 'MFA reset started. Verify the new code to finish.' : 'MFA setup started.');
    } catch (err: any) {
      setMfaError(err.message ?? 'Failed to start MFA');
    } finally {
      setMfaLoading(false);
    }
  };

  const verifyMfa = async () => {
    if (!mfaCode.trim()) {
      setMfaError('Enter the 6-digit code from your authenticator app.');
      return;
    }
    setMfaLoading(true);
    setMfaError(null);
    try {
      const result = await AuthApi.verifyMfa(mfaCode.trim());
      if (result.mfaEnabled && user) {
        setUser({ ...user, mfaEnabled: true });
        setMfaMessage('MFA enabled.');
        setSetup(null);
        setMfaCode('');
      }
    } catch (err: any) {
      setMfaError(err.message ?? 'Invalid code');
    } finally {
      setMfaLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Profile</h1>
      <p className="muted">Manage your personal settings, password, and MFA.</p>

      <div className="card" style={{ marginTop: 12 }}>
        <h3>Display name</h3>
        {nameMessage && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{nameMessage}</div>}
        {nameError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{nameError}</div>}
        <div className="stack">
          <label>Display name</label>
          <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Your name" />
          <button type="button" onClick={saveDisplayName} disabled={savingName || !displayName.trim()}>
            {savingName ? 'Saving...' : 'Save display name'}
          </button>
        </div>
      </div>

      <div className="card" style={{ marginTop: 12 }}>
        <h3>Change password</h3>
        {authMode !== 'password' && (
          <p className="muted">Password changes are available for email + MFA sign-in.</p>
        )}
        {passwordMessage && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{passwordMessage}</div>}
        {passwordError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{passwordError}</div>}
        <div className="stack">
          <label>Current password</label>
          <input value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} type="password" />
          <label>New password</label>
          <input value={newPassword} onChange={(e) => setNewPassword(e.target.value)} type="password" />
          <label>Confirm new password</label>
          <input value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} type="password" />
          <button type="button" onClick={changePassword} disabled={savingPassword || authMode !== 'password'}>
            {savingPassword ? 'Updating...' : 'Update password'}
          </button>
        </div>
      </div>

      <div className="card" style={{ marginTop: 12 }}>
        <h3>Two-factor authentication</h3>
        {authMode !== 'password' && (
          <p className="muted">MFA setup is available for email + MFA sign-in.</p>
        )}
        {mfaMessage && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{mfaMessage}</div>}
        {mfaError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{mfaError}</div>}
        <div className="stack">
          <button type="button" onClick={startMfa} disabled={mfaLoading || authMode !== 'password'}>
            {mfaLoading ? 'Working...' : user?.mfaEnabled ? 'Reset MFA' : 'Enable MFA'}
          </button>
          {setup?.secret && (
            <>
              <label>Secret</label>
              <input value={setup.secret} readOnly />
            </>
          )}
          {setup?.otpauthUrl && (
            <div style={{ marginTop: 8 }}>
              <strong>Scan QR</strong>
              <div style={{ marginTop: 8 }}>
                <img
                  src={`https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(setup.otpauthUrl)}`}
                  alt="MFA QR code"
                  style={{ background: '#fff', padding: 8, borderRadius: 8 }}
                />
              </div>
            </div>
          )}
          {setup && (
            <>
              <label>6-digit code</label>
              <input value={mfaCode} onChange={(e) => setMfaCode(e.target.value)} placeholder="123456" inputMode="numeric" />
              <button type="button" onClick={verifyMfa} disabled={mfaLoading || authMode !== 'password'}>
                {mfaLoading ? 'Verifying...' : 'Verify'}
              </button>
            </>
          )}
        </div>
      </div>

      <div className="card" style={{ marginTop: 12 }}>
        <h3>Microsoft account</h3>
        {authMode === 'password' && (
          <p className="muted">Sign in with Microsoft to link it to your account.</p>
        )}
        {swaPrincipal ? (
          <p className="muted">Connected as {swaPrincipal}.</p>
        ) : (
          <p className="muted">No Microsoft account connected.</p>
        )}
        <div className="stack">
          <a
            className="button"
            href="/.auth/login/aad?post_login_redirect_uri=/profile"
            style={{ textAlign: 'center', textDecoration: 'none' }}
          >
            Sign in with Microsoft
          </a>
        </div>
        {!isDev && (
          <div style={{ marginTop: 12 }}>
            <LinkLegacyAccount />
          </div>
        )}
      </div>
    </div>
  );
}
