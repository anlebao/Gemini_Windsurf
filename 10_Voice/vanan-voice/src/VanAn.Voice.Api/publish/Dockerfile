# syntax=docker/dockerfile:1
# vanan-voice — standalone voice signaling service (SRS v1.1).
# Own DB (SQLite volume), own image; host apps integrate via SDK + auth token.
# Media never flows through this service (WebRTC P2P / Coturn fallback).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages
ENV LANG=C.UTF-8

# Standalone — only needs props files + the 4 voice csproj files.
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["10_Voice/vanan-voice/src/VanAn.Voice.Api/VanAn.Voice.Api.csproj", "10_Voice/vanan-voice/src/VanAn.Voice.Api/"]
COPY ["10_Voice/vanan-voice/src/VanAn.Voice.Contracts/VanAn.Voice.Contracts.csproj", "10_Voice/vanan-voice/src/VanAn.Voice.Contracts/"]
COPY ["10_Voice/vanan-voice/src/VanAn.Voice.Domain/VanAn.Voice.Domain.csproj", "10_Voice/vanan-voice/src/VanAn.Voice.Domain/"]
COPY ["10_Voice/vanan-voice/src/VanAn.Voice.Infra/VanAn.Voice.Infra.csproj", "10_Voice/vanan-voice/src/VanAn.Voice.Infra/"]
RUN dotnet restore "10_Voice/vanan-voice/src/VanAn.Voice.Api/VanAn.Voice.Api.csproj"

COPY . .
WORKDIR "/src/10_Voice/vanan-voice/src/VanAn.Voice.Api"
RUN dotnet publish "VanAn.Voice.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5070

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# SQLite lives on this volume — back it up (call history + events land here).
VOLUME ["/app/data"]
ENV Voice__DbPath=/app/data/voice.db
ENV ASPNETCORE_URLS=http://+:5070

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Voice.Api.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:5070/v1/health || exit 1