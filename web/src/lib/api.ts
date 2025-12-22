export type HttpMethod = 'GET' | 'POST' | 'DELETE';

const API_BASE = import.meta.env.VITE_API_BASE ?? '/api';

const defaultHeaders = () => ({
  'Content-Type': 'application/json',
  // Dev stub auth: replace with real token later.
  'x-user-id': localStorage.getItem('dc.userId') ?? '1',
  'x-user-email': localStorage.getItem('dc.email') ?? 'owner@example.com',
  'x-user-name': localStorage.getItem('dc.name') ?? 'Owner User',
});

export async function api<T>(path: string, method: HttpMethod = 'GET', body?: unknown): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers: defaultHeaders(),
    body: body ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`${res.status} ${res.statusText}: ${text}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const ProjectsApi = {
  list: () => api<any[]>('/projects'),
  create: (name: string, description: string) => api<any>('/projects', 'POST', { name, description }),
};

export const CodesApi = {
  list: (projectId: number) => api<any[]>(`/projects/${projectId}/codes`),
  importCsv: (projectId: number, csv: string) => api<any>(`/projects/${projectId}/codes/import`, 'POST', csv),
};

export const MembersApi = {
  list: (projectId: number) => api<any[]>(`/projects/${projectId}/members`),
  invite: (projectId: number, email: string, role: string, daysValid = 7) =>
    api<any>(`/projects/${projectId}/invites`, 'POST', { email, role, daysValid }),
  remove: (projectId: number, userId: number) => api<any>(`/projects/${projectId}/members/${userId}`, 'DELETE'),
  changeRole: (projectId: number, userId: number, role: string) =>
    api<any>(`/projects/${projectId}/members/${userId}/role`, 'POST', { role }),
};

export const DocumentsApi = {
  list: (projectId: number, q?: string) =>
    api<any[]>(`/projects/${projectId}/documents${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  create: (
    projectId: number,
    payload: { level1: string; level2: string; level3: string; level4?: string; freeText?: string; extension?: string },
  ) => api<any>(`/projects/${projectId}/documents`, 'POST', payload),
  importSimple: (
    projectId: number,
    entries: { code: string; fileName?: string; freeText?: string; description?: string }[],
  ) => api<any>(`/projects/${projectId}/documents/import`, 'POST', { entries }),
};

export const AuditApi = {
  list: (projectId: number, take = 50) => api<any[]>(`/projects/${projectId}/audit?take=${take}`),
};

export const SettingsApi = {
  get: (projectId: number) => api<any>(`/projects/${projectId}/settings`),
  save: (projectId: number, payload: any) => api<any>(`/projects/${projectId}/settings`, 'POST', payload),
};

export const AiApi = {
  interpret: (projectId: number, query: string) =>
    api<any>(`/projects/${projectId}/ai/interpret`, 'POST', { query }),
  recommend: (projectId: number, query: string) =>
    api<any>(`/projects/${projectId}/ai/recommend`, 'POST', { query }),
};
