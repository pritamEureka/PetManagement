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

## Features

### Identity, accounts, and access control

- **Registration and login** - Email/password authentication with JWT access tokens, refresh tokens, protected routes, and seeded Super Admin access.
- **User onboarding** - First-run user flow for completing profile setup after account creation.
- **User profiles and dashboards** - Profile pages, user dashboard, role-specific vet and store dashboards, and account status handling.
- **Dynamic RBAC** - Permission-based authorization using `{module}.{action}` codes, role assignment, and guarded API/frontend routes.
- **System roles** - Built-in roles for SuperAdmin, Admin, Moderator, SupportAgent, Veterinarian, StoreOwner, Seller, ServiceProvider, Breeder, AdoptionCenter, DeliveryUser, and User.
- **User approval and suspension** - Admin workflows for approving, rejecting, suspending, restoring, and viewing users.
- **Security controls** - Device tracking, OTP/two-factor services, rate limiting, security headers, suspension guard middleware, and centralized exception handling.

### Pet management

- **Pet profiles** - Create, view, edit, and delete pet records for pet owners.
- **Pet detail records** - Dedicated pet detail pages backed by the pets API and domain model.
- **Digital health foundation** - Pet records are modeled as part of the ecosystem for appointments, prescriptions, and owner history.

### Social feed

- **Post feed** - Authenticated social feed for pet-related posts with location/content search support.
- **Post creation and editing** - Dialog-driven create/edit flows for user posts.
- **Post details** - Individual post pages with comments and interactions.
- **Comments and reactions** - Comment thread and drawer UI with reaction support in the backend domain.
- **Saved posts and my posts** - Personal collections for saved content and posts created by the current user.
- **Feed reporting and moderation** - Users can report posts; admins/moderators can review and moderate feed content.
- **Feed caching and denormalization** - Redis-backed feed cache plus worker support for denormalized feed projections.

### Adoption

- **Adoption marketplace** - Browse adoption listings with detail pages and approval status support.
- **Create and manage listings** - Create, edit, publish/unpublish, save, and mark adoption listings as adopted.
- **My listings and saved listings** - Dedicated pages for owned adoption listings and saved adoption items.
- **Adoption requests** - Users can submit adoption request forms and communicate with listing owners.
- **Adoption approvals** - Admin approval/rejection workflow for adoption listings.
- **Adoption events** - Kafka-backed adoption events and approval topics for workflow integration.

### Messaging and real-time communication

- **Direct messaging** - Conversation and message APIs with a full messaging page in the frontend.
- **SignalR chat hub** - Real-time chat via `/hubs/chat`.
- **Presence and typing indicators** - Presence dot and typing indicator components backed by presence services.
- **Read receipts and unread counts** - Message read receipt domain model and unread message hook.
- **Attachments** - Message attachment upload component and storage-backed media handling.
- **Message moderation and reporting** - Report message dialog plus moderation services for abuse handling.

### Veterinary care

- **Vet discovery** - Browse veterinarians and view doctor detail profiles.
- **Doctor registration** - Veterinarian profile registration and credential document support.
- **Doctor approval** - Admin approval, rejection, and suspension workflow for doctors.
- **Availability management** - Veterinarians can manage schedules and appointment availability.
- **Appointment booking** - Slot picker and booking flow for users to schedule vet appointments.
- **My appointments** - User-facing appointments page with status badges and appointment details.
- **Prescription uploads** - Vet prescription upload workflow tied to appointments.
- **Appointment reminders** - Worker job support for appointment reminder dispatching.

### Marketplace and stores

- **Product marketplace** - Storefront with product listing and product detail pages.
- **Cart and checkout** - Zustand cart store, cart page, checkout page, and order creation flow.
- **Orders** - Customer order list and order detail pages.
- **Address book** - Shipping address management for checkout and account use.
- **Store registration** - Seller/store onboarding with approval workflow.
- **Seller product management** - Create, edit, publish/unpublish, import/export, and manage products.
- **Inventory management** - Store inventory management page and inventory service.
- **Store order management** - Seller-facing order management dashboard.
- **Commissions** - Configurable marketplace commission settings for admins.
- **Reviews** - Store/product review domain support and admin review moderation.

