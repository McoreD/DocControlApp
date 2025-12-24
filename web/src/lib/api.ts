export type HttpMethod = 'GET' | 'POST' | 'DELETE';

export type RegisteredUser = { id: number; email: string; displayName: string; createdAtUtc: string; mfaEnabled: boolean };
export type CurrentUser = { userId: number; email: string; displayName: string; mfaEnabled: boolean };

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
  get: (projectId: number) => api<any>(`/projects/${projectId}`),
};

export const AuthApi = {
  register: (email: string, displayName: string) =>
    api<RegisteredUser>('/auth/register', 'POST', { email, displayName }, { skipAuth: true }),
  me: () => api<CurrentUser>('/auth/me'),
  startMfa: () => api<{ secret: string; otpauthUrl: string }>('/auth/mfa/start', 'POST'),
  verifyMfa: (code: string) => api<{ mfaEnabled: boolean }>('/auth/mfa/verify', 'POST', { code }),
};

export const CodesApi = {
  list: (projectId: number) => api<any[]>(`/projects/${projectId}/codes`),
  importCsv: async (projectId: number, csv: string) => {
    const headers = defaultHeaders();
    headers['Content-Type'] = 'text/csv';

    const res = await fetch(`${API_BASE}/projects/${projectId}/codes/import`, {
      method: 'POST',
      headers,
      body: csv,
    });

    if (!res.ok) {
      const text = await res.text();
      throw new Error(`${res.status} ${res.statusText}: ${text}`);
    }

    return res.json();
  },
  importJson: (projectId: number, codes: any[]) => api<any>(`/projects/${projectId}/codes/import/json`, 'POST', codes),
  exportJson: (projectId: number) => api<any[]>(`/projects/${projectId}/codes/export`),
  purge: (projectId: number) => api<{ deletedDocuments: number; deletedCodes: number }>(`/projects/${projectId}/codes/purge`, 'DELETE'),
};

export const MembersApi = {
  list: (projectId: number) => api<any[]>(`/projects/${projectId}/members`),
  invite: (projectId: number, email: string, role: string, daysValid = 7) =>
    api<any>(`/projects/${projectId}/invites`, 'POST', { email, role, daysValid }),
  remove: (projectId: number, userId: number) => api<any>(`/projects/${projectId}/members/${userId}`, 'DELETE'),
  changeRole: (projectId: number, userId: number, role: string) =>
    api<any>(`/projects/${projectId}/members/${userId}/role`, 'POST', { role }),
  accept: (token: string) => api<any>(`/invites/accept`, 'POST', { token }),
  pendingInvites: (projectId: number) => api<any[]>(`/projects/${projectId}/invites`),
  cancelInvite: (projectId: number, inviteId: number) => api<any>(`/projects/${projectId}/invites/${inviteId}`, 'DELETE'),
};

export const DocumentsApi = {
  list: (projectId: number, q?: string) =>
    api<any[]>(`/projects/${projectId}/documents${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  create: (
    projectId: number,
    payload: { level1: string; level2: string; level3: string; level4?: string; freeText?: string; extension?: string },
  ) => api<any>(`/projects/${projectId}/documents`, 'POST', payload),
  preview: (
    projectId: number,
    payload: { level1: string; level2: string; level3: string; level4?: string; freeText?: string; extension?: string },
  ) => api<{ number: number; fileName: string }>(`/projects/${projectId}/documents/preview`, 'POST', payload),
  importCsv: async (projectId: number, csv: string) => {
    const headers = defaultHeaders();
    headers['Content-Type'] = 'text/csv';
    const res = await fetch(`${API_BASE}/projects/${projectId}/documents/import/csv`, {
      method: 'POST',
      headers,
      body: csv,
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`${res.status} ${res.statusText}: ${text}`);
    }
    return res.json();
  },
  importJson: (projectId: number, entries: any[]) =>
    api<any>(`/projects/${projectId}/documents/import/json`, 'POST', entries),
  importSimple: (
    projectId: number,
    entries: { code: string; fileName?: string; freeText?: string; description?: string }[],
  ) => api<any>(`/projects/${projectId}/documents/import`, 'POST', { entries }),
  exportJson: (projectId: number) => api<any[]>(`/projects/${projectId}/documents/export`),
  purge: (projectId: number) => api<{ deleted: number }>(`/projects/${projectId}/documents`, 'DELETE'),
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
