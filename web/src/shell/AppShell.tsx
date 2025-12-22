import { NavLink, Outlet } from 'react-router-dom';

const links = [
  { to: '/projects', label: 'Projects' },
  { to: '/recommend', label: 'Recommend' },
  { to: '/generate', label: 'Generate' },
  { to: '/codes', label: 'Codes' },
  { to: '/documents', label: 'Documents' },
  { to: '/audit', label: 'Audit' },
  { to: '/import', label: 'Import' },
  { to: '/settings', label: 'Settings' },
];

export default function AppShell() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">DocControl</div>
        <nav className="nav">
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} className={({ isActive }) => (isActive ? 'active' : '')}>
              {l.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
