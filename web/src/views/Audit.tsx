import { useEffect, useState } from 'react';
import { AuditApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type AuditEntry = {
  id: number;
  action: string;
  payload?: string | null;
  createdAtUtc: string;
  createdByUserId: number;
};

export default function Audit() {
  const { projectId } = useProject();
  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await AuditApi.list(projectId);
      setEntries(data);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load audit');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  return (
    <div className="page">
      <h1>Audit</h1>
      <p className="muted">
        Paged audit trail per project. Endpoint: <code>/api/projects/{"{projectId}"}/audit</code>
      </p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="card">
        {loading ? <p className="muted">Loading...</p> : null}
        {!loading && entries.length === 0 ? <p className="muted">No audit entries.</p> : null}
        {entries.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>Action</th>
                <th>Payload</th>
                <th>User</th>
                <th>When</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((e) => (
                <tr key={e.id}>
                  <td>{e.action}</td>
                  <td className="muted">{e.payload ?? ''}</td>
                  <td className="muted">{e.createdByUserId}</td>
                  <td className="muted">{new Date(e.createdAtUtc).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
