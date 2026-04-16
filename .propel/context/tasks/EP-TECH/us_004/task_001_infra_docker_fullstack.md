# Task - task_001_infra_docker_fullstack

## Requirement Reference
- User Story: [us_004] (.propel/context/tasks/EP-TECH/us_004/us_004.md)
- Story Location: `.propel/context/tasks/EP-TECH/us_004/us_004.md`
- Acceptance Criteria:
  - AC-2: Running `docker-compose up` starts PostgreSQL, Redis, and the application services together with proper networking and volume mounts.
  - AC-4 (partial): All Docker images used are free and open-source; no paid registry or proprietary base images per FR-020 and NFR-015.
- Edge Case:
  - When a Docker image pull fails: Docker Compose logs the specific image/registry error; services with `restart: unless-stopped` and `depends_on` health check conditions prevent cascading failures from a single image pull failure.

## Design References (Frontend Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **UI Impact** | No |
| **Figma URL** | N/A |
| **Wireframe Status** | N/A |
| **Wireframe Type** | N/A |
| **Wireframe Path/URL** | N/A |
| **Screen Spec** | N/A |
| **UXR Requirements** | N/A |
| **Design Tokens** | N/A |

## Applicable Technology Stack
| Layer | Technology | Version |
|-------|------------|---------|
| Container Runtime | Docker | 24.x |
| Compose | Docker Compose | 2.x (V2 CLI) |
| Database Image | pgvector/pgvector | pg15 |
| Cache Image | redis | 7-alpine |
| Backend Runtime | mcr.microsoft.com/dotnet/aspnet | 8.0 |
| Backend SDK | mcr.microsoft.com/dotnet/sdk | 8.0 |
| Frontend | node | 20-alpine |
| AI/ML | N/A | - |
| Mobile | N/A | - |

**Note:** All images MUST use official OSS images from Docker Hub or Microsoft Container Registry. No paid or private registry images.

## AI References (AI Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **AI Impact** | No |
| **AIR Requirements** | N/A |
| **AI Pattern** | N/A |
| **Prompt Template Path** | N/A |
| **Guardrails Config** | N/A |
| **Model Provider** | N/A |

## Mobile References (Mobile Tasks Only)
| Reference Type | Value |
|----------------|-------|
| **Mobile Impact** | No |
| **Platform Target** | N/A |
| **Min OS Version** | N/A |
| **Mobile Framework** | N/A |

## Task Overview

Extends the Docker Compose setup created in us_003/task_001 (PostgreSQL only) into a full four-service local development stack: `db` (PostgreSQL 15 + pgvector), `redis` (Redis 7), `api` (.NET 8 ASP.NET Core), and `frontend` (React 18 Vite dev server). Creates multi-stage `server/Dockerfile` (build → publish → runtime) and `client/Dockerfile.dev` (node dev server). Defines a shared Docker network (`propeliq-net`) with named volumes for data persistence and node_modules caching. After this task a developer must be able to clone the repository, run `docker compose up`, and reach the API health check and frontend in a browser — with no manual environment configuration beyond copying `.env.example` to `.env` per TR-024.

## Dependent Tasks
- us_003/task_001_db_postgres_pgvector_efcore — `docker-compose.yml` base file and postgres service definition must exist before this delta update.

## Impacted Components
- `server/docker-compose.yml` — EXTEND: add `redis`, `api`, `frontend` services; add named volumes and `propeliq-net` network
- `server/docker-compose.override.yml` — EXTEND: add Redis + frontend port mappings; update api service env var overrides for local dev
- `server/Dockerfile` — NEW: multi-stage .NET 8 build and runtime image
- `client/Dockerfile.dev` — NEW: Node 20 Vite dev server image
- `server/.dockerignore` — NEW: excludes `bin/`, `obj/`, `.env`, `*.user`
- `client/.dockerignore` — NEW: excludes `node_modules/`, `dist/`, `.env*`

## Implementation Plan

1. **Extend docker-compose.yml with redis service** — Add `redis` service using `redis:7-alpine` image with named volume `redis-data:/data`, `restart: unless-stopped`, health check (`redis-cli ping`), and connection to `propeliq-net`. Redis does NOT expose ports in the base compose file (only in the override).

