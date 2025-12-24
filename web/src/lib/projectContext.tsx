import { createContext, useContext, useEffect, useState } from 'react';
import { ProjectsApi } from './api';

type ProjectContextValue = {
  projectId: number | null;
  projectName: string | null;
  setProjectId: (id: number | null, name?: string | null) => void;
};

const ProjectContext = createContext<ProjectContextValue | undefined>(undefined);

export function ProjectProvider({ children }: { children: React.ReactNode }) {
  const [projectId, setProjectIdState] = useState<number | null>(null);
  const [projectName, setProjectNameState] = useState<string | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem('dc.projectId');
    const storedName = localStorage.getItem('dc.projectName');
    if (stored) {
      const n = Number(stored);
      if (!Number.isNaN(n)) setProjectIdState(n);
    }
    if (storedName) {
      setProjectNameState(storedName);
    }
  }, []);

  // Refresh the project name from the API when we have an id but no friendly name.
  useEffect(() => {
    let cancelled = false;
    const shouldFetch = projectId !== null && (!projectName || projectName === `Project ${projectId}`);
    if (!shouldFetch) return;

    (async () => {
      try {
        const project = await ProjectsApi.get(projectId!);
        if (!cancelled && project?.name) {
          setProjectNameState(project.name);
          localStorage.setItem('dc.projectName', project.name);
        }
      } catch {
        // Ignore fetch errors; keep fallback title.
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [projectId, projectName]);

  useEffect(() => {
    const titleSuffix = projectId ? projectName ?? `Project ${projectId}` : null;
    document.title = titleSuffix ? `DocControl - ${titleSuffix}` : 'DocControl';
  }, [projectId, projectName]);

  const setProjectId = (id: number | null, name?: string | null) => {
    const nextName = name ?? projectName ?? (id ? `Project ${id}` : null);
    setProjectIdState(id);
    setProjectNameState(nextName);
    if (id === null) {
      localStorage.removeItem('dc.projectId');
      localStorage.removeItem('dc.projectName');
    } else {
      localStorage.setItem('dc.projectId', id.toString());
      if (nextName) {
        localStorage.setItem('dc.projectName', nextName);
      } else {
        localStorage.removeItem('dc.projectName');
      }
    }
  };

  return <ProjectContext.Provider value={{ projectId, projectName, setProjectId }}>{children}</ProjectContext.Provider>;
}

export function useProject() {
  const ctx = useContext(ProjectContext);
  if (!ctx) throw new Error('useProject must be used within ProjectProvider');
  return ctx;
}
