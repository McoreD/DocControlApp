#!/bin/bash
# DocControl App - Development Startup Script
# This script starts both the backend and frontend for local development

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   DocControl App - Development Setup      ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

# Check if running from correct directory
if [ ! -f "DocControlApp.sln" ]; then
    echo -e "${RED}❌ Error: DocControlApp.sln not found${NC}"
    echo "Please run this script from the workspace root directory."
    exit 1
fi

# Function to start backend
start_backend() {
    echo -e "${BLUE}→ Starting Backend (Azure Functions)...${NC}"
    cd DocControl.Api
    
    if ! dotnet run --no-launch-profile > /tmp/backend.log 2>&1 &
    then
        echo -e "${RED}❌ Failed to start backend${NC}"
        cat /tmp/backend.log
        exit 1
    fi
    
    BACKEND_PID=$!
    echo -e "${GREEN}✓ Backend starting (PID: $BACKEND_PID)${NC}"
    
    # Wait for backend to be ready
    echo "  Waiting for backend to be ready..."
    for i in {1..30}; do
        if curl -s http://localhost:7071/api/projects \
            -H "x-user-id: 1" \
            -H "x-user-email: test@example.com" \
            -H "x-user-name: Test" > /dev/null 2>&1; then
            echo -e "  ${GREEN}✓ Backend is ready!${NC}"
            break
        fi
        sleep 1
        if [ $i -eq 30 ]; then
            echo -e "  ${YELLOW}⚠ Backend might still be initializing...${NC}"
        fi
    done
    
    cd ..
}

# Function to start frontend
start_frontend() {
    echo -e "${BLUE}→ Starting Frontend (React + Vite)...${NC}"
    cd web
    
    if ! nohup npm run dev > /tmp/frontend.log 2>&1 &
    then
        echo -e "${RED}❌ Failed to start frontend${NC}"
        cat /tmp/frontend.log
        exit 1
    fi
    
    FRONTEND_PID=$!
    echo -e "${GREEN}✓ Frontend starting (PID: $FRONTEND_PID)${NC}"
    
    # Wait for frontend to be ready
    echo "  Waiting for frontend to be ready..."
    for i in {1..30}; do
        if curl -s http://localhost:5173 > /dev/null 2>&1; then
            echo -e "  ${GREEN}✓ Frontend is ready!${NC}"
            break
        fi
        sleep 1
        if [ $i -eq 30 ]; then
            echo -e "  ${YELLOW}⚠ Frontend might still be initializing...${NC}"
        fi
    done
    
    cd ..
}

# Kill previous instances
echo -e "${YELLOW}→ Cleaning up old processes...${NC}"
pkill -f "dotnet run --no-launch-profile" 2>/dev/null || true
pkill -f "npm run dev" 2>/dev/null || true
sleep 1

# Start services
start_backend
start_frontend

# Display URLs and info
echo ""
echo -e "${GREEN}╔════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║      ✓ Development Environment Ready      ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${BLUE}Services:${NC}"
echo -e "  Backend:  ${YELLOW}http://localhost:7071${NC}"
echo -e "  Frontend: ${YELLOW}http://localhost:5173${NC}"
echo ""
echo -e "${BLUE}Development Auth (localStorage):${NC}"
echo -e "  ${YELLOW}localStorage.setItem('dc.userId', '1');${NC}"
echo -e "  ${YELLOW}localStorage.setItem('dc.email', 'owner@example.com');${NC}"
echo -e "  ${YELLOW}localStorage.setItem('dc.name', 'Owner User');${NC}"
echo ""
echo -e "${BLUE}Logs:${NC}"
echo -e "  Backend:  ${YELLOW}/tmp/backend.log${NC}"
echo -e "  Frontend: ${YELLOW}/tmp/frontend.log${NC}"
echo ""
echo -e "${BLUE}Database:${NC}"
echo -e "  Provider: ${YELLOW}Neon PostgreSQL${NC}"
echo -e "  Region:   ${YELLOW}East US 2${NC}"
echo ""
echo -e "${BLUE}Documentation:${NC}"
echo -e "  Setup Guide: ${YELLOW}./SETUP.md${NC}"
echo ""
echo -e "${YELLOW}⏸  Press Ctrl+C to stop services${NC}"
echo ""

# Keep script running
wait
