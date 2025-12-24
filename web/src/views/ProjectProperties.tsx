import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { CodesApi, ProjectsApi } from '../lib/api';

type Project = {
  id: number;
  name: string;
  description: string;
  separator: string;
  paddingLength: number;
  levelCount: number;
  level1Label: string;
  level2Label: string;
  level3Label: string;
  level4Label: string;
  level5Label: string;
  level6Label: string;
  level1Length: number;
  level2Length: number;
  level3Length: number;
  level4Length: number;
  level5Length: number;
  level6Length: number;
  createdAtUtc: string;
};

type CodeSeries = {
  id: number;
  key: {
    level1: string;
    level2: string;
    level3: string;
    level4?: string | null;
    level5?: string | null;
    level6?: string | null;
  };
  description?: string | null;
  nextNumber: number;
};

export default function ProjectProperties() {
  const { projectId } = useParams();
  const navigate = useNavigate();
  const projectIdNum = projectId ? Number(projectId) : NaN;
  const [project, setProject] = useState<Project | null>(null);
  const [series, setSeries] = useState<CodeSeries[]>([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const levelItems = useMemo(() => {
    if (!project) return [];
    return [
      { label: project.level1Label, length: project.level1Length },
      { label: project.level2Label, length: project.level2Length },
      { label: project.level3Label, length: project.level3Length },
      { label: project.level4Label, length: project.level4Length },
      { label: project.level5Label, length: project.level5Length },
      { label: project.level6Label, length: project.level6Length },
    ].slice(0, project.levelCount);
  }, [project]);

  useEffect(() => {
    if (!Number.isFinite(projectIdNum)) {
      setError('Invalid project id.');
      return;
    }
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await ProjectsApi.get(projectIdNum);
        setProject(data);
        setName(data.name ?? '');
        setDescription(data.description ?? '');
        const dataSeries = await CodesApi.listSeries(projectIdNum);
        setSeries(dataSeries ?? []);
      } catch (err: any) {
        setError(err.message ?? 'Failed to load project');
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [projectIdNum]);

  const save = async () => {
    if (!project) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const updated = await ProjectsApi.update(project.id, name.trim(), description.trim());
      setProject(updated);
      setMessage('Saved');
    } catch (err: any) {
      setError(err.message ?? 'Failed to save project');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="page">
      <div className="row" style={{ alignItems: 'center', gap: 12 }}>
        <h1 style={{ margin: 0 }}>Project properties</h1>
        <button type="button" onClick={() => navigate('/projects')} style={{ background: '#334155', color: '#e2e8f0' }}>
          Back to projects
        </button>
      </div>

      {loading && <p className="muted">Loading...</p>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}

      {project && (
        <>
          <div className="grid" style={{ marginTop: 12 }}>
            <div className="card">
              <h3>Details</h3>
              <div className="stack">
                <label>Project name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} />
                <label>Description</label>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
                <label>Created</label>
                <input value={new Date(project.createdAtUtc).toLocaleString()} readOnly />
              </div>
            </div>
            <div className="card">
              <h3>Document settings</h3>
              <div className="stack">
                <label>Separator</label>
                <input value={project.separator} readOnly />
                <label>Padding length</label>
                <input value={project.paddingLength} readOnly />
                <label>Number of levels</label>
                <input value={project.levelCount} readOnly />
              </div>
              <div style={{ marginTop: 12 }}>
                <strong>Levels</strong>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 8, marginTop: 8 }}>
                  {levelItems.map((item, index) => (
                    <div key={`${item.label}-${index}`} style={{ display: 'contents' }}>
                      <div>{item.label}</div>
                      <div className="muted">{item.length} chars</div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>

          <div style={{ marginTop: 12 }}>
            <button onClick={save} disabled={saving || !name.trim()}>
              {saving ? 'Saving...' : 'Save changes'}
            </button>
          </div>

          <div className="card" style={{ marginTop: 16 }}>
            <h3>Code series ({series.length})</h3>
            {series.length === 0 ? (
              <p className="muted">No code series yet.</p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Combination</th>
                    <th>Next #</th>
                  </tr>
                </thead>
                <tbody>
                  {[...series].sort((a, b) => {
                    const comboA = [a.key.level1, a.key.level2, a.key.level3, a.key.level4, a.key.level5, a.key.level6]
                      .filter((p) => p && p.length > 0)
                      .join('-');
                    const comboB = [b.key.level1, b.key.level2, b.key.level3, b.key.level4, b.key.level5, b.key.level6]
                      .filter((p) => p && p.length > 0)
                      .join('-');
                    return comboA.localeCompare(comboB, undefined, { sensitivity: 'base' });
                  }).map((s) => {
                    const parts = [
                      s.key.level1,
                      s.key.level2,
                      s.key.level3,
                      s.key.level4,
                      s.key.level5,
                      s.key.level6,
                    ].filter((p) => p && p.length > 0);
                    return (
                      <tr key={s.id}>
                        <td>{parts.join('-')}</td>
                        <td className="muted">{s.nextNumber}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
