import { createBrowserRouter, Navigate } from 'react-router-dom';
import AppShell from './shell/AppShell';
import RequireAuth from './shell/RequireAuth';
import Register from './views/Register';
import Projects from './views/Projects';
import Generate from './views/Generate';
import Codes from './views/Codes';
import Documents from './views/Documents';
import Audit from './views/Audit';
import Management from './views/Management';
import Settings from './views/Settings';
import Members from './views/Members';
import NotFound from './views/NotFound';
import MfaSetup from './views/MfaSetup';

const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/projects" replace /> },
      { path: 'projects', element: <Projects /> },
      { path: 'generate', element: <Generate /> },
      { path: 'members', element: <Members /> },
      { path: 'codes', element: <Codes /> },
      { path: 'documents', element: <Documents /> },
      { path: 'audit', element: <Audit /> },
      { path: 'management', element: <Management /> },
      { path: 'settings', element: <Settings /> },
      { path: 'mfa', element: <MfaSetup /> },
      { path: '*', element: <NotFound /> },
    ],
  },
  { path: '/register', element: <Register /> },
]);

export default router;