### Notifications

- **In-app notifications** - Notification model, notifications page, unread counts, and notification API.
- **SignalR notification hub** - Real-time notifications via `/hubs/notifications`.
- **Admin notifications** - Admin page for notification management.
- **Notification dispatch worker** - Background job for dispatching notification events.

### Admin and moderation

- **Admin dashboard** - Dedicated admin layout, dashboard, and protected admin navigation.
- **Unified approvals inbox** - Cross-module approval requests for users, adoption, vets, stores, and moderation flows.
- **User management** - Create, view, edit, assign roles, suspend, restore, import/export-capable permission model.
- **Role and permission management** - Role pages, permission matrix, assignment dialog, and database-seeded permission catalog.
- **Reported content** - Abuse report review and moderation action workflows.
- **Feed moderation** - Admin feed moderation page for post review and action.
- **Product moderation** - Product publishing, featuring, and moderation controls.
- **Review moderation** - Review moderation page and permissions.
- **Order and appointment administration** - Admin views for orders and appointments.
- **Categories and settings** - Admin category management and system settings pages.
- **Analytics and reports** - Admin analytics/reports surfaces with export-oriented permissions.
- **Audit logs** - Audit entries, audit controller, admin audit log page, and audit event worker support.

### Platform, integrations, and operations

- **PostgreSQL persistence** - EF Core 9, Npgsql, snake_case naming, migrations, global soft delete filters, audit fields, and JSONB columns.
- **Full-text search** - Generated `tsvector` columns and GIN indexes for posts and products.
- **Redis caching** - Session, permission, notification count, OTP, feed, marketplace, and doctor availability caches.
- **Kafka event streaming** - Domain event topics for users, RBAC, feed, adoption, messaging, vets, appointments, marketplace, notifications, and audit.
- **Outbox pattern** - Reliable event publishing through persisted outbox messages and dispatcher job.
- **Background worker** - Worker service for order projections, notification dispatch, feed denormalization, audit consumption, and appointment reminders.
- **Object storage** - S3-compatible storage via MinIO for media, documents, attachments, and uploads.
- **API versioning and Swagger** - Versioned API controllers and local Swagger endpoint.
- **Health checks and monitoring** - API health endpoint, Prometheus config, Kafka UI, Redis Insight, and container health checks.
- **Containerized local stack** - Docker Compose for Postgres, Redis, Kafka, MinIO, API, worker, and optional web container.
- **Kubernetes manifests** - Base and environment overlays for dev, staging, and production deployments.
- **Frontend architecture** - React 18, TypeScript, Vite, React Query, shadcn/ui, Tailwind CSS, Zod forms, SignalR hooks, and protected route guards.

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
| Comments      | `/api/comments`        |
| Saved posts   | `/api/saved-posts`     |
| Adoption      | `/api/adoption`        |
| Adoption reqs | `/api/adoption-requests` |
| Messaging     | `/api/messages`        |
| Vets          | `/api/vets`            |
| Appointments  | `/api/appointments`    |
| Stores        | `/api/stores`          |
| Products      | `/api/products`        |
| Cart          | `/api/cart`            |
| Orders        | `/api/orders`          |
| Addresses     | `/api/shipping-addresses` |
| Notifications | `/api/notifications`   |
| Security      | `/api/security`        |
| Moderation    | `/api/moderation`      |
| Reports       | `/api/reports`         |
| Media         | `/api/media`           |
| Audit         | `/api/audit`           |
| Services      | `/api/services`        |
| Admin         | `/api/admin/*`         |
| SignalR hubs  | `/hubs/chat`, `/hubs/notifications` |

## Permission model

`{module}.{action}` (e.g. `posts.create`, `adoption.approve`, `stores.refund`). Roles are bags of permissions; users can hold multiple roles. Enforced via `[Permission("module.action")]` attribute backed by an `IAuthorizationPolicyProvider`.

## Status

This is the scaffolded foundation, now expanded with working domain modules across identity, pets, feed, adoption, messaging, vets, marketplace, notifications, moderation, admin, workers, and deployment. Each major module is wired end-to-end (entity → DbContext → controller/service → frontend page) and can continue to be fleshed out feature-by-feature in subsequent iterations.
