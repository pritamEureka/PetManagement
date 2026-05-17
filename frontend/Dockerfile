# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Pawzaroo Web (React + Vite) — npm ci with mount cache, nginx unprivileged.
# ---------------------------------------------------------------------------
FROM node:20-alpine AS deps
WORKDIR /app

# Separate copy of package files keeps the npm install layer cacheable.
COPY frontend/package.json frontend/package-lock.json* ./
RUN --mount=type=cache,target=/root/.npm \
    if [ -f package-lock.json ]; then npm ci --no-audit --no-fund; \
    else npm install --no-audit --no-fund; fi

FROM deps AS build
COPY frontend/ ./
ARG VITE_API_BASE=/api
ARG VERSION=0.0.0
ARG GIT_SHA=local
ENV VITE_API_BASE=${VITE_API_BASE} \
    VITE_APP_VERSION=${VERSION} \
    VITE_GIT_SHA=${GIT_SHA}
RUN npm run build

# nginxinc/nginx-unprivileged listens on 8080 by default and runs as uid 101.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
ARG VERSION=0.0.0
ARG GIT_SHA=local
LABEL org.opencontainers.image.title="pawzaroo-web" \
      org.opencontainers.image.source="https://github.com/your-org/pawzaroo" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}"

COPY --chown=nginx:nginx --from=build /app/dist /usr/share/nginx/html
COPY --chown=nginx:nginx frontend/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1
