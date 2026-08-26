# syntax=docker/dockerfile:1
# VanAn.Crawler — standalone worker service (no project references)
# Listens on port 5010 for POST /trigger, crawls business listings, posts to Gateway.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages
ENV LANG=C.UTF-8

# Crawler is standalone — only needs its own csproj + Directory.Packages.props
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["7_Tooling/VanAn.Crawler/VanAn.Crawler.csproj", "7_Tooling/VanAn.Crawler/"]
RUN dotnet restore "7_Tooling/VanAn.Crawler/VanAn.Crawler.csproj"

# Copy everything else and publish
COPY . .
WORKDIR "/src/7_Tooling/VanAn.Crawler"
RUN dotnet publish "VanAn.Crawler.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5010

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Crawler.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:5010/health || exit 1
