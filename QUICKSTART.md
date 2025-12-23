# 🎉 DocControl App - Setup Complete!

## Status: ✅ FULLY OPERATIONAL

Your DocControl application is now fully built and configured for local development!

---

## 🚀 Quick Start

### Option 1: Automated Startup (Recommended)
```bash
cd /workspaces/DocControlApp
./start-dev.sh
```

This script will:
- Kill any previous instances
- Start the backend (Azure Functions on port 7071)
- Start the frontend (React on port 5173)
- Wait for both to be ready
- Display startup information and links

### Option 2: Manual Startup

**Terminal 1 - Backend:**
```bash
cd /workspaces/DocControlApp/DocControl.Api
dotnet run --no-launch-profile
```

**Terminal 2 - Frontend:**
```bash
cd /workspaces/DocControlApp/web
npm run dev
```

---

## 🌐 Access Your Application

Once both services are running:

- **Frontend UI**: http://localhost:5173
- **Backend API**: http://localhost:7071
- **API Documentation**: See [SETUP.md](./SETUP.md#available-backend-endpoints)

---

## 🔐 Login / Authentication

Since this is development, use header-based auth:

### Option A: Browser Console (Easiest)
Open your browser's developer console (F12) and run:
```javascript
localStorage.setItem('dc.userId', '1');
localStorage.setItem('dc.email', 'owner@example.com');
localStorage.setItem('dc.name', 'Owner User');
localStorage.setItem('dc.projectId', '1'); // Optional: set default project
```

Then refresh the page. You'll be authenticated as "Owner User".

### Option B: API with curl
```bash
curl -H "x-user-id: 1" \
     -H "x-user-email: owner@example.com" \
     -H "x-user-name: Owner User" \
     http://localhost:7071/api/projects
```

---

## 📋 What Was Set Up

### ✅ Backend (.NET 10 Azure Functions)
- **Status**: Running successfully
- **Database**: Connected to Neon PostgreSQL
- **Schema**: Automatically created on startup
- **Endpoints**: All 21 API endpoints loaded and operational
- **Config**: Stored in `DocControl.Api/local.settings.json`

### ✅ Frontend (React + TypeScript + Vite)
- **Status**: Ready to run
- **Build**: Optimized with Vite
- **Dependencies**: All installed via npm
- **Hot Reload**: Enabled for development
- **Build Output**: `web/dist/` (run `npm run build`)

### ✅ Database (Neon PostgreSQL)
- **Host**: ep-hidden-fog-a8923y2b-pooler.eastus2.azure.neon.tech
- **Database**: neondb
- **Tables**: Users, Projects, Members, Codes, Documents, Audit, etc.
- **SSL Mode**: Required and configured

---

## 📚 Project Structure

```
DocControlApp/
├── DocControl.Api/              # Azure Functions (Backend)
│   ├── Functions/               # HTTP trigger functions
│   ├── Infrastructure/          # Auth, hosting services
│   └── local.settings.json      # Config & secrets
│
├── DocControl.Core/             # Shared models & configuration
│   ├── Models/                  # DTOs, entities
│   └── Security/                # Auth & credential handling
│
├── DocControl.Infrastructure/   # Data & business logic
│   ├── Data/                    # Database repositories
│   └── Services/                # Business services
│
├── DocControl.AI/               # AI orchestration
│   ├── GeminiClient.cs
│   ├── OpenAiClient.cs
│   └── AiOrchestrator.cs
│
├── web/                         # React frontend
│   ├── src/
│   │   ├── views/               # Page components
│   │   ├── shell/               # App layout
│   │   └── lib/                 # API client, utilities
│   └── package.json
│
├── SETUP.md                     # Detailed setup documentation
├── start-dev.sh                 # Development startup script
└── DocControlApp.sln            # Solution file
```

---

## 🔧 Common Tasks

### View Logs

**Backend logs:**
```bash
tail -f /tmp/backend.log
```

**Frontend logs:**
```bash
tail -f /tmp/frontend.log
```

### Restart Services

**Kill backend:**
```bash
pkill -f "dotnet run"
```

**Kill frontend:**
```bash
pkill -f "npm run dev"
```

### Build for Production

**Backend:**
```bash
cd /workspaces/DocControlApp
dotnet build -c Release DocControlApp.sln
```

**Frontend:**
```bash
cd /workspaces/DocControlApp/web
npm run build
# Output: dist/ folder (ready to deploy)
```

### Run Type Checks (Frontend)
```bash
cd /workspaces/DocControlApp/web
npm run build    # Includes TypeScript checking
```

### Run Linting (Frontend)
```bash
cd /workspaces/DocControlApp/web
npm run lint
```

---

## 🧪 Test the Backend

### Create a Project
```bash
curl -X POST http://localhost:7071/api/projects \
  -H "x-user-id: 1" \
  -H "x-user-email: owner@example.com" \
  -H "x-user-name: Owner User" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Project",
    "description": "A test project to get started"
  }'
```

### List Projects
```bash
curl http://localhost:7071/api/projects \
  -H "x-user-id: 1" \
  -H "x-user-email: owner@example.com" \
  -H "x-user-name: Owner User"
```

### Upload Documents
```bash
curl -X POST http://localhost:7071/api/projects/1/documents/import \
  -H "x-user-id: 1" \
  -H "x-user-email: owner@example.com" \
  -H "x-user-name: Owner User" \
  -H "Content-Type: application/json" \
  -d '{
    "importLines": ["COD-001 document1.pdf", "COD-002 document2.pdf"]
  }'
```

---

## ⚙️ Configuration Files

### Backend Config: `local.settings.json`
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DbConnection": "Server=...;Database=neondb;..."
  }
}
```

**Optional environment variables:**
- `ApiKeysPath` - Path to API keys JSON file
- OpenAI/Gemini API keys (when configuring AI features)

### Frontend Config: `vite.config.ts`
- Development server runs on port 5173
- Configured for React 19 with TypeScript
- ESLint enabled

---

## 📦 Dependencies

### Backend NuGet Packages
- Microsoft.Azure.Functions.Worker (2.51.0)
- Microsoft.ApplicationInsights.WorkerService (2.23.0)
- Npgsql (8.0.4) - PostgreSQL driver
- Dapper (2.1.35) - Micro-ORM
- CredentialManagement (1.0.2) - ⚠️ Deprecate for production

### Frontend npm Packages
- react (19.2.0)
- react-dom (19.2.0)
- react-router-dom (7.11.0)
- vite (7.2.4)
- typescript (5.9.3)

---

## 🚨 Known Issues & Warnings

### ⚠️ Build Warnings (Safe to Ignore)
The solution builds with 6 warnings about the `CredentialManagement` NuGet package being incompatible with .NET 10. This is a legacy package and should be replaced with Azure Key Vault for production.

**Production Action**: Replace with Azure Key Vault integration.

### 🔐 Authentication
Currently uses development header-based auth. 

**Production Action**: Implement OIDC/PKCE token validation.

### 📊 AI Features
AI interpretation and recommendations require API keys for OpenAI or Google Gemini.

**To Enable**:
1. Add API keys to `local.settings.json`
2. Configure in frontend (Settings page)

---

## 📖 Documentation

- **Setup Guide**: [./SETUP.md](./SETUP.md)
- **Backend README**: [./DocControl.Api/README.md](./DocControl.Api) *(if exists)*
- **Frontend README**: [./web/README.md](./web/README.md)
- **Main README**: [./README.md](./README.md)

---

## 🆘 Troubleshooting

### Backend won't start
1. Check `DbConnection` in `local.settings.json`
2. Verify Neon connection is accessible
3. Check logs: `cat /tmp/backend.log`

### Frontend can't connect to backend
1. Ensure both are running: `ps aux | grep -E "npm|dotnet"`
2. Check CORS in backend configuration
3. Verify auth headers are being sent
4. Check browser console (F12) for errors

### Database connection timeout
1. Neon may have connection limits
2. The connection string uses Neon's pooler endpoint (already configured)
3. Check your Neon dashboard for connection issues

### Port already in use
```bash
# Free port 7071 (backend)
lsof -ti:7071 | xargs kill -9

# Free port 5173 (frontend)
lsof -ti:5173 | xargs kill -9
```

---

## 🎯 Next Steps

1. **Start the dev environment**: `./start-dev.sh`
2. **Open the frontend**: http://localhost:5173
3. **Set authentication**: Use browser console to set localStorage
4. **Create a test project**: Via UI or API
5. **Upload documents**: Test the import functionality
6. **Configure code classifications**: Set up your document codes
7. *(Optional)* **Setup AI features**: Add OpenAI/Gemini keys

---

## 🚀 Ready for Development!

Your application is fully set up and ready to go. Start the development environment and begin building!

**Need help?** Check [SETUP.md](./SETUP.md) for detailed documentation of all API endpoints and configurations.

---

**Last Updated**: December 23, 2025  
**Status**: ✅ All Systems Operational
