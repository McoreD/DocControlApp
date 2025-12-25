import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../lib/authContext';
import { useProject } from '../lib/projectContext';

const links = [
  { to: '/generate', label: 'Generate' }, // Most used daily
  { to: '/documents', label: 'Documents' },
  { to: '/projects', label: 'Projects' },
  { to: '/codes', label: 'Codes' },
  { to: '/audit', label: 'Audit' },
  { to: '/members', label: 'Members' },
  { to: '/management', label: 'Management' },
  { to: '/profile', label: 'Profile' },
  { to: '/settings', label: 'Settings' },
];

export default function AppShell() {
  const navigate = useNavigate();
  const { user, clearUser } = useAuth();
  const { projectId, projectName, setProjectId } = useProject();
  const appVersion = '1.0.0';
  const isDev = import.meta.env.DEV;

  const signOut = () => {
    setProjectId(null);
    clearUser();
    localStorage.removeItem('dc.authToken');
    localStorage.removeItem('dc.authMode');
    if (isDev) {
      navigate('/register');
      return;
    }
    window.location.href = '/.auth/logout?post_logout_redirect_uri=/';
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
        <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: 12, color: '#94a3b8', marginBottom: 8 }}>
          v{appVersion}
        </div>
        <Outlet />
      </main>
    </div>
  );
}
