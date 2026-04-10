# Use .NET 8 runtime with ICU support for internationalization
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Install ICU libraries for globalization support
RUN apt-get update && apt-get install -y \
    icu-devtools \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Set environment variable for proper globalization
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=en_US.UTF-8
ENV LANG=en_US.UTF-8

# Use .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore as distinct layers
COPY ["VanAn.sln", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["3_CoreHub/VanAn.CoreHub.csproj", "3_CoreHub/"]
RUN dotnet restore "3_CoreHub/VanAn.CoreHub.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/3_CoreHub"

# TODO: Add tests back when test project is properly configured
# RUN dotnet test "6_Tests/VanAn.Tests.csproj" --no-restore --logger "console;verbosity=detailed" --results-directory /test-results

# Build application
RUN dotnet build "VanAn.CoreHub.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VanAn.CoreHub.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.CoreHub.dll"]
