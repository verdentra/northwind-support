# syntax=docker/dockerfile:1
#
# The Support Desk API. Build from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# or let docker compose do it (`docker compose up --build`).
#
# Nothing environment-specific is baked in: the connection string, the JWT signing key and the
# environment name all come from environment variables at run time (see .env.example).

# ---------------------------------------------------------------------------- build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files and the shared build settings only, so this layer is
# reused until a dependency changes - editing a .cs file does not re-download packages.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Then the source, and publish without restoring again.
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        -p:UseAppHost=false

# -------------------------------------------------------------------------- runtime ---
# The ASP.NET runtime only: no SDK, no compilers, no source.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Plain HTTP on 8080; TLS belongs to whatever sits in front of the container. No certificates
# are copied in.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The non-root "app" user that the .NET images provide (UID 1654).
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
