import { useState } from 'react';
import { CodesApi, DocumentsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

export default function Management() {
  const { projectId } = useProject();
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const purgeDocuments = async () => {
    if (!projectId) return;
    const confirmed = window.confirm('This will permanently delete all documents in this project. Continue?');
    if (!confirmed) return;
    setLoading(true);
    setMessage(null);
    setError(null);
    try {
      const result = await DocumentsApi.purge(projectId);
      setMessage(`Deleted ${result.deleted} document(s).`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to purge documents');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Management</h1>
      <p className="muted">Administrative actions for the current project.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="card" style={{ marginTop: 12 }}>
        <strong>Documents</strong>
        <p className="muted" style={{ margin: '8px 0' }}>
          Purge removes every document in this project. Codes are left untouched.
        </p>
        <button onClick={purgeDocuments} disabled={!projectId || loading} style={{ background: '#b91c1c' }}>
          {loading ? 'Purging...' : 'Purge documents'}
        </button>
      </div>
      <div className="card" style={{ marginTop: 12 }}>
        <strong>Codes</strong>
        <p className="muted" style={{ margin: '8px 0' }}>
          Purge removes all codes and any documents linked to them for this project.
        </p>
        <button
          onClick={async () => {
            if (!projectId) return;
            const confirmed = window.confirm('Permanently delete all codes (and their documents). Continue?');
            if (!confirmed) return;
            setLoading(true);
            setMessage(null);
            setError(null);
            try {
              const result = await CodesApi.purge(projectId);
              setMessage(`Deleted ${result.deletedCodes} code(s) and ${result.deletedDocuments} document(s).`);
            } catch (err: any) {
              setError(err.message ?? 'Failed to purge codes');
            } finally {
              setLoading(false);
            }
          }}
          disabled={!projectId || loading}
          style={{ background: '#b91c1c' }}
        >
          {loading ? 'Purging...' : 'Purge codes'}
        </button>
      </div>
    </div>
  );
}
