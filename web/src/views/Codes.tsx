import { useEffect, useState } from 'react';
import { CodesApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type CodeSeries = {
  id: number;
  key: { level1: string; level2: string; level3: string; level4?: string | null };
  description?: string | null;
  nextNumber: number;
};

export default function Codes() {
  const { projectId } = useProject();
  const [codes, setCodes] = useState<CodeSeries[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const load = async () => {
      if (!projectId) return;
      setLoading(true);
      setError(null);
      try {
        const data = await CodesApi.list(projectId);
        setCodes(data);
      } catch (err: any) {
        setError(err.message ?? 'Failed to load codes');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [projectId]);

  return (
    <div className="page">
      <h1>Codes</h1>
      <p className="muted">Import or manage code catalog per project. Use CSV format: Level,Code,Code Description.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="card">
        <strong>Sample rows</strong>
        <pre style={{ whiteSpace: 'pre-wrap' }}>
{`Level,Code,Code Description
1,DFT,Delpach Family Trust
2,GOV,Governance
3,REG,Registers`}
        </pre>
      </div>
      <p className="muted" style={{ marginTop: 12 }}>
        Endpoints: <code>/api/projects/{"{projectId}"}/codes</code> and <code>/api/projects/{"{projectId}"}/codes/import</code>.
      </p>
      {projectId && (
        <div className="card" style={{ marginTop: 12 }}>
          <strong>Existing codes</strong>
          {loading ? <p className="muted">Loading...</p> : null}
          {!loading && codes.length === 0 ? <p className="muted">No codes yet.</p> : null}
          {codes.length > 0 && (
            <table className="table">
              <thead>
                <tr>
                  <th>Level</th>
                  <th>Code</th>
                  <th>Description</th>
                </tr>
              </thead>
              <tbody>
                {codes.map((c) => {
                  const level = c.key.level4 ? 4 : c.key.level3 ? 3 : c.key.level2 ? 2 : 1;
                  const code =
                    level === 4 ? c.key.level4 :
                    level === 3 ? c.key.level3 :
                    level === 2 ? c.key.level2 :
                    c.key.level1;
                  return (
                    <tr key={c.id}>
                      <td>{level}</td>
                      <td>{code}</td>
                      <td className="muted">{c.description ?? ''}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
