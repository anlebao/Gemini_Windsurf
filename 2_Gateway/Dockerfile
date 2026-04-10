# Use .NET 8 runtime (matching our target framework)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Use .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY ["VanAn.sln", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["2_Gateway/VanAn.Gateway.csproj", "2_Gateway/"]
RUN dotnet restore "2_Gateway/VanAn.Gateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/2_Gateway"

# TODO: Add tests back when test project is properly configured
# RUN dotnet test "6_Tests/VanAn.Tests.csproj" --no-restore --logger "console;verbosity=detailed" --results-directory /test-results

# Build application
RUN dotnet build "VanAn.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VanAn.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll"]
