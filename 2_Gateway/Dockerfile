# syntax=docker/dockerfile:1
# Use .NET 8 runtime (matching our target framework)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Install curl for healthcheck
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Use .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Pin NuGet global packages folder for BuildKit cache mount (NuGet.Config uses %USERPROFILE% which is Windows-only)
ENV NUGET_PACKAGES=/root/.nuget/packages

# Copy csproj files and restore as distinct layers
COPY ["VanAn.sln", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["2_Gateway/VanAn.Gateway.csproj", "2_Gateway/"]
COPY ["3_CoreHub/VanAn.CoreHub.csproj", "3_CoreHub/"]
COPY ["UI.Platform/VanAn.UI.Platform.csproj", "UI.Platform/"]
COPY ["VanAn.Accounting/VanAn.Accounting.csproj", "VanAn.Accounting/"]
COPY ["VanAn.Accounting/VanAn.Accounting.Analyzers/VanAn.Accounting.Analyzers.csproj", "VanAn.Accounting/VanAn.Accounting.Analyzers/"]
COPY ["5_WebApps/ShopERP/VanAn.ShopERP.csproj", "5_WebApps/ShopERP/"]
COPY ["5_WebApps/KhachLink/VanAn.KhachLink.csproj", "5_WebApps/KhachLink/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "2_Gateway/VanAn.Gateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/2_Gateway"

# TODO: Add tests back when test project is properly configured
# RUN dotnet test "6_Tests/VanAn.Tests.csproj" --no-restore --logger "console;verbosity=detailed" --results-directory /test-results

# Build application
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "VanAn.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "VanAn.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll"]
