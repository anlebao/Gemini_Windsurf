# syntax=docker/dockerfile:1
# Use .NET 8 runtime with ICU support for internationalization
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Install ICU libraries for globalization support and curl for healthcheck
RUN apt-get update && apt-get install -y \
    icu-devtools \
    libicu-dev \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set environment variable for proper globalization
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8

# Use .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Pin NuGet global packages folder for BuildKit cache mount (NuGet.Config uses %USERPROFILE% which is Windows-only)
ENV NUGET_PACKAGES=/root/.nuget/packages

# Issue #154: Force UTF-8 locale in build stage so Razor compiler reads .razor files
# without BOM as UTF-8 (not ASCII/C locale). Prevents Vietnamese mojibake in compiled DLLs.
ENV LANG=C.UTF-8

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
RUN dotnet restore "5_WebApps/ShopERP/VanAn.ShopERP.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/5_WebApps/ShopERP"

# Debug: verify Phase 1/2/3 files are in build context
RUN ls -la Controllers/SocialAuthController.cs Controllers/LoyaltyController.cs Controllers/CustomerIdentityController.cs

# W7-T4 (2026-07-05): Test stage (separate from final image — keeps runtime image lean)
# Usage: docker build --target test -t vanan-shoperp-test .
FROM build AS test
RUN dotnet test "VanAn.ShopERP.csproj" -c Release --no-build --logger "console;verbosity=detailed" --results-directory /test-results

# Build application
RUN dotnet build "VanAn.ShopERP.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/5_WebApps/ShopERP"
RUN dotnet publish "VanAn.ShopERP.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.ShopERP.dll"]
