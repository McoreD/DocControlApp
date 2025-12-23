export type HttpMethod = 'GET' | 'POST' | 'DELETE';

export type RegisteredUser = { id: number; email: string; displayName: string; createdAtUtc: string };

const API_BASE = import.meta.env.VITE_API_BASE ?? '/api';

const defaultHeaders = () => {
  const userId = localStorage.getItem('dc.userId');
  const email = localStorage.getItem('dc.email');
  const name = localStorage.getItem('dc.name');

  if (!userId || Number.isNaN(Number(userId)) || Number(userId) <= 0) {
    throw new Error('User not registered. Please sign up first.');
  }

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'x-user-id': userId,
  };

  if (email) headers['x-user-email'] = email;
  if (name) headers['x-user-name'] = name;
  return headers;
};

export async function api<T>(path: string, method: HttpMethod = 'GET', body?: unknown, opts?: { skipAuth?: boolean }): Promise<T> {
  const headers = opts?.skipAuth ? { 'Content-Type': 'application/json' } : defaultHeaders();

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
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

export const AuthApi = {
  register: (email: string, displayName: string) =>
    api<RegisteredUser>('/auth/register', 'POST', { email, displayName }, { skipAuth: true }),
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
  accept: (token: string) => api<any>(`/invites/accept`, 'POST', { token }),
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
