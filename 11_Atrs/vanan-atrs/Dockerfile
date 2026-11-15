# syntax=docker/dockerfile:1
# vanan-atrs — standalone Account Trust & Recovery Service (SRS v1.0, ADR-001).
# Own PostgreSQL DB, own image; host apps integrate via API + auth token.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages

# Standalone — only needs props files + the 4 atrs csproj files.
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["11_Atrs/vanan-atrs/src/VanAn.Atrs.Api/VanAn.Atrs.Api.csproj", "11_Atrs/vanan-atrs/src/VanAn.Atrs.Api/"]
COPY ["11_Atrs/vanan-atrs/src/VanAn.Atrs.Contracts/VanAn.Atrs.Contracts.csproj", "11_Atrs/vanan-atrs/src/VanAn.Atrs.Contracts/"]
COPY ["11_Atrs/vanan-atrs/src/VanAn.Atrs.Domain/VanAn.Atrs.Domain.csproj", "11_Atrs/vanan-atrs/src/VanAn.Atrs.Domain/"]
COPY ["11_Atrs/vanan-atrs/src/VanAn.Atrs.Infra/VanAn.Atrs.Infra.csproj", "11_Atrs/vanan-atrs/src/VanAn.Atrs.Infra/"]
RUN dotnet restore "11_Atrs/vanan-atrs/src/VanAn.Atrs.Api/VanAn.Atrs.Api.csproj"

COPY ["11_Atrs/vanan-atrs/src/", "11_Atrs/vanan-atrs/src/"]
RUN dotnet publish "11_Atrs/vanan-atrs/src/VanAn.Atrs.Api/VanAn.Atrs.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "VanAn.Atrs.Api.dll"]