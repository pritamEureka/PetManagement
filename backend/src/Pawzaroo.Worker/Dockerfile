# syntax=docker/dockerfile:1.7
# Pawzaroo.Worker — multi-stage build with cached restore + non-root runtime.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src
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
    dotnet publish src/Pawzaroo.Worker/Pawzaroo.Worker.csproj \
      -c Release -o /app/publish \
      /p:UseAppHost=false \
      /p:Version=${VERSION} \
      /p:SourceRevisionId=${GIT_SHA}

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

ARG VERSION=0.0.0
ARG GIT_SHA=local
LABEL org.opencontainers.image.title="pawzaroo-worker" \
      org.opencontainers.image.source="https://github.com/your-org/pawzaroo" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}"

RUN groupadd --system --gid 1001 app \
 && useradd --system --uid 1001 --gid app --no-create-home --shell /sbin/nologin app
COPY --from=build --chown=app:app /app/publish ./

USER app
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "Pawzaroo.Worker.dll"]
