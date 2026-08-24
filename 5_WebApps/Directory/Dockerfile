# syntax=docker/dockerfile:1
# Blazor SSR — ASP.NET runtime (not nginx static like KhachLink WASM)
# Directory SSR serves Directory-profile domains (timlathay.com, etc.)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages

# Issue #154: Force UTF-8 locale in build stage so Razor compiler reads .razor files
# without BOM as UTF-8 (not ASCII/C locale). Prevents Vietnamese mojibake in compiled DLLs.
ENV LANG=C.UTF-8

# Copy only csproj files needed for Directory + its dependencies (Shared + UI.Platform)
COPY ["VanAn.sln", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["UI.Platform/VanAn.UI.Platform.csproj", "UI.Platform/"]
COPY ["5_WebApps/Directory/VanAn.Directory.csproj", "5_WebApps/Directory/"]
RUN dotnet restore "5_WebApps/Directory/VanAn.Directory.csproj"

# Copy everything else and publish
COPY . .
WORKDIR "/src/5_WebApps/Directory"
RUN dotnet publish "VanAn.Directory.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Directory.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:8080/health || exit 1
