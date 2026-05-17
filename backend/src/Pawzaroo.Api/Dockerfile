# syntax=docker/dockerfile:1.7
# ---------------------------------------------------------------------------
# Pawzaroo.Api — multi-stage build
#   1. restore  : NuGet restore in its own layer for cache hits
#   2. build    : compile + publish
#   3. runtime  : aspnet:9.0 with a dedicated non-root user
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

# Copy only the bits that affect restore so the layer cache invalidates as
# rarely as possible. Source files change far more often than csproj files.
COPY backend/Directory.Build.props ./
COPY backend/Pawzaroo.sln ./
COPY backend/src/Pawzaroo.Shared/Pawzaroo.Shared.csproj           ./src/Pawzaroo.Shared/
COPY backend/src/Pawzaroo.Domain/Pawzaroo.Domain.csproj           ./src/Pawzaroo.Domain/
COPY backend/src/Pawzaroo.Application/Pawzaroo.Application.csproj ./src/Pawzaroo.Application/
COPY backend/src/Pawzaroo.Infrastructure/Pawzaroo.Infrastructure.csproj ./src/Pawzaroo.Infrastructure/
COPY backend/src/Pawzaroo.Api/Pawzaroo.Api.csproj                 ./src/Pawzaroo.Api/
COPY backend/src/Pawzaroo.Worker/Pawzaroo.Worker.csproj           ./src/Pawzaroo.Worker/
COPY backend/tests/ ./tests/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore Pawzaroo.sln

FROM restore AS build
COPY backend/src/ ./src/
ARG VERSION=0.0.0
ARG GIT_SHA=local
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Pawzaroo.Api/Pawzaroo.Api.csproj \
      -c Release -o /app/publish \
      /p:UseAppHost=false \
      /p:Version=${VERSION} \
      /p:SourceRevisionId=${GIT_SHA}

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ARG VERSION=0.0.0
ARG GIT_SHA=local
LABEL org.opencontainers.image.title="pawzaroo-api" \
      org.opencontainers.image.source="https://github.com/your-org/pawzaroo" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}" \
      org.opencontainers.image.licenses="Proprietary"

# Non-root user. App writes nothing under /app at runtime so chown is enough.
RUN groupadd --system --gid 1001 app \
 && useradd --system --uid 1001 --gid app --no-create-home --shell /sbin/nologin app
COPY --from=build --chown=app:app /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Pawzaroo.Api.dll"]
