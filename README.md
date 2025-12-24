# DocControl App (Backend + Frontend)
Project tracking, document numbering, code catalog, audit log, and basic AI helpers. Backend runs on Azure Functions (isolated) with PostgreSQL; frontend is React/Vite.

## Requirements
- .NET 8 SDK
- Node.js 20+
- PostgreSQL (Neon friendly; SSL required)

## Environment (Functions)
- Configure database connection (case matters on Linux):
  - `ConnectionStrings__Db=<Npgsql connection string>` **or** `DbConnection=<Npgsql connection string>`
- Optional: `ApiKeysPath` for AI keys (defaults to a writable temp path like `%HOME%/doccontrol/apikeys.json`)
- Auth/dev flow: register first (`POST /auth/register` or via the UI) to get a user id, then pass headers `x-user-id`, `x-user-email`, `x-user-name` (UI persists these). MFA is mandatory: call `/auth/mfa/start` then `/auth/mfa/verify` (the UI also offers QR).

### Neon connection tips
- Remove unsupported params like `channel_binding` (the runtime also sanitizes).
- Ensure `SSL Mode=Require`.

## Backend (Azure Functions isolated)
```bash
cd DocControl.Api
# local.settings.json contains a sample; replace DbConnection with your Postgres string
dotnet build
func start   # or: dotnet run
```
Key endpoints (all prefixed with `/api`):
- Projects: `GET/POST /projects`, `GET /projects/{projectId}`
- Members/Invites: `GET /projects/{id}/members`, `POST /projects/{id}/invites`, `POST /invites/accept`, `POST /projects/{id}/members/{userId}/role`, `DELETE /projects/{id}/members/{userId}`
- Codes: `GET /projects/{id}/codes`, `POST /projects/{id}/codes`, `DELETE /projects/{id}/codes/{codeSeriesId}`, `POST /projects/{id}/codes/import` (CSV `Level,Code,Code Description`)
- Documents: `GET /projects/{id}/documents`, `GET /projects/{id}/documents/{docId}`, `POST /projects/{id}/documents`, `POST /projects/{id}/documents/import` (lines: `CODE filename`), `DELETE /projects/{id}/documents` (owner-only purge)
- Audit: `GET /projects/{id}/audit`
- Settings: `GET/POST /projects/{id}/settings`
- AI: `POST /projects/{id}/ai/interpret`, `POST /projects/{id}/ai/recommend`
- Auth: `POST /auth/register`, `GET /auth/me`, `POST /auth/mfa/start`, `POST /auth/mfa/verify`

## Frontend (React + Vite)
```bash
cd web
npm install
npm run dev     # or: npm run build
```
Features: project switcher, code catalog table, document generator/importer, members & roles, audit, settings, AI recommend/interpret, management (document purge), MFA setup with QR.

### Dev auth shortcut
Use the Register screen or set localStorage directly after calling `/auth/register`:
```js
localStorage.setItem('dc.userId', '<user id>');
localStorage.setItem('dc.email', '<email>');
localStorage.setItem('dc.name', '<display name>');
localStorage.setItem('dc.mfa', 'true'); // set true after /auth/mfa/verify
```
Pick a project on the Projects page; selection is persisted.

## Production TODOs
- Replace header-based dev auth with real OIDC/PKCE token validation and map roles per project.
- Swap CredentialManagement for a secret store/Key Vault to clear NuGet warnings.
- Add richer UX (toasts/spinners) as desired.

## Quick checks
- `dotnet build DocControlApp.sln`
- `cd web && npm run build`
