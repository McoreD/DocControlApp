import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { MembersApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type Member = {
  projectId: number;
  userId: number;
  role: string;
  addedAtUtc: string;
};

export default function Members() {
  const { projectId } = useProject();
  const [members, setMembers] = useState<Member[]>([]);
  const [pending, setPending] = useState<any[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingPending, setLoadingPending] = useState(false);
  const [inviteToken, setInviteToken] = useState('');
  const [searchParams, setSearchParams] = useSearchParams();

  const load = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await MembersApi.list(projectId);
      setMembers(data);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load members');
    } finally {
      setLoading(false);
    }
  };

  const loadPending = async () => {
    if (!projectId) return;
    setLoadingPending(true);
    try {
      const invites = await MembersApi.pendingInvites(projectId);
      setPending(invites);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load pending invites');
    } finally {
      setLoadingPending(false);
    }
  };

  useEffect(() => {
    load();
    loadPending();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  useEffect(() => {
    const token = searchParams.get('inviteToken');
    if (token) {
      setInviteToken(token);
      setMessage('Invite token prefilled from shared link. Sign in and accept to join the project.');
      const params = new URLSearchParams(searchParams);
      params.delete('inviteToken');
      setSearchParams(params);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const changeRole = async (userId: number, newRole: string) => {
    if (!projectId) return;
    setError(null);
    setMessage(null);
    try {
      await MembersApi.changeRole(projectId, userId, newRole);
      setMessage('Role updated.');
      await load();
    } catch (err: any) {
      setError(err.message ?? 'Change role failed');
    }
  };

  const remove = async (userId: number) => {
    if (!projectId) return;
    setError(null);
    setMessage(null);
    try {
      await MembersApi.remove(projectId, userId);
      setMessage('Member removed.');
      await load();
    } catch (err: any) {
      setError(err.message ?? 'Remove failed');
    }
  };

  const accept = async () => {
    if (!inviteToken.trim()) return;
    setError(null);
    setMessage(null);
    try {
      await MembersApi.accept(inviteToken.trim());
      setMessage('Invite accepted.');
      setInviteToken('');
      await load();
      await loadPending();
    } catch (err: any) {
      setError(err.message ?? 'Accept invite failed');
    }
  };

  return (
    <div className="page">
      <h1>Members</h1>
      <p className="muted">Manage project members. Invites are sent from the Projects page; paste tokens here to accept.</p>
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <>
        <div className="card">
          <strong>Accept invite</strong>
          <div className="stack">
            <label>Invite token (paste from shared link)</label>
            <input value={inviteToken} onChange={(e) => setInviteToken(e.target.value)} placeholder="Paste token" />
            <button onClick={accept} disabled={loading}>
              {loading ? 'Working...' : 'Accept'}
            </button>
            </div>
          </div>

          <div className="card" style={{ marginTop: 12 }}>
            <strong>Pending invitations</strong>
            {!projectId && <p className="muted">Select a project to view its pending invites.</p>}
            {loadingPending ? <p className="muted">Loading...</p> : null}
            {!loadingPending && pending.length === 0 ? <p className="muted">No pending invites.</p> : null}
            {pending.length > 0 && (
              <table className="table">
                <thead>
                  <tr>
                    <th>Email</th>
                    <th>Role</th>
                    <th>Expires</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {pending.map((p) => (
                    <tr key={p.id}>
                      <td>{p.email}</td>
                      <td>{p.role}</td>
                      <td className="muted">{new Date(p.expiresAtUtc).toLocaleString()}</td>
                      <td>
                        <button
                          onClick={async () => {
                            if (!p.token) {
                              setError('No invite token available to copy.');
                              return;
                            }
                            try {
                              const url = `${window.location.origin}/members?inviteToken=${encodeURIComponent(p.token)}`;
                              await navigator.clipboard.writeText(url);
                              setMessage('Invite link copied.');
                            } catch {
                              setError('Failed to copy invite link.');
                            }
                          }}
                          style={{ marginRight: 8 }}
                        >
                          Copy link
                        </button>
                        <button
                          onClick={async () => {
                            if (!projectId) return;
                            setError(null);
                            setMessage(null);
                            try {
                              await MembersApi.cancelInvite(projectId, p.id);
                              setMessage('Invite cancelled.');
                              await loadPending();
                            } catch (err: any) {
                              setError(err.message ?? 'Cancel failed');
                            }
                          }}
                          style={{ background: '#b91c1c', color: '#f8fafc' }}
                        >
                          Cancel
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="card" style={{ marginTop: 12 }}>
            <strong>Members</strong>
            {!projectId && <p className="muted">Select a project to view its members.</p>}
            <p className="muted">Members for {projectName ?? 'this project'}.</p>
            {loading ? <p className="muted">Loading...</p> : null}
            {!loading && members.length === 0 ? <p className="muted">No members yet.</p> : null}
            {members.length > 0 && (
              <table className="table">
                <thead>
                  <tr>
                    <th>User</th>
                    <th>Role</th>
                    <th>Added</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {members.map((m) => (
                    <tr key={m.userId}>
                      <td>{m.userId}</td>
                      <td>
                        <select value={m.role} onChange={(e) => changeRole(m.userId, e.target.value)}>
                          <option value="Owner">Owner</option>
                          <option value="Contributor">Contributor</option>
                          <option value="Viewer">Viewer</option>
                        </select>
                      </td>
                      <td className="muted">{new Date(m.addedAtUtc).toLocaleString()}</td>
                      <td>
                        {members.length > 1 && (
                          <button onClick={() => remove(m.userId)} style={{ background: '#ef4444', color: '#f8fafc' }}>
                            Remove
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
      </>
    </div>
  );
}
