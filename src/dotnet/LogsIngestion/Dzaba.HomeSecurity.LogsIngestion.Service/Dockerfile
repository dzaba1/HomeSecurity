# Build context is the repo root (not this project's own folder or even
# src/dotnet/), because Dzaba.HomeSecurity.LogsIngestion.Contracts imports
# build/JsonSchema.targets and reads schema files from src/contracts/json/ -
# both outside src/dotnet/. See docker-compose.yml's `build.context` for the
# logs-ingestion-service entry.

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
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.Observability/Dzaba.HomeSecurity.Observability.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.Observability/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.WebApi/Dzaba.HomeSecurity.WebApi.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.WebApi/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.DeviceAuth/Dzaba.HomeSecurity.DeviceAuth.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.DeviceAuth/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.Contracts/Dzaba.HomeSecurity.MessageBroker.Contracts.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.Contracts/"]
COPY ["src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.RabbitMQ/Dzaba.HomeSecurity.MessageBroker.RabbitMQ.csproj", "src/dotnet/Common/Dzaba.HomeSecurity.MessageBroker.RabbitMQ/"]
COPY ["src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Contracts/Dzaba.HomeSecurity.LogsIngestion.Contracts.csproj", "src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Contracts/"]
COPY ["src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Service/Dzaba.HomeSecurity.LogsIngestion.Service.csproj", "src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Service/"]
RUN dotnet restore "src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Service/Dzaba.HomeSecurity.LogsIngestion.Service.csproj"

# The Contracts project generates C# from these at build time (NSwag), so
# they're needed before `dotnet build`, not just restore.
COPY src/contracts/json/ src/contracts/json/
COPY src/dotnet/ src/dotnet/

WORKDIR "/source/src/dotnet/LogsIngestion/Dzaba.HomeSecurity.LogsIngestion.Service"
RUN dotnet build "Dzaba.HomeSecurity.LogsIngestion.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Dzaba.HomeSecurity.LogsIngestion.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dzaba.HomeSecurity.LogsIngestion.Service.dll"]
