````md
# SSSP – Local Development with Docker Compose

This README explains how to run the SSSP stack locally using Docker Compose for development.

> This repo uses a dedicated compose file: `docker-compose-v3.yml`  
> All commands below assume you are running them from:
> `infrastructure/docker/`

---

## Prerequisites

- **Docker Desktop** (Windows/Mac) or Docker Engine (Linux)
- **Docker Compose v2** (comes with Docker Desktop)
- Enough resources:
  - RAM: 8–16 GB recommended
  - CPU: 4+ cores recommended

- **Be Aware** it usually takes more than 2 Hours, so Be Patient.

### Check versions

```bash
docker version
docker compose version
````


## Quick Start (First Run)

### 1) Go to the compose folder

**PowerShell**

```powershell
cd M:\SSSP\GP14-26\infrastructure\docker
```

### 2) (Recommended) Pull base images

```bash
docker compose -f docker-compose-v3.yml pull
```

### 3) Build + start everything

```bash
docker compose -f docker-compose-v3.yml up -d --build
```

### 4) Verify containers are up

```bash
docker compose -f docker-compose-v3.yml ps
```

### 5) Open Swagger

Check the mapped port first:

```bash
docker compose -f docker-compose-v3.yml ps
```

Then open Swagger based on the **host port mapping**:

* If you see `0.0.0.0:8080->8080/tcp`
  → `http://localhost:8080/swagger`

* If you see something like `0.0.0.0:5000->8080/tcp`
  → `http://localhost:5000/swagger`

> Note: For local dev, Swagger is typically on **HTTP** (not HTTPS) unless HTTPS was explicitly configured in the container.

---

## Daily Development Workflow

### Start services (normal day)

```bash
docker compose -f docker-compose-v3.yml up -d
```

### Stop services (keep data/volumes)

```bash
docker compose -f docker-compose-v3.yml stop
```

### View logs (follow)

API logs:

```bash
docker compose -f docker-compose-v3.yml logs -f api
```

AI logs:

```bash
docker compose -f docker-compose-v3.yml logs -f ai
```

All services:

```bash
docker compose -f docker-compose-v3.yml logs -f
```

### Restart a single service

```bash
docker compose -f docker-compose-v3.yml restart api
```

### Rebuild after code changes (when needed)

If your compose build copies code into the image, rebuild:

```bash
docker compose -f docker-compose-v3.yml up -d --build api
```

If everything needs rebuild:

```bash
docker compose -f docker-compose-v3.yml up -d --build
```

> If your setup uses bind mounts (code mounted into container), you may only need a restart, not rebuild.

---

## Useful Commands

### Exec into a container shell

```bash
docker compose -f docker-compose-v3.yml exec api sh
```

(If the container supports bash)

```bash
docker compose -f docker-compose-v3.yml exec api bash
```

### Check environment and ports quickly

```bash
docker compose -f docker-compose-v3.yml ps
docker compose -f docker-compose-v3.yml config
```

### Health checks

If the API exposes health endpoints (common):

* `http://localhost:<PORT>/health`
* `http://localhost:<PORT>/health/ready`
* `http://localhost:<PORT>/health/live`

---

## Clean Up / Reset

### Bring down containers (keep volumes)

```bash
docker compose -f docker-compose-v3.yml down
```

### Bring down and remove volumes (FULL RESET – deletes DB/data)

⚠️ This will delete persisted data.

```bash
docker compose -f docker-compose-v3.yml down -v
```

### Remove unused images/build cache (optional)

```bash
docker system prune
```

---

## Common Troubleshooting

### 1) Swagger not reachable

* Check host port mapping:

  ```bash
  docker compose -f docker-compose-v3.yml ps
  ```

* Use **HTTP**, not HTTPS, unless HTTPS is configured.

### 2) Port is already allocated

Something else is using the port on your machine.

**Windows**

```powershell
netstat -ano | findstr :8080
```

Stop the conflicting process or change the mapped port in the compose file.

### 3) Changes not reflected

* Code baked into image → rebuild:

  ```bash
  docker compose -f docker-compose-v3.yml up -d --build api
  ```

* Code mounted → restart:

  ```bash
  docker compose -f docker-compose-v3.yml restart api
  ```

### 4) Database / migrations issues

* Check API logs:

  ```bash
  docker compose -f docker-compose-v3.yml logs -f api
  ```

* Full reset if needed:

  ```bash
  docker compose -f docker-compose-v3.yml down -v
  docker compose -f docker-compose-v3.yml up -d --build
  ```

---

## Suggested Dev Routine

**Morning**

```bash
docker compose -f docker-compose-v3.yml up -d
docker compose -f docker-compose-v3.yml logs -f api
```

**After pulling new changes**

```bash
docker compose -f docker-compose-v3.yml up -d --build
```

**End of day**

```bash
docker compose -f docker-compose-v3.yml stop
```

---

## Notes / Conventions

* Prefer `docker compose` (Compose v2) over `docker-compose`
* Always run commands from `infrastructure/docker/`
* After changing compose config:

  ```bash
  docker compose -f docker-compose-v3.yml up -d --build
  ```