2. **Add .NET API service** — Add `api` service with `build: { context: ./server, dockerfile: Dockerfile }` targeting the multi-stage server Dockerfile. Set:
   - `depends_on: { db: { condition: service_healthy }, redis: { condition: service_healthy } }`
   - Environment variables sourced from `.env` file: `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__DefaultConnection`, `Redis__ConnectionString=redis:6379,abortConnect=False`
   - `EXPOSE 8080` (internal) with port `8080:8080` in override
   - `restart: unless-stopped`
   - Named volume `api-logs:/app/logs` for structured Serilog output

3. **Add React frontend service** — Add `frontend` service with `build: { context: ./client, dockerfile: Dockerfile.dev }`. Set:
   - `depends_on: [api]`
   - Environment variable `VITE_API_BASE_URL=http://localhost:8080`
   - `restart: unless-stopped`
   - Port `3000:3000` in override
   - Named volume `node-modules:/app/node_modules` for npm install cache

4. **Define networking and volumes** — Add top-level `networks: { propeliq-net: { driver: bridge } }` and `volumes: { postgres-data:, redis-data:, api-logs:, node-modules: }`. Mark all four services to use `propeliq-net`. This isolates the stack from other local Docker networks.

5. **Create server/Dockerfile (multi-stage)** — Stage 1 `build`: `FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build`, `WORKDIR /src`, `COPY . .`, `dotnet restore PropelIQ.sln`, `dotnet publish src/PropelIQ.Api/PropelIQ.Api.csproj -c Release -o /app/publish`. Stage 2 `runtime`: `FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime`, `WORKDIR /app`, `COPY --from=build /app/publish .`, `EXPOSE 8080`, `ENV ASPNETCORE_URLS=http://+:8080`, `ENTRYPOINT ["dotnet", "PropelIQ.Api.dll"]`.

6. **Create client/Dockerfile.dev** — `FROM node:20-alpine`, `WORKDIR /app`, `COPY package*.json ./`, `RUN npm ci`, `COPY . .`, `EXPOSE 3000`, `CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "3000"]`. The `node_modules` volume in docker-compose.yml mounts over the container's `/app/node_modules` to avoid overwriting with host OS modules.

7. **Create .dockerignore files** — `server/.dockerignore`: exclude `**/bin/`, `**/obj/`, `.env`, `*.user`, `*.suo`, `.git/`. `client/.dockerignore`: exclude `node_modules/`, `dist/`, `.env*`, `.git/`. This prevents copying unnecessary files into the build context, reducing image build time and avoiding secret leaks (OWASP A02).

8. **Validate full stack start** — Run `docker compose up --build -d` from the repository root (or `server/`). Verify:
   - `docker compose ps` shows all four services as `running`
   - `curl http://localhost:8080/api/health` returns `{ "status": "Healthy" }`
   - Browser navigates to `http://localhost:3000` and loads the React app shell

## Current Project State

```
server/
├── docker-compose.yml           ← PostgreSQL + pgvector service only (from us_003/task_001)
├── docker-compose.override.yml  ← Port 5432 binding (from us_003/task_001)
├── .env.example                 ← POSTGRES_* env var placeholders (from us_003/task_001)
├── global.json
├── PropelIQ.sln
└── src/
    ├── PropelIQ.Api/            ← Program.cs + health check + Swagger configured
    ├── PropelIQ.PatientAccess.Data/
    │   ├── PropelIQDbContext.cs
    │   └── Migrations/
    └── (other modules)
client/
    ├── package.json             ← React 18 + Vite 5 + MUI 5 (from us_001/task_001)
    ├── vite.config.ts
    └── src/
```

## Expected Changes
| Action | File Path | Description |
|--------|-----------|-------------|
| MODIFY | `server/docker-compose.yml` | Add `redis` (redis:7-alpine), `api` (.NET 8 multi-stage build), `frontend` (node:20-alpine) services; add `propeliq-net` network and four named volumes |
| MODIFY | `server/docker-compose.override.yml` | Add port bindings for `redis` (6379), `api` (8080), `frontend` (3000); add dev env var overrides for `api` service |
| MODIFY | `server/.env.example` | Add `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD` (empty for local dev), `ASPNETCORE_ENVIRONMENT=Development` placeholders |
| CREATE | `server/Dockerfile` | Multi-stage .NET 8 image: sdk:8.0 build → aspnet:8.0 runtime; EXPOSE 8080 |
| CREATE | `client/Dockerfile.dev` | node:20-alpine Vite dev server; npm ci; CMD dev --host 0.0.0.0; EXPOSE 3000 |
| CREATE | `server/.dockerignore` | Excludes bin/, obj/, .env, *.user, .git/ from Docker build context |
| CREATE | `client/.dockerignore` | Excludes node_modules/, dist/, .env*, .git/ from Docker build context |

