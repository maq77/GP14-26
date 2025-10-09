
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

## 🧩 Folder Structure

```
sssp/
  apps/
    api-dotnet/        # ASP.NET Core 9
    ai-fastapi/        # FastAPI + PyTorch/YOLO
    web-dashboard/     # React + TS
    workers/           # optional: queue consumers, schedulers
  core/
    domain/            # entities, value objects, domain services
    application/       # use-cases, ports (interfaces), DTOs
    infrastructure/    # repos, providers, adapters
    shared/            # contracts, errors, utils
  deploy/
    docker/            # Dockerfiles, docker-compose.yml
    k8s/               # optional later
  db/
    migrations/        # EF Core / Alembic
    seeds/
  .github/
    workflows/         # GitHub Actions
    ISSUE_TEMPLATE/
  docs/
    ADRs/              # Architecture Decision Records
    diagrams/
  .env.example
  CODEOWNERS
  CONTRIBUTING.md
  SECURITY.md

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

### 1️⃣ Clone the repository
```bash
git clone https://github.com/maq77/GP14-26.git
cd GP14-26
```

### 2️⃣ Prepare environment file
```bash
cp .env.example .env
```

### 3️⃣ Build & run everything
```bash
cd deploy/docker
docker compose up -d --build
```

### 4️⃣ Verify services
| Service | URL | Description |
|----------|-----|-------------|
| API (.NET) | [http://localhost:8080](http://localhost:8080/swagger/index.html)| Main backend |
| FastAPI | [http://localhost:8000/](http://localhost:8000/docs) | AI endpoint |
| Dashboard | [http://localhost:8081](http://localhost:8081/) | Web UI |
| RabbitMQ | http://localhost:15672 | Messaging UI |
| MinIO Console | http://localhost:9001 | S3 storage UI |
| SQL Server | sqlserver,1433 | Use SSMS or Azure Data Studio |

---

###


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

> _“We're the next Gen.....!”_
