import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';
import './index.css';
import router from './router';
import { ProjectProvider } from './lib/projectContext';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ProjectProvider>
      <RouterProvider router={router} />
    </ProjectProvider>
  </StrictMode>,
);
