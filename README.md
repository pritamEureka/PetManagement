# Pawzaroo

Integrated pet & domestic animal management ecosystem: social feed, adoption, marketplace, vet booking, real-time chat, digital pet health records, and a dynamic RBAC admin.

## Stack

| Layer            | Tech                                                |
|------------------|------------------------------------------------------|
| Backend API      | .NET 9 / ASP.NET Core Web API                       |
| ORM              | EF Core 9 + Npgsql                                   |
| Database         | PostgreSQL 16                                        |
| Cache            | Redis 7                                              |
| Event streaming  | Apache Kafka (KRaft mode)                            |
| Real-time        | SignalR (ChatHub, NotificationHub)                   |
| Auth             | JWT access + refresh tokens, dynamic RBAC            |
| Frontend         | React 18 + TypeScript + Vite                         |
| UI               | shadcn/ui + Tailwind CSS                             |
| Deploy           | Docker, docker-compose, Kubernetes manifests         |
| CI/CD            | GitHub Actions                                       |

## Layout

```
PetManagement/
  backend/      .NET 9 solution (Domain / Application / Infrastructure / API)
  frontend/    React + TS + Vite + shadcn
  deploy/      docker-compose, k8s manifests, ci pipelines
```

## Local development

Prereqs: Docker Desktop, .NET 9 SDK, Node 20+.

```bash
# 1. spin up postgres + redis + kafka
cd deploy
docker compose up -d postgres redis kafka

# 2. backend
cd ../backend
dotnet restore
dotnet ef database update --project src/Pawzaroo.Infrastructure --startup-project src/Pawzaroo.Api
dotnet run --project src/Pawzaroo.Api
# -> https://localhost:5001/swagger

# 3. frontend
cd ../frontend
npm install
npm run dev
# -> http://localhost:5173
```

Run everything in containers:

```bash
cd deploy
docker compose up --build
```

## Seeded admin

On first run the database is seeded with a Super Admin:

```
email:    superadmin@pawzaroo.local
password: Admin@12345
```

Change the password after first login.

## EF Core migrations

All migrations live in `backend/src/Pawzaroo.Infrastructure/Persistence/Migrations` and use snake_case names (enforced by `EFCore.NamingConventions`).

```bash
# from backend/
dotnet tool install --global dotnet-ef        # one-time, if not installed
export PROJ=src/Pawzaroo.Infrastructure
export START=src/Pawzaroo.Api

# add a new migration
dotnet ef migrations add Init --project $PROJ --startup-project $START -o Persistence/Migrations

# apply pending migrations against the configured Postgres connection
dotnet ef database update --project $PROJ --startup-project $START

# generate an idempotent SQL script for prod rollout
dotnet ef migrations script --idempotent --project $PROJ --startup-project $START -o ./db.sql

# revert to a specific migration
dotnet ef database update PreviousMigrationName --project $PROJ --startup-project $START

# drop the database (dev only)
dotnet ef database drop --project $PROJ --startup-project $START -f
```

PowerShell equivalent:

```powershell
$env:PROJ = "src/Pawzaroo.Infrastructure"; $env:START = "src/Pawzaroo.Api"
dotnet ef migrations add Init --project $env:PROJ --startup-project $env:START -o Persistence/Migrations
dotnet ef database update      --project $env:PROJ --startup-project $env:START
```

## Database conventions

| Concern             | Approach                                                                                              |
|---------------------|-------------------------------------------------------------------------------------------------------|
| PKs                 | `uuid` generated client-side (`Guid.NewGuid()`).                                                       |
| Naming              | `snake_case` tables and columns via `UseSnakeCaseNamingConvention()`.                                  |
| Soft delete         | `AuditableEntity.IsDeleted` + global query filter; deletes are flipped to updates in `SaveChanges`.    |
| Audit fields        | `CreatedAt/By`, `UpdatedAt/By`, `DeletedAt` auto-populated from `ICurrentUserService`.                 |
| Money               | `decimal(12,2)` via `HasPrecision`.                                                                    |
| Timestamps          | UTC `timestamp with time zone` (Npgsql default for `DateTime`).                                        |
| JSONB               | `PreferencesJson`, `PayloadJson`, `ItemsJson`, `ValueJson` mapped via `HasColumnType("jsonb")`.        |
| Full-text search    | `tsvector` generated columns on `posts(content,location)` and `products(name,description)` with GIN.   |
| RBAC                | Dynamic permissions; `[Permission("module.action")]` resolved by `PermissionPolicyProvider`.           |
| Approval workflow   | Per-module `ApprovalStatus` for hot reads, `approval_requests` table for the unified admin inbox.      |

## API surface (high level)

| Area          | Base route             |
|---------------|------------------------|
| Auth          | `/api/auth`            |
| Users         | `/api/users`           |
| Pets          | `/api/pets`            |
| Posts (feed)  | `/api/posts`           |
| Adoption      | `/api/adoption`        |
| Messaging     | `/api/messages`        |
| Vets          | `/api/vets`            |
| Appointments  | `/api/appointments`    |
| Stores        | `/api/stores`          |
| Products      | `/api/products`        |
| Orders        | `/api/orders`          |
| Services      | `/api/services`        |
| Admin         | `/api/admin/*`         |
| SignalR hubs  | `/hubs/chat`, `/hubs/notifications` |

## Permission model

`{module}.{action}` (e.g. `posts.create`, `adoption.approve`, `store.refund`). Roles are bags of permissions; users can hold multiple roles. Enforced via `[Permission("module.action")]` attribute backed by an `IAuthorizationPolicyProvider`.

## Status

This is the scaffolded foundation. Each domain module is wired end-to-end (entity → DbContext → controller stub → frontend page stub) and ready to be fleshed out feature-by-feature in subsequent iterations.
