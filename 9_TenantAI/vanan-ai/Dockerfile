# syntax=docker/dockerfile:1
# vanan-ai — standalone Tenant AI service (SRS v1.1 [D1]).
# Own PG (pgvector), own image; callers integrate via HTTP API only.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages
ENV LANG=C.UTF-8

# Standalone — only needs props files + the 5 tenantai csproj files.
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["9_TenantAI/vanan-ai/src/VanAn.TenantAi.Api/VanAn.TenantAi.Api.csproj", "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Api/"]
COPY ["9_TenantAI/vanan-ai/src/VanAn.TenantAi.Contracts/VanAn.TenantAi.Contracts.csproj", "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Contracts/"]
COPY ["9_TenantAI/vanan-ai/src/VanAn.TenantAi.Domain/VanAn.TenantAi.Domain.csproj", "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Domain/"]
COPY ["9_TenantAI/vanan-ai/src/VanAn.TenantAi.Infra/VanAn.TenantAi.Infra.csproj", "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Infra/"]
COPY ["9_TenantAI/vanan-ai/src/VanAn.TenantAi.Ai/VanAn.TenantAi.Ai.csproj", "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Ai/"]
RUN dotnet restore "9_TenantAI/vanan-ai/src/VanAn.TenantAi.Api/VanAn.TenantAi.Api.csproj"

COPY . .
WORKDIR "/src/9_TenantAI/vanan-ai/src/VanAn.TenantAi.Api"
RUN dotnet publish "VanAn.TenantAi.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5060

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5060

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.TenantAi.Api.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:5060/v1/health || exit 1