## External References
- Docker Compose V2 networking and volumes: https://docs.docker.com/compose/networking/
- Docker multi-stage builds (.NET): https://learn.microsoft.com/en-us/dotnet/core/docker/build-container
- pgvector Docker image: https://hub.docker.com/r/pgvector/pgvector (tag `pg15`)
- Redis 7 Alpine image: https://hub.docker.com/_/redis (tag `7-alpine`)
- Node 20 Alpine image: https://hub.docker.com/_/node (tag `20-alpine`)
- .NET SDK/runtime images: https://hub.docker.com/_/microsoft-dotnet (MCR)
- TR-024 (Docker for dev, IIS for prod), NFR-015 (OSS images only), NFR-014 (IIS production — Docker dev only)

## Build Commands
```bash
# Copy env example to .env and fill values
cp server/.env.example server/.env

# Start full stack (build images on first run)
docker compose -f server/docker-compose.yml -f server/docker-compose.override.yml up --build -d

# Check all services are running
docker compose -f server/docker-compose.yml ps

# Validate API health check
curl http://localhost:8080/api/health
# Expected: { "status": "Healthy", ..., "checks": { "postgresql": "Healthy", "redis": "Healthy" } }

# Validate frontend
# Open http://localhost:3000 in browser

# Tear down (preserves named volumes)
docker compose -f server/docker-compose.yml down

# Tear down + remove volumes (clean slate)
docker compose -f server/docker-compose.yml down -v
```

## Implementation Validation Strategy
- [ ] Unit tests pass
- [ ] `docker compose up --build -d` exits without error (all 4 services start)
- [ ] `docker compose ps` shows all four services as `running` (not `exiting` or `restarting`)
- [ ] `GET http://localhost:8080/api/health` returns `200 OK` with `"status": "Healthy"` and both db + redis checks present
- [ ] `http://localhost:3000` loads the React Vite shell in a browser
- [ ] `docker inspect propeliq-net` confirms all four services are on the same Docker network
- [ ] Named volumes `postgres-data` and `redis-data` persist data across `docker compose restart` (stop + start without `-v`)
- [ ] No secrets or credentials appear in Dockerfile or docker-compose.yml — all sensitive values come from `.env` (not committed per .gitignore); OWASP A02 compliance

## Implementation Checklist
- [x] Modify `server/docker-compose.yml`: add `redis` service (redis:7-alpine, health check, volume, network), `api` service (multi-stage Dockerfile, depends_on db+redis healthy, env from .env, propeliq-net), `frontend` service (node Dockerfile.dev, depends_on api, propeliq-net); add top-level `volumes` and `networks` blocks
- [x] Modify `server/docker-compose.override.yml`: add port bindings 6379→6379 (redis), 8080→8080 (api), 3000→3000 (frontend); add dev-specific `env_file: .env` to api service
- [x] Create `server/Dockerfile`: two-stage — sdk:8.0 build stage (dotnet restore PropelIQ.slnx + publish -c Release), aspnet:8.0 runtime stage (non-root user, COPY publish, EXPOSE 8080, ENTRYPOINT dotnet PropelIQ.Api.dll)
- [x] Create `client/Dockerfile.dev`: node:20-alpine, npm ci, EXPOSE 3000, CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
- [x] Create `server/.dockerignore` (exclude bin/, obj/, .env, *.user) and `client/.dockerignore` (exclude node_modules/, dist/, .env*)
- [x] Update `server/.env.example` with all required env var placeholders for all four services (POSTGRES_*, REDIS_PASSWORD, ASPNETCORE_ENVIRONMENT)
- [ ] Run `docker compose up --build -d` and verify all four services reach `running` state
- [ ] Verify `GET /api/health` returns Healthy with both postgresql and redis checks; verify `http://localhost:3000` loads frontend
