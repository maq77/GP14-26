# SSSP - Smart Security & Sustainability Platform

AI-powered IoT platform for smart cities combining security monitoring, environmental sustainability, and citizen engagement.


# 🧱 SSSP Project – Developer Guide

> Full-stack project including:
> - **api-dotnet** (.NET backend)
> - **ai-fastapi** (Python AI service)
> - **web-dashboard** (Frontend)
> - Shared infrastructure: SQL Server, Redis, RabbitMQ, MinIO

---

## 🚀 Overview

This repository provides a complete **Docker-based development environment** for all SSSP services.  
Each service runs in its own container and communicates via an internal Docker network (`sssp-net`).

Your local machine runs the exact same environment used in CI/CD pipelines — no configuration drift, no “works on my machine.”

---

## Folder Structure v1.2 (new) based on Uber & Netflix Design
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
│   ├── api/                          # ASP.NET Core 9 (don't use "dotnet" in name)
│   │   ├── src/
│   │   │   ├── SSSP.Api/            # Main API project
│   │   │   │   ├── Controllers/
│   │   │   │   ├── Middleware/
│   │   │   │   ├── Hubs/            # SignalR
│   │   │   │   ├── Extensions/
│   │   │   │   ├── Program.cs
│   │   │   │   └── appsettings.json
│   │   │   ├── SSSP.Application/    # Use cases, DTOs
│   │   │   ├── SSSP.Domain/         # Entities, interfaces
│   │   │   └── SSSP.Infrastructure/ # EF Core, external services
│   │   ├── tests/
│   │   │   ├── SSSP.Api.Tests/
│   │   │   ├── SSSP.Application.Tests/
│   │   │   └── SSSP.Domain.Tests/
│   │   ├── Dockerfile
│   │   ├── .dockerignore
│   │   └── SSSP.sln
│   │
│   ├── ai/                           # FastAPI AI Service
│   │   ├── src/
│   │   │   ├── api/
│   │   │   │   ├── routes/
│   │   │   │   ├── middleware/
│   │   │   │   └── main.py
│   │   │   ├── core/
│   │   │   │   ├── config.py
│   │   │   │   └── exceptions.py
│   │   │   ├── models/              # ML models
│   │   │   │   ├── yolo/
│   │   │   │   ├── face/
│   │   │   │   └── behavior/
│   │   │   ├── services/            # Business logic
│   │   │   └── schemas/             # Pydantic models
│   │   ├── tests/
│   │   ├── notebooks/               # Jupyter experiments
│   │   ├── scripts/                 # Training scripts
│   │   ├── Dockerfile
│   │   ├── requirements.txt
│   │   └── pyproject.toml
│   │
│   ├── web/                          # React Dashboard
│   │   ├── public/
│   │   ├── src/
│   │   │   ├── components/
│   │   │   │   ├── ui/              # Base components
│   │   │   │   ├── layout/
│   │   │   │   └── features/        # Feature-specific
│   │   │   ├── pages/
│   │   │   │   ├── admin/
│   │   │   │   ├── operator/
│   │   │   │   └── user/
│   │   │   ├── services/            # API calls
│   │   │   ├── hooks/
│   │   │   ├── store/               # Zustand
│   │   │   ├── types/
│   │   │   ├── utils/
│   │   │   ├── App.tsx
│   │   │   └── main.tsx
│   │   ├── tests/
│   │   ├── Dockerfile
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   └── vite.config.ts
│   │
│   └── workers/                      # Background jobs (optional for MVP)
│       └── incident-processor/
│
├── packages/                         # SHARED CODE (Critical for monorepo!)
│   ├── contracts/                    # Shared contracts
│   │   ├── proto/                    # gRPC .proto files
│   │   │   └── inference.proto
│   │   ├── events/                   # Event schemas
│   │   │   ├── incident.events.ts
│   │   │   └── detection.events.py
│   │   └── openapi/                  # OpenAPI specs
│   │       └── api.yaml
│   │
│   └── shared-types/                 # TypeScript types (if needed)
│       └── index.ts
│
├── infrastructure/                   # BETTER than "deploy/"
│   ├── docker/
│   │   ├── docker-compose.yml
│   │   ├── docker-compose.dev.yml
│   │   ├── docker-compose.prod.yml
│   │   └── .env.example
│   │
│   ├── terraform/                    # Infrastructure as Code (add later)
│   │   ├── environments/
│   │   │   ├── dev/
│   │   │   └── prod/
│   │   └── modules/
│   │
│   └── k8s/                          # Kubernetes (Phase 3)
│       ├── base/
│       └── overlays/
│
├── scripts/                          # BUILD & UTILITY SCRIPTS
│   ├── setup-dev.sh
│   ├── run-tests.sh
│   ├── seed-db.sh
│   └── deploy.sh
│
├── docs/                             # DOCUMENTATION
│   ├── architecture/
│   │   ├── ADRs/                     # Architecture Decision Records
│   │   │   ├── 001-use-clean-architecture.md
│   │   │   ├── 002-grpc-for-ml-integration.md
│   │   │   └── 003-signalr-for-realtime.md
│   │   ├── c4/                       # C4 diagrams
│   │   │   ├── context.png
│   │   │   ├── container.png
│   │   │   └── component.png
│   │   └── data-flow.md
│   │
│   ├── api/                          # API documentation
│   │   ├── rest-api.md
│   │   └── grpc-api.md
│   │
│   ├── development/
│   │   ├── getting-started.md
│   │   ├── coding-standards.md
│   │   └── testing-guide.md
│   │
│   ├── deployment/
│   │   ├── docker-guide.md
│   │   └── production-checklist.md
│   │
│   └── ml/                           # ML-specific docs
│       ├── model-training.md
│       ├── dataset-guide.md
│       └── inference-optimization.md
│
├── .editorconfig                     # Code style config
├── .gitignore
├── .gitattributes
├── LICENSE
├── README.md
├── CHANGELOG.md                      # Version history
├── CONTRIBUTING.md
├── SECURITY.md
├── CODE_OF_CONDUCT.md
└── CODEOWNERS
```

---

## ⚙️ Prerequisites

| Tool | Minimum Version | Notes |
|------|------------------|-------|
| Docker Desktop | 4.x | Required for containers |
| Git | Latest | For version control |
| VS Code | Latest | Recommended IDE |
| Python | 3.12+ | Optional for AI local testing |
| .NET SDK | 8.0+ | Optional for API local builds |

---

## 🪄 Quick Start (Local Dev)
### 2. Setup development environment
./scripts/setup-dev.sh


### 1️⃣ Clone the repository
```bash
git clone https://github.com/maq77/sssp.git
cd sssp
```

### 2️⃣ Prepare environment file
```bash
cp .env.example .env
```

### 3️⃣ Build & run everything
```bash
cd infrastructure/docker
docker compose up -d --build
```

### 4️⃣ Verify services
| Service | URL | Description |
|----------|-----|-------------|
| API (.NET) | http://localhost:8080 | Main backend |
| FastAPI | http://localhost:8000/health | AI healthcheck |
| Dashboard | http://localhost:5173 | Web UI |
| RabbitMQ | http://localhost:15672 | Messaging UI |
| MinIO Console | http://localhost:9001 | S3 storage UI |
| SQL Server | localhost,1433 | Use SSMS or Azure Data Studio |

---

## 🧰 Developer Workflow

| Action | Command |
|--------|----------|
| Rebuild all images | docker compose build --no-cache |
| Restart containers | docker compose up -d |
| Remove containers and volumes | docker compose down -v |
| Check logs | docker compose logs -f |

---

## 🧩 Docker Compose Details

Each service has its own Dockerfile under `apps/<service>/Dockerfile`.

Example:
```yaml
ai-fastapi:
  build:
    context: ../../apps/ai-fastapi
    dockerfile: Dockerfile
  env_file:
    - ../../.env
  environment:
    <<: *default-env
  ports:
    - "8000:8000"
    - "50051:50051"
