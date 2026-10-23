# syntax=docker/dockerfile:1
# vanan-metering — standalone platform-fee metering service (SRS [G1]).
# Own DB (SQLite volume), own image; callers integrate via HTTP API only.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages
ENV LANG=C.UTF-8

# Standalone — only needs props files + the 4 metering csproj files.
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["8_Metering/vanan-metering/src/VanAn.Metering.Api/VanAn.Metering.Api.csproj", "8_Metering/vanan-metering/src/VanAn.Metering.Api/"]
COPY ["8_Metering/vanan-metering/src/VanAn.Metering.Contracts/VanAn.Metering.Contracts.csproj", "8_Metering/vanan-metering/src/VanAn.Metering.Contracts/"]
COPY ["8_Metering/vanan-metering/src/VanAn.Metering.Domain/VanAn.Metering.Domain.csproj", "8_Metering/vanan-metering/src/VanAn.Metering.Domain/"]
COPY ["8_Metering/vanan-metering/src/VanAn.Metering.Infra/VanAn.Metering.Infra.csproj", "8_Metering/vanan-metering/src/VanAn.Metering.Infra/"]
RUN dotnet restore "8_Metering/vanan-metering/src/VanAn.Metering.Api/VanAn.Metering.Api.csproj"

COPY . .
WORKDIR "/src/8_Metering/vanan-metering/src/VanAn.Metering.Api"
RUN dotnet publish "VanAn.Metering.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5050

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# SQLite lives on this volume — back it up (ledger + wallet land here from GĐ-1/P7).
VOLUME ["/app/data"]
ENV Metering__DbPath=/app/data/metering.db
ENV ASPNETCORE_URLS=http://+:5050

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Metering.Api.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:5050/v1/health || exit 1
