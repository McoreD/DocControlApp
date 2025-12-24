import { useEffect, useState } from 'react';
import { MembersApi, ProjectsApi } from '../lib/api';
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
  const [inviteProjectId, setInviteProjectId] = useState<number | null>(null);
  const [inviteProjectName, setInviteProjectName] = useState<string | null>(null);
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteRole, setInviteRole] = useState('Viewer');
  const [inviteMessage, setInviteMessage] = useState<string | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteLoading, setInviteLoading] = useState(false);
  const [inviteLink, setInviteLink] = useState<string | null>(null);
  const [inviteToken, setInviteToken] = useState<string | null>(null);
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
                <th>Share</th>
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
                  <td>
                    <button
                      onClick={() => {
                        setInviteProjectId(p.id);
                        setInviteProjectName(p.name);
                        setInviteEmail('');
                        setInviteRole('Viewer');
                        setInviteError(null);
                        setInviteMessage(null);
                        setInviteLink(null);
                        setInviteToken(null);
                      }}
                    >
                      Share
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {inviteProjectId !== null && (
        <div className="card" style={{ marginTop: 16 }}>
          <h3>Share project</h3>
          <p className="muted">Invite collaborators to <strong>{inviteProjectName}</strong>.</p>
          <div className="grid" style={{ gridTemplateColumns: '2fr 1fr auto', gap: 8 }}>
            <input
              type="email"
              placeholder="Email"
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
            />
            <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value)}>
              <option value="Viewer">Viewer</option>
              <option value="Contributor">Contributor</option>
              <option value="Owner">Owner</option>
            </select>
            <button
              onClick={async () => {
                if (!inviteProjectId || !inviteEmail.trim()) return;
                setInviteLoading(true);
                setInviteError(null);
                setInviteMessage(null);
                setInviteLink(null);
                setInviteToken(null);
                try {
                  const result = await MembersApi.invite(inviteProjectId, inviteEmail.trim(), inviteRole);
                  const token = result?.token as string | undefined;
                  if (token) {
                    const link = `${window.location.origin}/members?inviteToken=${encodeURIComponent(token)}`;
                    setInviteLink(link);
                    setInviteToken(token);
                    setInviteMessage('Share this link with your teammate so they can accept the invite.');
                  } else {
                    setInviteMessage('Invite created. Share the token with your teammate.');
                  }
                } catch (err: any) {
                  setInviteError(err.message ?? 'Failed to send invite');
                } finally {
                  setInviteLoading(false);
                }
              }}
              disabled={inviteLoading || !inviteEmail.trim()}
            >
              {inviteLoading ? 'Working...' : 'Generate invite link'}
            </button>
          </div>
          {inviteLink && (
            <div className="pill" style={{ background: '#ecfdf3', color: '#166534', marginTop: 8 }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                <span>{inviteMessage ?? 'Share this invite link.'}</span>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <input
                    readOnly
                    value={inviteLink}
                    style={{ flex: 1, fontSize: 12 }}
                    onFocus={(e) => e.target.select()}
                  />
                  <button
                    onClick={async () => {
                      try {
                        await navigator.clipboard.writeText(inviteLink);
                        setInviteMessage('Copied! Share this link with your teammate.');
                      } catch {
                        setInviteMessage('Copy failed. Manually copy the link below.');
                      }
                    }}
                  >
                    Copy link
                  </button>
                </div>
                {inviteToken && (
                  <small style={{ color: '#166534' }}>Token: {inviteToken}</small>
                )}
              </div>
            </div>
          )}
          {inviteError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b', marginTop: 8 }}>{inviteError}</div>}
          {!inviteLink && inviteMessage && (
            <div className="pill" style={{ background: '#ecfdf3', color: '#166534', marginTop: 8 }}>{inviteMessage}</div>
          )}
        </div>
      )}
    </div>
  );
}
