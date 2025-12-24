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
  level5?: string | null;
  level6?: string | null;
  number: number;
  createdAtUtc: string;
  freeText?: string | null;
};

export default function Documents() {
  const { projectId } = useProject();
  const [docs, setDocs] = useState<Document[]>([]);
  const [q, setQ] = useState('');
  const [sortKey, setSortKey] = useState<'code' | 'freeText' | 'createdAtUtc'>('createdAtUtc');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');
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

  const sortedDocs = [...docs].sort((a, b) => {
    if (sortKey === 'createdAtUtc') {
      const aTime = new Date(a.createdAtUtc).getTime();
      const bTime = new Date(b.createdAtUtc).getTime();
      return sortDir === 'asc' ? aTime - bTime : bTime - aTime;
    }
    if (sortKey === 'freeText') {
      const aText = (a.freeText ?? '').toLowerCase();
      const bText = (b.freeText ?? '').toLowerCase();
      return sortDir === 'asc' ? aText.localeCompare(bText) : bText.localeCompare(aText);
    }
    const aCode = (a.fileName ?? '').split(' ')[0] ?? '';
    const bCode = (b.fileName ?? '').split(' ')[0] ?? '';
    return sortDir === 'asc' ? aCode.localeCompare(bCode) : bCode.localeCompare(aCode);
  });

  const toggleSort = (key: 'code' | 'freeText' | 'createdAtUtc') => {
    if (sortKey === key) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
      return;
    }
    setSortKey(key);
    setSortDir('asc');
  };

  return (
    <div className="page">
      <h1>Documents ({docs.length})</h1>
      <p className="muted">List and filter documents per project. Filters: level1, level2, level3, q (file/free text).</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="row" style={{ marginBottom: 12, alignItems: 'center' }}>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search free text or file name" />
        <button onClick={load} disabled={!projectId || loading}>
          {loading ? 'Loading...' : 'Search'}
        </button>
        <button
          onClick={() => {
            setQ('');
            load();
          }}
          disabled={!projectId || loading}
          style={{ background: '#334155', color: '#e2e8f0' }}
        >
          Clear
        </button>
      </div>
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="card">
        {docs.length === 0 && !loading ? <p className="muted">No documents yet.</p> : null}
        {docs.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>
                  <button type="button" className="link" onClick={() => toggleSort('code')}>
                    Code {sortKey === 'code' ? (sortDir === 'asc' ? '▲' : '▼') : ''}
                  </button>
                </th>
                <th>
                  <button type="button" className="link" onClick={() => toggleSort('freeText')}>
                    Free text {sortKey === 'freeText' ? (sortDir === 'asc' ? '▲' : '▼') : ''}
                  </button>
                </th>
                <th>
                  <button type="button" className="link" onClick={() => toggleSort('createdAtUtc')}>
                    Created {sortKey === 'createdAtUtc' ? (sortDir === 'asc' ? '▲' : '▼') : ''}
                  </button>
                </th>
              </tr>
            </thead>
            <tbody>
              {sortedDocs.map((d) => {
                const code = (d.fileName ?? '').split(' ')[0] ?? '';
                return (
                  <tr key={d.id}>
                    <td className="muted">{code}</td>
                    <td className="muted">{d.freeText ?? ''}</td>
                    <td className="muted">{new Date(d.createdAtUtc).toLocaleString()}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
