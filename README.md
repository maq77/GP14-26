# 🧱 SSSP – Smart Security & Sustainability Platform

[![Build Status](https://img.shields.io/github/actions/workflow/status/maq77/sssp/deploy.yml?branch=main&style=for-the-badge)](https://github.com/maq77/sssp/actions)
[![Version](https://img.shields.io/badge/version-1.2-blue?style=for-the-badge)](https://github.com/maq77/sssp/releases)
[![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)](LICENSE)
[![Docker](https://img.shields.io/badge/docker-ready-0db7ed?style=for-the-badge&logo=docker&logoColor=white)](#)
[![Platform](https://img.shields.io/badge/platform-linux--%7C--windows-lightgrey?style=for-the-badge)](#)

---

### _AI-Powered IoT Platform for Smart Cities_

SSSP integrates **security monitoring**, **environmental sustainability**, and **citizen engagement** into one intelligent ecosystem powered by **AI + IoT**.

---

## 🧱 Project Overview

This repository hosts the **full-stack monorepo** for SSSP, including all backend, frontend, and AI services.  
The stack is fully containerized with Docker, ensuring **identical environments** across development, CI/CD, and production.

### Includes:
- **api** – ASP.NET Core 9 backend  
- **ai** – Python FastAPI microservice for AI models  
- **web** – React (Vite + TypeScript) dashboard  
- **shared infrastructure** – SQL Server, Redis, RabbitMQ, MinIO  
- **scripts**, **docs**, **CI/CD**, and **IaC (Terraform/K8s)**

> _“If a new dev can run the app with one command, you’ve done DevOps right.”_

---

## 📂 Folder Structure v1.2

```
sssp/
├── .github/
│   ├── workflows/
│   │   ├── api-dotnet-ci.yml
│   │   ├── ai-fastapi-ci.yml
│   │   ├── web-dashboard-ci.yml
│   │   └── deploy.yml
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   ├── feature_request.md
│   │   └── tech_debt.md
│   ├── pull_request_template.md
│   └── CODEOWNERS
│
├── apps/
│   ├── api/       # .NET backend
│   ├── ai/        # FastAPI AI service
│   ├── web/       # React dashboard
│   └── workers/   # Background jobs (optional)
│
├── packages/      # Shared contracts & types
│   ├── contracts/
│   └── shared-types/
│
├── infrastructure/
│   ├── docker/    # Compose files
│   ├── terraform/ # Infra as Code
│   └── k8s/       # K8s manifests
│
├── scripts/       # Build & utility scripts
├── docs/          # Documentation
├── LICENSE
└── README.md
```

---

## ⚙️ Prerequisites

| Tool | Min Version | Notes |
|------|--------------|-------|
| **Docker Desktop** | 4.x | Required for containers |
| **Git** | Latest | For version control |
| **VS Code** | Latest | Recommended IDE |
| **Python** | 3.12+ | For AI local dev |
| **.NET SDK** | 8.0+ | For backend builds |

---

## 🚀 Quick Start (Local Dev)

### 1️⃣ Clone repository
```bash
git clone https://github.com/maq77/sssp.git
cd sssp
```

### 2️⃣ Prepare environment
```bash
cp .env.example .env
```

### 3️⃣ Start stack (Development mode)
```bash
cd infrastructure/docker
docker compose -f docker-compose.dev.yml up -d --build
```

### 4️⃣ Verify services

| Service | URL | Description |
|----------|-----|-------------|
| **API (.NET)** | http://localhost:8080 | Backend |
| **AI FastAPI** | http://localhost:8000/health | AI health check |
| **Web Dashboard** | http://localhost:5173 | Frontend |
| **RabbitMQ** | http://localhost:15672 | Messaging UI |
| **MinIO** | http://localhost:9001 | S3 console |
| **SQL Server** | localhost,1433 | Database |

---

## 🧰 Common Docker Commands

| Action | Command |
|--------|----------|
| Rebuild all images | `docker compose build --no-cache` |
| Restart containers | `docker compose up -d` |
| Remove all | `docker compose down -v` |
| Logs | `docker compose logs -f` |

---

## 🧱 Git & CI/CD

### Git Workflow
1. `main` → protected branch  
2. `feature/*` → new features  
3. PR required before merge

```bash
git checkout -b feature/add-login
```

### GitHub Actions
Each app has its own pipeline under `.github/workflows/`.

```yaml
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build Docker images
        run: docker compose -f infrastructure/docker/docker-compose.yml build
```

---

## 🧠 Best Practices

| Area | Guideline |
|------|------------|
| Environment | `.env.example → .env` |
| Dependencies | Pin versions |
| Secrets | Never commit to git |
| Code Reviews | Mandatory |
| Dockerfiles | Use LF endings |
| Docs | Keep updated |

---

## 🧹 .gitignore Essentials

```
.vs/
bin/
obj/
.env
.venv/
__pycache__/
*.pyc
*.pyo
*.log
node_modules/
dist/
build/
```

---

## 🧩 Troubleshooting

| Issue | Solution |
|--------|-----------|
| `failed to read dockerfile` | Rename → `Dockerfile` |
| `ModuleNotFoundError: fastapi` | Reinstall deps |
| `Permission denied (.vs)` | Delete `.vs/` |
| `version warning` | Remove `version:` line |

---

## 🤝 Contributing

1. Fork repo  
2. Create feature branch  
3. Commit with clear message  
4. Push & open PR  
5. Ensure CI passes  

---

## 🧭 Maintainers

**Core Team:**  
- Project Lead  
- AI Engineer  
- Backend Engineer  
- Frontend Engineer  
- DevOps Engineer  

**Tech Stack:**  
.NET 9 · FastAPI · Redis · RabbitMQ · SQL Server · Docker · Vite + React

---

## 🏁 License

Licensed under the **MIT License**.  
See [LICENSE](LICENSE) for details.

---
