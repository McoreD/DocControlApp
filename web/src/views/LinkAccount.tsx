import LinkLegacyAccount from '../components/LinkLegacyAccount';

export default function LinkAccount() {
  const isDev = import.meta.env.DEV;

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
        <LinkLegacyAccount />
      </div>
    </div>
  );
}
