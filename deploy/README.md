# Pawzaroo Deployment Guide

End-to-end commands and conventions for running Pawzaroo from a laptop up to
production Kubernetes. Read this before the first deploy.

---

## Layout

```
deploy/
├── docker-compose.yml          # Local dev stack (Postgres, Redis, Kafka, MinIO, API, Worker, Web)
├── docker-compose.override.yml # Dev-only patches (loaded automatically)
├── prometheus.yml              # Metrics scrape config used by the compose stack
└── k8s/
    ├── base/                   # Canonical manifests (any env)
    │   ├── namespace.yaml
    │   ├── configmap.yaml
    │   ├── secret.yaml         # template only — do NOT commit real secrets
    │   ├── serviceaccount.yaml
    │   ├── postgres.yaml | redis.yaml | kafka.yaml | minio.yaml
    │   ├── api.deployment.yaml | api.service.yaml | api.hpa.yaml
    │   ├── worker.deployment.yaml
    │   ├── web.deployment.yaml
    │   ├── ingress.yaml
    │   ├── migration.job.yaml  # rendered with envsubst per deploy
    │   ├── smoketest.job.yaml  # rendered with envsubst per deploy
    │   └── kustomization.yaml
    └── overlays/
        ├── dev/        # 1 replica each, dev hostnames, Debug logging
        ├── staging/    # 2 replicas each, staging hostnames
        └── production/ # full replicas, managed PG/Kafka/S3 (in-cluster set to 0)
```

The unversioned top-level files (`api.yaml`, `web.yaml`, ...) are kept for
backward-compat with the old `kubectl apply -f deploy/k8s/` flow; new pipelines
target the Kustomize overlays.

---

## Local development (Docker Compose)

```bash
# One-time
cp .env.example .env

# Bring everything up — first run takes ~3 min for image builds.
docker compose -f deploy/docker-compose.yml up -d --build

# Tail the API + worker
docker compose -f deploy/docker-compose.yml logs -f api worker

# Tear down (keep volumes)
docker compose -f deploy/docker-compose.yml down

# Tear down + delete data
docker compose -f deploy/docker-compose.yml down -v
```

Useful endpoints once the stack is up:

| URL                          | What it is                       |
|------------------------------|----------------------------------|
| http://localhost:3000        | React app                        |
| http://localhost:8080/swagger | API Swagger UI                  |
| http://localhost:8085        | Kafka UI (Redpanda Console)      |
| http://localhost:8001        | RedisInsight                     |
| http://localhost:9001        | MinIO console                    |
| http://localhost:9090        | Prometheus                       |

---

## Kubernetes — first install

```bash
# 0. Point kubectl at the right cluster
kubectl config use-context <your-cluster>

# 1. Install ingress + cert-manager (one-time per cluster)
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/cloud/deploy.yaml
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

# 2. Render + apply via Kustomize (dev as example)
kubectl apply -k deploy/k8s/overlays/dev

# 3. Replace placeholder secrets BEFORE running any real traffic
kubectl -n pawzaroo-dev create secret generic pawzaroo-secrets \
  --from-literal=POSTGRES_PASSWORD="$(openssl rand -base64 24)" \
  --from-literal=JWT_SIGNING_KEY="$(openssl rand -base64 48)" \
  --from-literal=STORAGE_ACCESS_KEY=minioadmin \
  --from-literal=STORAGE_SECRET_KEY=minioadmin \
  --dry-run=client -o yaml | kubectl apply -f -
```

For staging / production, swap `overlays/dev` for the right overlay path.

---

## CI/CD

### GitHub Actions

| Workflow              | Triggers                                          | What it does                                                                                                |
|-----------------------|---------------------------------------------------|-------------------------------------------------------------------------------------------------------------|
| `.github/workflows/ci.yml`      | push / PR to `main`,`develop`            | restore → lint → build → test → security (Gitleaks, Trivy, CodeQL)                                          |
| `.github/workflows/release.yml` | push to `main`, push tag `v*`, manual run | build & push 3 images → `deploy-dev` (auto) → `deploy-staging` (gate) → `deploy-production` (gate, tag only) |

Required repo secrets (`Settings → Secrets and variables → Actions`):

- `KUBECONFIG_DEV` / `KUBECONFIG_STAGING` / `KUBECONFIG_PROD` — base64-encoded kubeconfig files

Configure required reviewers for the `staging` and `production`
**Environments** so deploys pause for human approval.

