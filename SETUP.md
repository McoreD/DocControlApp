# DocControl App - Development Setup Guide

## ✅ Completed Setup

Your solution is now fully configured and ready for development!

### Backend Status: **RUNNING** ✅

**Database**: Connected to Neon PostgreSQL  
**API Port**: http://localhost:7071  
**Status**: All 21 API endpoints are loaded and operational

### Available Backend Endpoints

#### Projects
- `GET /api/projects` - List all projects
- `POST /api/projects` - Create new project  
- `GET /api/projects/{projectId}` - Get project details

#### Members & Invites
- `GET /api/projects/{projectId}/members` - List project members
- `POST /api/projects/{projectId}/invites` - Send project invite
- `POST /api/invites/accept` - Accept project invite
- `POST /api/projects/{projectId}/members/{userId}/role` - Change member role
- `DELETE /api/projects/{projectId}/members/{userId}` - Remove member

#### Codes (Classification System)
- `GET /api/projects/{projectId}/codes` - List code series
- `POST /api/projects/{projectId}/codes` - Create/update codes
- `DELETE /api/projects/{projectId}/codes/{codeSeriesId}` - Delete code series
- `POST /api/projects/{projectId}/codes/import` - Bulk import codes (CSV: `Level,Code,Code Description`)

#### Documents
- `GET /api/projects/{projectId}/documents` - List documents
- `GET /api/projects/{projectId}/documents/{documentId}` - Get document details
- `POST /api/projects/{projectId}/documents` - Upload document
- `POST /api/projects/{projectId}/documents/import` - Bulk import (format: `CODE filename`)

#### Audit
- `GET /api/projects/{projectId}/audit` - View audit logs

#### Settings
- `GET /api/projects/{projectId}/settings` - Get project settings
- `POST /api/projects/{projectId}/settings` - Save project settings

#### AI Features
- `POST /api/projects/{projectId}/ai/interpret` - Interpret documents using AI
- `POST /api/projects/{projectId}/ai/recommend` - Get recommendations

---

## 🚀 Running Development Environment

### Start Backend (Already Running in Background)

The backend is currently running. To restart it:

```bash
cd /workspaces/DocControlApp/DocControl.Api
dotnet run --no-launch-profile
```

The backend will:
1. Automatically create/initialize the database schema on startup
2. Load all Azure Functions
3. Listen on `http://localhost:7071`

### Start Frontend

Open a new terminal and run:

```bash
cd /workspaces/DocControlApp/web
npm run dev
```

This will start the React dev server with HMR (Hot Module Reload). The frontend will be available at `http://localhost:5173` (or next available port).

---

## 🔐 Development Authentication

The app uses header-based authentication for development. The backend reads these headers:

- `x-user-id` - Unique user ID (number)
- `x-user-email` - User email address  
- `x-user-name` - Display name

**Set credentials in browser localStorage** (for frontend use):

```javascript
localStorage.setItem('dc.userId', '1');
localStorage.setItem('dc.email', 'owner@example.com');
localStorage.setItem('dc.name', 'Owner User');
```

Or use curl with headers:

```bash
curl -H "x-user-id: 1" \
     -H "x-user-email: owner@example.com" \
     -H "x-user-name: Owner User" \
     http://localhost:7071/api/projects
```

---

## 📋 Configuration Files

### Backend Configuration
**File**: `/workspaces/DocControlApp/DocControl.Api/local.settings.json`

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DbConnection": "Server=ep-hidden-fog-a8923y2b-pooler.eastus2.azure.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_pmAWirkO3HZ6;SslMode=Require"
  }
}
```

**Optional Settings**:
- `ApiKeysPath` - Path to API keys JSON file (defaults to `data/apikeys.json`)
- AI provider keys (when configured)

### Frontend Configuration
**File**: `/workspaces/DocControlApp/web`

- `vite.config.ts` - Vite build config
- `tsconfig.json` - TypeScript configuration  
- `.env` files for environment-specific settings (optional)

---

## 🗄️ Database (Neon PostgreSQL)

**Connection Details**:
- **Host**: ep-hidden-fog-a8923y2b-pooler.eastus2.azure.neon.tech
- **Database**: neondb
- **Region**: East US 2
- **SSL Mode**: Required

**Schema**: Automatically created on first backend startup

**Tables Created**:
- `Users` - User accounts
- `UserAuth` - Authentication details
- `Projects` - Project records
- `ProjectMembers` - Project team membership
- `ProjectInvites` - Pending invitations
- `Config` - Configuration settings
- `CodeSeries` - Classification codes
- `Documents` - Document records
- `AuditLog` - Activity tracking
- `NumberAllocator` - Document numbering

---

## 🛠️ Build & Deploy

### Build Backend (Release)
```bash
cd /workspaces/DocControlApp
dotnet build -c Release DocControlApp.sln
```

### Build Frontend (Production)
```bash
cd /workspaces/DocControlApp/web
npm run build
```

Output: `dist/` folder with optimized static files

---

## ⚠️ Known Limitations & TODO

### Production Readiness
- [ ] Replace header-based auth with OIDC/PKCE tokens
- [ ] Move API keys to Azure Key Vault (replaces CredentialManagement package)
- [ ] Add error toasts/spinners for better UX
- [ ] Configure CORS properly for production domains
- [ ] Set up logging aggregation (Application Insights configured but needs setup)

### AI Features
- Requires API keys for OpenAI or Google Gemini
- Configure in local.settings.json when ready

---

## 🧪 Testing the API

### Create a Project
```bash
curl -X POST http://localhost:7071/api/projects \
  -H "x-user-id: 1" \
  -H "x-user-email: owner@example.com" \
  -H "x-user-name: Owner User" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Sample Project",
    "description": "Test project"
  }'
```

### List Projects
```bash
curl http://localhost:7071/api/projects \
  -H "x-user-id: 1" \
  -H "x-user-email: owner@example.com" \
  -H "x-user-name: Owner User"
```

---

## 📚 Stack Overview

### Backend
- **.NET 10** (Azure Functions Isolated)
- **PostgreSQL** (Npgsql driver)
- **Dapper** (Micro-ORM)
- **Azure Functions** (Serverless compute)
- **Application Insights** (Logging & monitoring)

### Frontend
- **React 19**
- **TypeScript 5.9**
- **Vite 7** (Lightning-fast build tool)
- **React Router 7** (Routing)
- **Dark Mode Support**

### Deployment Ready
- Backend: Deploy to Azure Functions
- Frontend: Deploy to Azure Static Web Apps, Vercel, Netlify, or CDN
- Database: Neon PostgreSQL (serverless, auto-scaling)

---

## 📞 Support & Next Steps

### Next Things to Try
1. **Start frontend**: `cd web && npm run dev`
2. **Create a test project** via the API or UI
3. **Add project members** with invites
4. **Set up code classifications**
5. **Upload documents**
6. **Configure AI models** (optional)

### Common Issues

**Backend won't start**:
- Verify Neon database connection is accessible
- Check `DbConnection` in `local.settings.json`
- Look at logs in `/tmp/backend.log`

**Frontend can't reach backend**:
- Ensure backend is running on http://localhost:7071
- Check browser console for CORS errors
- Verify auth headers are being sent

**Database connection timeouts**:
- Neon has connection pooling limits
- Consider using Neon's pooler endpoint (already configured)

---

**Setup Date**: December 23, 2025  
**Status**: ✅ Ready for Development
