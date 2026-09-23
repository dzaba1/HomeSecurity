# Build context is the repo root (not this project's own folder or even
# src/dotnet/), because Dzaba.HomeSecurity.Org.Contracts imports
# build/JsonSchema.targets and reads schema files from src/contracts/json/ -
# both outside src/dotnet/. See docker-compose.yml's `build.context` for the
# org-service entry.

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

# Imported directly by the Contracts project file, so it must exist before
# that csproj is even evaluated - i.e. before restore, not just build.
COPY ["build/JsonSchema.targets", "build/"]

COPY ["src/dotnet/Directory.Build.props", "src/dotnet/Directory.Packages.props", "src/dotnet/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Domain/Dzaba.HomeSecurity.Domain.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Domain/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Observability/Dzaba.HomeSecurity.Observability.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Observability/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.WebApi/Dzaba.HomeSecurity.WebApi.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.WebApi/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Authorization/Dzaba.HomeSecurity.Authorization.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Authorization/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Caching.Contracts/Dzaba.HomeSecurity.Caching.Contracts.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Caching.Contracts/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Caching.Redis/Dzaba.HomeSecurity.Caching.Redis.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Caching.Redis/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.DbServer/Dzaba.HomeSecurity.DbServer.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.DbServer/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.Contracts/Dzaba.HomeSecurity.MessageBroker.Contracts.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.Contracts/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.RabbitMQ/Dzaba.HomeSecurity.MessageBroker.RabbitMQ.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.RabbitMQ/"]
COPY ["src/dotnet/Data/Dzaba.HomeSecurity.Data/Dzaba.HomeSecurity.Data.csproj", "src/dotnet/Data/Dzaba.HomeSecurity.Data/"]
COPY ["src/dotnet/Org/Dzaba.HomeSecurity.Org.Contracts/Dzaba.HomeSecurity.Org.Contracts.csproj", "src/dotnet/Org/Dzaba.HomeSecurity.Org.Contracts/"]
COPY ["src/dotnet/Org/Dzaba.HomeSecurity.Org.Service/Dzaba.HomeSecurity.Org.Service.csproj", "src/dotnet/Org/Dzaba.HomeSecurity.Org.Service/"]
RUN dotnet restore "src/dotnet/Org/Dzaba.HomeSecurity.Org.Service/Dzaba.HomeSecurity.Org.Service.csproj"

# The Contracts project generates C# from these at build time (NSwag), so
# they're needed before `dotnet build`, not just restore.
COPY src/contracts/json/ src/contracts/json/
COPY src/dotnet/ src/dotnet/

WORKDIR "/source/src/dotnet/Org/Dzaba.HomeSecurity.Org.Service"
RUN dotnet build "Dzaba.HomeSecurity.Org.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Dzaba.HomeSecurity.Org.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dzaba.HomeSecurity.Org.Service.dll"]
