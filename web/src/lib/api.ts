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
};
