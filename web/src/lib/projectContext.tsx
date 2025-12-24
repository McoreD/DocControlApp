import { createContext, useContext, useEffect, useRef, useState } from 'react';
import { ProjectsApi } from './api';
import { useAuth } from './authContext';

type ProjectContextValue = {
  projectId: number | null;
  projectName: string | null;
  defaultProjectId: number | null;
  setProjectId: (id: number | null, name?: string | null) => void;
  setDefaultProjectId: (id: number, name?: string | null) => Promise<void>;
};

const ProjectContext = createContext<ProjectContextValue | undefined>(undefined);

export function ProjectProvider({ children }: { children: React.ReactNode }) {
  const { user, ready } = useAuth();
  const [projectId, setProjectIdState] = useState<number | null>(null);
  const [projectName, setProjectNameState] = useState<string | null>(null);
  const [defaultProjectId, setDefaultProjectIdState] = useState<number | null>(null);
  const defaultLoadedForUser = useRef<number | null>(null);

  useEffect(() => {
    if (!user) {
      setDefaultProjectIdState(null);
      defaultLoadedForUser.current = null;
      return;
    }
    setDefaultProjectIdState(null);
    defaultLoadedForUser.current = null;
  }, [user?.id]);

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

  useEffect(() => {
    if (!ready || !user) return;
    if (defaultLoadedForUser.current === user.id) return;
    defaultLoadedForUser.current = user.id;
    let cancelled = false;

    (async () => {
      try {
        const projects = await ProjectsApi.list();
        if (cancelled) return;
        const defaultProject = projects.find((p) => p.isDefault);
        const nextDefaultId = defaultProject?.id ?? null;
        setDefaultProjectIdState(nextDefaultId);
        if (nextDefaultId && projectId !== nextDefaultId) {
          setProjectId(nextDefaultId, defaultProject?.name ?? null);
        }
      } catch {
        if (!cancelled) {
          setDefaultProjectIdState(null);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [projectId, ready, user]);

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

  const setDefaultProjectId = async (id: number, name?: string | null) => {
    await ProjectsApi.setDefault(id);
    setDefaultProjectIdState(id);
    if (!projectId) {
      setProjectId(id, name ?? null);
    }
  };

  return (
    <ProjectContext.Provider value={{ projectId, projectName, defaultProjectId, setProjectId, setDefaultProjectId }}>
      {children}
    </ProjectContext.Provider>
  );
}

export function useProject() {
  const ctx = useContext(ProjectContext);
  if (!ctx) throw new Error('useProject must be used within ProjectProvider');
  return ctx;
}
