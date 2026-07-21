# syntax=docker/dockerfile:1
# Blazor WebAssembly — served as static files via nginx.
# Phase 1 WASM conversion: replaced dotnet runtime with nginx static file server.

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Pin NuGet global packages folder for BuildKit cache mount
ENV NUGET_PACKAGES=/root/.nuget/packages

# Copy csproj files and restore as distinct layers
COPY ["VanAn.sln", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["2_Gateway/VanAn.Gateway.csproj", "2_Gateway/"]
COPY ["3_CoreHub/VanAn.CoreHub.csproj", "3_CoreHub/"]
COPY ["UI.Platform/VanAn.UI.Platform.csproj", "UI.Platform/"]
COPY ["VanAn.Accounting/VanAn.Accounting.csproj", "VanAn.Accounting/"]
COPY ["VanAn.Accounting/VanAn.Accounting.Analyzers/VanAn.Accounting.Analyzers.csproj", "VanAn.Accounting/VanAn.Accounting.Analyzers/"]
COPY ["5_WebApps/ShopERP/VanAn.ShopERP.csproj", "5_WebApps/ShopERP/"]
COPY ["5_WebApps/KhachLink/VanAn.KhachLink.csproj", "5_WebApps/KhachLink/"]
RUN dotnet restore "5_WebApps/KhachLink/VanAn.KhachLink.csproj"

# Copy everything else and publish
COPY . .
WORKDIR "/src/5_WebApps/KhachLink"
RUN dotnet publish "VanAn.KhachLink.csproj" -c Release -o /app/publish

# Runtime stage — nginx serving static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Remove default nginx static assets
RUN rm -rf ./*

# Copy published Blazor WASM static files
COPY --from=build /app/publish/wwwroot ./

# Copy custom nginx config for Blazor WASM SPA routing
COPY 5_WebApps/KhachLink/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# nginx serves static files — no ENTRYPOINT needed (nginx image has default CMD)
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD wget -q --spider http://localhost/ || exit 1