### GitLab CI/CD

`.gitlab-ci.yml` mirrors the GitHub flow. Required project variables:
`KUBECONFIG_DEV`, `KUBECONFIG_STAGING`, `KUBECONFIG_PROD` (each a file-type
variable containing base64-encoded kubeconfig).

Staging and production both fire via **manual** jobs in the pipeline view.

---

## Database migrations

Migrations are baked into the API image — the same binary runs as the API or
as a one-shot migrator:

```bash
# Inside the cluster (the deploy action does this automatically):
kubectl -n pawzaroo apply -f deploy/k8s/base/migration.job.yaml
kubectl -n pawzaroo wait --for=condition=complete --timeout=10m job/db-migrate-<TAG>

# Locally (without containers):
cd backend
dotnet ef database update --project src/Pawzaroo.Infrastructure --startup-project src/Pawzaroo.Api
```

Migrations are written to be idempotent and forward-only; never edit a
committed migration after it has shipped to dev.

---

## Smoke tests

Run automatically after every deploy via `deploy/k8s/base/smoketest.job.yaml`.
To run manually:

```bash
export IMAGE_TAG=$(git rev-parse --short HEAD)
envsubst < deploy/k8s/base/smoketest.job.yaml | kubectl apply -f -
kubectl -n pawzaroo wait --for=condition=complete --timeout=5m job/smoke-test-$IMAGE_TAG
kubectl -n pawzaroo logs job/smoke-test-$IMAGE_TAG
```

---

## Rollback

The deploy pipeline auto-rolls back on smoke-test failure. For a manual
rollback:

```bash
# 1. Look up the rollout history
kubectl -n pawzaroo rollout history deploy/api

# 2. Revert to the previous ReplicaSet
kubectl -n pawzaroo rollout undo deploy/api
kubectl -n pawzaroo rollout undo deploy/worker
kubectl -n pawzaroo rollout undo deploy/web

# 3. Confirm
kubectl -n pawzaroo rollout status deploy/api
```

If a bad migration is the culprit:

```bash
# Down-migrate to the previous EF migration name. Treat as last-resort —
# prefer fixing forward with a new migration.
kubectl -n pawzaroo run efrollback --rm -it --restart=Never \
  --image=ghcr.io/your-org/pawzaroo-api:<PREVIOUS-TAG> \
  -- dotnet Pawzaroo.Api.dll --migrate-to <MigrationName>
```

---

## Operational handles

- **Scale API up:** `kubectl -n pawzaroo scale deploy/api --replicas=10`
- **Force HPA evaluation:** `kubectl -n pawzaroo describe hpa api`
- **Drain a node safely:** the API+Worker PDBs keep at least 2 / 1 replicas up
- **Shell into a pod:** `kubectl -n pawzaroo exec -it deploy/api -- /bin/sh`
- **Tail logs:** `kubectl -n pawzaroo logs -l app=api -f --max-log-requests=10`

---

## Environment matrix

| Env        | Replicas (api / worker / web) | DB                | Kafka      | Storage     | Ingress host                   |
|------------|-------------------------------|-------------------|------------|-------------|--------------------------------|
| local      | 1 / 1 / 1                     | in-compose PG     | in-compose | MinIO       | http://localhost:3000          |
| dev        | 1 / 1 / 1                     | in-cluster PG     | in-cluster | MinIO       | https://dev.pawzaroo.example.com |
| staging    | 2 / 2 / 2                     | in-cluster PG     | in-cluster | MinIO       | https://staging.pawzaroo.example.com |
| production | 5 / 3 / 3 (HPA up to 30 / – / 12) | managed (RDS) | managed (Strimzi/MSK) | S3 | https://pawzaroo.example.com |

---

## Secret management

Three supported flows — pick one per environment:

1. **External Secrets Operator** (recommended) — point at AWS Secrets Manager,
   GCP Secret Manager, Vault, or 1Password Connect. Commit `ExternalSecret`
   manifests; the operator materializes K8s `Secret` resources.
2. **Sealed Secrets** — encrypt secrets with the cluster's public key and
   commit the encrypted YAML.
3. **CI injection** — render `secret.yaml` from CI variables with `envsubst`
   and `kubectl apply` on each deploy.

The placeholder `deploy/k8s/base/secret.yaml` exists so `kustomize build`
succeeds locally; replace it before applying to any real cluster.
