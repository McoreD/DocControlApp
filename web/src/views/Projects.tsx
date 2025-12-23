import { useEffect, useState } from 'react';
import { ProjectsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type Project = {
  id: number;
  name: string;
  description: string;
  createdAtUtc: string;
};

export default function Projects() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { projectId, setProjectId } = useProject();

  const load = async () => {
    setError(null);
    setLoading(true);
    try {
      const data = await ProjectsApi.list();
      setProjects(data);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load projects');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const create = async () => {
    if (!name.trim()) return;
    setLoading(true);
    setError(null);
    try {
      await ProjectsApi.create(name.trim(), description.trim());
      setName('');
      setDescription('');
      await load();
    } catch (err: any) {
      setError(err.message ?? 'Failed to create project');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Projects</h1>
      <p className="muted">Create and manage projects you can access.</p>

      <div className="grid" style={{ marginTop: 12 }}>
        <div className="card">
          <div className="stack">
            <label>Project name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Project name" />
            <label>Description</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="What is this project about?"
              rows={3}
            />
            <button onClick={create} disabled={loading}>
              {loading ? 'Working...' : 'Create project'}
            </button>
            {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3>Accessible Projects</h3>
        {loading && projects.length === 0 ? <p className="muted">Loading...</p> : null}
        {projects.length === 0 && !loading ? <p className="muted">No projects yet.</p> : null}
        {projects.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Created</th>
                <th>Active</th>
              </tr>
            </thead>
            <tbody>
              {projects.map((p) => (
                <tr key={p.id}>
                  <td>{p.name}</td>
                  <td className="muted">{p.description}</td>
                  <td className="muted">{new Date(p.createdAtUtc).toLocaleString()}</td>
                  <td>
                    <button onClick={() => setProjectId(p.id, p.name)} disabled={projectId === p.id}>
                      {projectId === p.id ? 'Selected' : 'Select'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
