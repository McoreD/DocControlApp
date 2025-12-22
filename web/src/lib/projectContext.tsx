import { createContext, useContext, useEffect, useState } from 'react';

type ProjectContextValue = {
  projectId: number | null;
  setProjectId: (id: number | null) => void;
};

const ProjectContext = createContext<ProjectContextValue | undefined>(undefined);

export function ProjectProvider({ children }: { children: React.ReactNode }) {
  const [projectId, setProjectIdState] = useState<number | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem('dc.projectId');
    if (stored) {
      const n = Number(stored);
      if (!Number.isNaN(n)) setProjectIdState(n);
    }
  }, []);

  const setProjectId = (id: number | null) => {
    setProjectIdState(id);
    if (id === null) {
      localStorage.removeItem('dc.projectId');
    } else {
      localStorage.setItem('dc.projectId', id.toString());
    }
  };

  return <ProjectContext.Provider value={{ projectId, setProjectId }}>{children}</ProjectContext.Provider>;
}

export function useProject() {
  const ctx = useContext(ProjectContext);
  if (!ctx) throw new Error('useProject must be used within ProjectProvider');
  return ctx;
}
