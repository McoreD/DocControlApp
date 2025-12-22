import { useEffect, useState } from 'react';
import { DocumentsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type Document = {
  id: number;
  fileName: string;
  level1: string;
  level2: string;
  level3: string;
  level4?: string | null;
  number: number;
  createdAtUtc: string;
  freeText?: string | null;
};

export default function Documents() {
  const { projectId } = useProject();
  const [docs, setDocs] = useState<Document[]>([]);
  const [q, setQ] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await DocumentsApi.list(projectId, q);
      setDocs(data);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load documents');
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
      <h1>Documents</h1>
      <p className="muted">List and filter documents per project. Filters: level1, level2, level3, q (file/free text).</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="row" style={{ marginBottom: 12 }}>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search free text or file name" />
        <button onClick={load} disabled={!projectId || loading}>
          {loading ? 'Loading...' : 'Search'}
        </button>
      </div>
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="card">
        {docs.length === 0 && !loading ? <p className="muted">No documents yet.</p> : null}
        {docs.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>File</th>
                <th>Code</th>
                <th>Free text</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {docs.map((d) => (
                <tr key={d.id}>
                  <td>{d.fileName}</td>
                  <td className="muted">
                    {[d.level1, d.level2, d.level3, d.level4].filter(Boolean).join('-')} ({d.number})
                  </td>
                  <td className="muted">{d.freeText ?? ''}</td>
                  <td className="muted">{new Date(d.createdAtUtc).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
