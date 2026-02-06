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

# Build argument for version
ARG VERSION=1.0.0

# Publish Web application
RUN dotnet publish src/NebulaPanel.Web/NebulaPanel.Web.csproj \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION} \
    --output /app/publish

# Publish Updater
RUN dotnet publish src/NebulaPanel.Updater/NebulaPanel.Updater.csproj \
    --configuration Release \
    --no-restore \
    -p:Version=${VERSION} \
    --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install dependencies for game server management
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    unzip \
    tar \
    procps \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security (use high UID/GID to avoid conflicts)
RUN groupadd --gid 10000 nebula \
    && useradd --uid 10000 --gid nebula --shell /bin/bash --create-home nebula

# Create directories for data persistence
RUN mkdir -p /app/data /app/servers /app/logs \
    && chown -R nebula:nebula /app

# Copy published application
COPY --from=build --chown=nebula:nebula /app/publish .

# Switch to non-root user
USER nebula

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__ConnectionString="Data Source=/app/data/nebula.db"

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "NebulaPanel.Web.dll"]
