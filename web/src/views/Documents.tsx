import { useEffect, useState } from 'react';
import { DocumentsApi, ProjectsApi } from '../lib/api';
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

type ProjectConfig = {
  separator: string;
  paddingLength: number;
  levelCount: number;
};

export default function Documents() {
  const { projectId } = useProject();
  const [docs, setDocs] = useState<Document[]>([]);
  const [projectConfig, setProjectConfig] = useState<ProjectConfig | null>(null);
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [sortKey, setSortKey] = useState<'code' | 'freeText' | 'createdAtUtc'>('createdAtUtc');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const pageSize = 50;

  const load = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const skip = (page - 1) * pageSize;
      const data = await DocumentsApi.list(projectId, { q, take: pageSize, skip });
      setDocs(data.items ?? []);
      setTotal(data.total ?? 0);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load documents');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId, page]);

  useEffect(() => {
    if (!projectId) {
      setProjectConfig(null);
      return;
    }
    const loadProject = async () => {
      try {
        const project = await ProjectsApi.get(projectId);
        setProjectConfig({
          separator: project.separator ?? '-',
          paddingLength: project.paddingLength ?? 3,
          levelCount: project.levelCount ?? 3,
        });
      } catch (err: any) {
        setError(err.message ?? 'Failed to load project configuration');
      }
    };
    loadProject();
  }, [projectId]);

  const buildCode = (doc: Document) => {
    const separator = projectConfig?.separator?.length ? projectConfig.separator : '-';
    const padding = projectConfig?.paddingLength && projectConfig.paddingLength > 0 ? projectConfig.paddingLength : 3;
    const levelCount = Math.min(Math.max(projectConfig?.levelCount ?? 3, 1), 6);
    const parts: string[] = [];
    if (levelCount >= 1) parts.push(doc.level1);
    if (levelCount >= 2) parts.push(doc.level2);
    if (levelCount >= 3) parts.push(doc.level3);
    if (levelCount >= 4 && doc.level4) parts.push(doc.level4);
    if (levelCount >= 5 && doc.level5) parts.push(doc.level5);
    if (levelCount >= 6 && doc.level6) parts.push(doc.level6);
    const number = String(doc.number ?? 0).padStart(padding, '0');
    parts.push(number);
    return parts.join(separator);
  };

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
    const aCode = buildCode(a).toLowerCase();
    const bCode = buildCode(b).toLowerCase();
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
      <h1>Documents ({total})</h1>
      <p className="muted">List and filter documents per project. Filters: level1, level2, level3, q (file/free text).</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="row" style={{ marginBottom: 12, alignItems: 'center' }}>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search free text or file name" />
        <button
          onClick={() => {
            setPage(1);
            load();
          }}
          disabled={!projectId || loading}
        >
          {loading ? 'Loading...' : 'Search'}
        </button>
        <button
          onClick={() => {
            setQ('');
            setPage(1);
            load();
          }}
          disabled={!projectId || loading}
          style={{ background: '#334155', color: '#e2e8f0' }}
        >
          Clear
        </button>
      </div>
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      {total > 0 && (
        <div className="row" style={{ gap: 8, alignItems: 'center', marginBottom: 8 }}>
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1 || loading}>
            Previous
          </button>
          <div className="muted">
            Page {page} of {Math.max(1, Math.ceil(total / pageSize))}
          </div>
          <button
            onClick={() => setPage((p) => p + 1)}
            disabled={page >= Math.ceil(total / pageSize) || loading}
          >
            Next
          </button>
        </div>
      )}
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
                const code = buildCode(d);
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