```

---

## 🧱 Branching & CI/CD

### Git Workflow
1. main → protected branch
2. feature/* → new features
3. PR required before merge

Example:
```bash
git checkout -b feature/add-login
```

### GitHub Actions CI
```yaml
name: Build & Test
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build Docker images
        run: docker compose -f deploy/docker/docker-compose.yml build
```

---

## 🧠 Team Practices

| Area | Best Practice |
|------|----------------|
| Environment | Use .env.example → copy to .env |
| Dependencies | Pin versions |
| Secrets | Never commit to git |
| Code Reviews | Required on PRs |
| Dockerfile names | Always `Dockerfile` |
| Line endings | LF for Dockerfiles |
| Documentation | Keep README updated |

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

## 🧠 Troubleshooting

| Issue | Fix |
|--------|------|
| `failed to read dockerfile` | Rename to `Dockerfile` (lowercase `f`) |
| `ModuleNotFoundError: No module named 'fastapi'` | Create venv & install deps |
| `Permission denied` in `.vs` | Close VS, delete `.vs/` |
| `version` warning | Remove version: line from docker-compose.yml |

---

## 💬 Contributing

1. Fork repo  
2. Create feature branch  
3. Commit with clear message  
4. Push and open PR  
5. Ensure Docker build passes before merge

---

## 🧭 Credits

**Maintainers:** Project Lead + Team  
**Tech Stack:** .NET, FastAPI, Redis, RabbitMQ, SQL Server, Docker, Vite

---

> _“If a new dev can run the app with one command, you’ve done DevOps right.”_
