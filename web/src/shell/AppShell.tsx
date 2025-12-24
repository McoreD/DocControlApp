import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/authContext';
import { useProject } from '../lib/projectContext';

const links = [
  { to: '/projects', label: 'Projects' },
  { to: '/generate', label: 'Generate' },
  { to: '/members', label: 'Members' },
  { to: '/codes', label: 'Codes' },
  { to: '/documents', label: 'Documents' },
  { to: '/audit', label: 'Audit' },
  { to: '/import', label: 'Import' },
  { to: '/management', label: 'Management' },
  { to: '/settings', label: 'Settings' },
];

export default function AppShell() {
  const navigate = useNavigate();
  const { user, clearUser } = useAuth();
  const { projectId, projectName, setProjectId } = useProject();

  const signOut = () => {
    setProjectId(null);
    clearUser();
    navigate('/register');
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">DocControl</div>
        {user && (
          <div className="muted" style={{ fontSize: 12, lineHeight: 1.4 }}>
            Signed in as
            <div style={{ color: '#e2e8f0' }}>{user.name || user.email}</div>
            <div>{user.email}</div>
          </div>
        )}
        <div className="muted" style={{ fontSize: 12 }}>
          {projectId ? `Active project: ${projectName ?? `Project ${projectId}`}` : 'No project selected'}
        </div>
        <nav className="nav">
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} className={({ isActive }) => (isActive ? 'active' : '')}>
              {l.label}
            </NavLink>
          ))}
        </nav>
        <button
          type="button"
          onClick={signOut}
          style={{
            marginTop: 'auto',
            background: 'none',
            border: '1px solid #1f2937',
            color: '#e2e8f0',
          }}
        >
          Sign out
        </button>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
