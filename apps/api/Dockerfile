# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy shared configuration first for better Docker cache usage
COPY global.json .
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Copy project files before source files
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# Restore NuGet packages
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Copy the remaining API source code
COPY apps/api/src apps/api/src

# Publish the API
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# Do not include development connection strings or JWT settings in the image.
RUN rm -f /app/publish/appsettings.Development.json

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# The API listens on HTTP port 8080 inside the container
ENV ASPNETCORE_HTTP_PORTS=8080

# Run as the non-root user provided by the official .NET image
USER $APP_UID

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
