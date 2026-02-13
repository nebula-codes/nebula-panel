# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore (excludes test projects)
COPY src/NebulaPanel.Domain/NebulaPanel.Domain.csproj src/NebulaPanel.Domain/
COPY src/NebulaPanel.Application/NebulaPanel.Application.csproj src/NebulaPanel.Application/
COPY src/NebulaPanel.Infrastructure/NebulaPanel.Infrastructure.csproj src/NebulaPanel.Infrastructure/
COPY src/NebulaPanel.Web/NebulaPanel.Web.csproj src/NebulaPanel.Web/
COPY src/NebulaPanel.Shared/NebulaPanel.Shared.csproj src/NebulaPanel.Shared/
COPY src/NebulaPanel.Updater/NebulaPanel.Updater.csproj src/NebulaPanel.Updater/

# Restore dependencies for Web and Updater projects
RUN dotnet restore src/NebulaPanel.Web/NebulaPanel.Web.csproj
RUN dotnet restore src/NebulaPanel.Updater/NebulaPanel.Updater.csproj

# Copy source code
COPY src/ src/

# Download Monaco Editor for local serving (no CDN dependency at runtime)
RUN mkdir -p src/NebulaPanel.Web/wwwroot/lib/monaco-editor \
    && curl -sL -o src/NebulaPanel.Web/wwwroot/lib/require.min.js \
      https://cdnjs.cloudflare.com/ajax/libs/require.js/2.3.6/require.min.js \
    && curl -sL -o /tmp/monaco.tgz \
      https://registry.npmjs.org/monaco-editor/-/monaco-editor-0.45.0.tgz \
    && tar -xzf /tmp/monaco.tgz -C /tmp \
    && cp -r /tmp/package/min src/NebulaPanel.Web/wwwroot/lib/monaco-editor/min \
    && rm -rf /tmp/monaco.tgz /tmp/package

# Build argument for version
ARG VERSION=1.0.0

# Publish Web application (restore required for Blazor static web assets)
RUN dotnet publish src/NebulaPanel.Web/NebulaPanel.Web.csproj \
    --configuration Release \
    -p:Version=${VERSION} \
    --output /app/publish

# Publish Updater to separate directory to avoid overwriting Web static assets
RUN dotnet publish src/NebulaPanel.Updater/NebulaPanel.Updater.csproj \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION} \
    --output /app/updater

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install dependencies for game server management
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    unzip \
    tar \
    procps \
    gosu \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security (use high UID/GID to avoid conflicts)
RUN groupadd --gid 10000 nebula \
    && useradd --uid 10000 --gid nebula --shell /bin/bash --create-home nebula

# Create directories for data persistence
RUN mkdir -p /app/data /app/servers /app/logs \
    && chown -R nebula:nebula /app

# Copy published Web application
COPY --from=build --chown=nebula:nebula /app/publish .

# Copy Updater executable alongside the Web app
COPY --from=build --chown=nebula:nebula /app/updater/NebulaPanel.Updater* ./

# Copy entrypoint script (runs as root to fix Docker socket permissions, then drops to nebula)
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__ConnectionString="Data Source=/app/data/nebula.db"

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Entry point (entrypoint script handles privilege drop via gosu)
ENTRYPOINT ["/app/docker-entrypoint.sh", "dotnet", "NebulaPanel.Web.dll"]
