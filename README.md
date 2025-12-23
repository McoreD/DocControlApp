# DocControl App (Backend + Frontend)

## Prerequisites
- .NET 10 SDK
- Node.js 20+ (for frontend)
- PostgreSQL (Neon connection string preferred)

## Environment (Functions)
- Set database connection (exact casing matters on Linux):
  - `ConnectionStrings__Db=<sanitized Npgsql connection string>`
  - or `DbConnection=<sanitized Npgsql connection string>`
- Optional API keys path: `ApiKeysPath` (defaults to `data/apikeys.json`).
- Dev auth: register first (`POST /auth/register` or via the UI) to get a user id, then pass headers `x-user-id`, `x-user-email`, `x-user-name` (UI sets them from localStorage). Replace with real token validation before production.

### Neon connection tips
- Remove unsupported params like `channel_binding` (sanitizer in code also handles this).
- Ensure `SSL Mode=Require`.

## Backend (Azure Functions isolated)
```bash
cd DocControl.Api
# local.settings.json already has a sample; replace DbConnection with your Neon string
dotnet build
func start   # or dotnet run
```

Key endpoints (all prefixed with `/api`):
- Projects: `GET/POST /projects`, `GET /projects/{projectId}`
- Members/Invites: `GET /projects/{id}/members`, `POST /projects/{id}/invites`, `POST /invites/accept`, `POST /projects/{id}/members/{userId}/role`, `DELETE /projects/{id}/members/{userId}`
- Codes: `GET /projects/{id}/codes`, `POST /projects/{id}/codes`, `DELETE /projects/{id}/codes/{codeSeriesId}`, `POST /projects/{id}/codes/import` (CSV `Level,Code,Code Description`)
- Documents: `GET /projects/{id}/documents`, `GET /projects/{id}/documents/{docId}`, `POST /projects/{id}/documents`, `POST /projects/{id}/documents/import` (lines: `CODE filename`)
- Audit: `GET /projects/{id}/audit`
- Settings: `GET/POST /projects/{id}/settings`
- AI: `POST /projects/{id}/ai/interpret`, `POST /projects/{id}/ai/recommend`
- Auth: `POST /auth/register` (dev-time registration to receive a user id)

## Frontend (React + Vite, dark mode)
```bash
cd web
npm install
npm run dev   # or npm run build
```

Dev auth: set in browser console/localStorage to impersonate:
```js
// Use the Register screen in the app or call POST /auth/register to get these values.
localStorage.setItem('dc.userId', '<user id>');
localStorage.setItem('dc.email', '<email>');
localStorage.setItem('dc.name', '<display name>');
```

Select a project on the Projects page; the selection is stored and used across pages.

## Remaining production tasks
- Replace header-based auth with real OIDC/PKCE token validation and map roles per project.
- Swap `CredentialManagement` for Key Vault/secret store to clear NuGet warnings.
- Add error toasts/spinners as desired.

## Quick sanity checks
- `dotnet build DocControlApp.sln`
- `cd web && npm run build`
