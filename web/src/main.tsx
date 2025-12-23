import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';
import './index.css';
import router from './router';
import { AuthProvider } from './lib/authContext';
import { ProjectProvider } from './lib/projectContext';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <ProjectProvider>
        <RouterProvider router={router} />
      </ProjectProvider>
    </AuthProvider>
  </StrictMode>,
);
