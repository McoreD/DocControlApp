import { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { useNavigate } from 'react-router-dom';
import { AuthApi } from '../lib/api';
import { useAuth } from '../lib/authContext';

type SetupState = { secret: string; otpauthUrl: string } | null;

export default function MfaSetup() {
  const { user, setUser } = useAuth();
  const navigate = useNavigate();
  const [setup, setSetup] = useState<SetupState>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const init = async () => {
      try {
        const result = await AuthApi.startMfa();
        setSetup(result);
      } catch (err: any) {
        setError(err.message ?? 'Failed to start MFA');
      }
    };
    if (user && !user.mfaEnabled) {
      void init();
    } else if (user?.mfaEnabled) {
      navigate('/projects', { replace: true });
    }
  }, [user, navigate]);

  useEffect(() => {
    const buildQr = async () => {
      if (!setup?.otpauthUrl) {
        setQrDataUrl(null);
        return;
      }
      try {
        const dataUrl = await QRCode.toDataURL(setup.otpauthUrl, { margin: 1, width: 220 });
        setQrDataUrl(dataUrl);
      } catch {
        setQrDataUrl(null);
      }
    };
    void buildQr();
  }, [setup?.otpauthUrl]);

  const verify = async () => {
    if (!code.trim()) {
      setError('Enter the 6-digit code from your authenticator app.');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const result = await AuthApi.verifyMfa(code.trim());
      if (result.mfaEnabled && user) {
        setUser({ ...user, mfaEnabled: true });
        navigate('/projects', { replace: true });
      }
    } catch (err: any) {
      setError(err.message ?? 'Invalid code');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page" style={{ maxWidth: 720, margin: '40px auto' }}>
      <h1>Set up two-factor authentication</h1>
      <p className="muted">Scan the code or enter the secret, then confirm with a 6-digit code.</p>

      <div className="card stack" style={{ marginTop: 12 }}>
        <label>Secret</label>
        <input value={setup?.secret ?? ''} readOnly />
        {setup?.otpauthUrl && (
          <>
            <a href={setup.otpauthUrl} target="_blank" rel="noreferrer">
              Open in authenticator
            </a>
            <div style={{ marginTop: 12 }}>
              <strong>Scan QR</strong>
              <div style={{ marginTop: 8 }}>
                {qrDataUrl ? (
                  <img
                    src={qrDataUrl}
                    alt="MFA QR code"
                    style={{ background: '#fff', padding: 8, borderRadius: 8 }}
                  />
                ) : (
                  <div className="muted">QR code unavailable.</div>
                )}
              </div>
            </div>
          </>
        )}

        <label>6-digit code</label>
        <input value={code} onChange={(e) => setCode(e.target.value)} placeholder="123456" inputMode="numeric" />

        <button type="button" onClick={verify} disabled={loading}>
          {loading ? 'Verifying...' : 'Verify & continue'}
        </button>

        {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      </div>
    </div>
  );
}
