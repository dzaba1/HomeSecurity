# Build context is the repo root (not this project's own folder or even
# src/dotnet/), like every other service: shared code and, later, schema files
# live outside this project's folder. See docker-compose.yml's `build.context`
# for the audit-service entry.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# The runtime image ships no HTTP client tool; curl is needed for
# docker-compose's healthcheck against /health/ready. Installed as root,
# before switching to the non-root $APP_UID user below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

COPY ["src/dotnet/Directory.Build.props", "src/dotnet/Directory.Packages.props", "src/dotnet/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Domain/Dzaba.HomeSecurity.Domain.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Domain/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Observability/Dzaba.HomeSecurity.Observability.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Observability/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.WebApi/Dzaba.HomeSecurity.WebApi.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.WebApi/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Authorization/Dzaba.HomeSecurity.Authorization.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Authorization/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Caching.Contracts/Dzaba.HomeSecurity.Caching.Contracts.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Caching.Contracts/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.DbServer/Dzaba.HomeSecurity.DbServer.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.DbServer/"]
COPY ["src/dotnet/Audit/Dzaba.HomeSecurity.Audit.Service/Dzaba.HomeSecurity.Audit.Service.csproj", "src/dotnet/Audit/Dzaba.HomeSecurity.Audit.Service/"]
RUN dotnet restore "src/dotnet/Audit/Dzaba.HomeSecurity.Audit.Service/Dzaba.HomeSecurity.Audit.Service.csproj"

COPY src/dotnet/ src/dotnet/

WORKDIR "/source/src/dotnet/Audit/Dzaba.HomeSecurity.Audit.Service"
RUN dotnet build "Dzaba.HomeSecurity.Audit.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Dzaba.HomeSecurity.Audit.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dzaba.HomeSecurity.Audit.Service.dll"]
