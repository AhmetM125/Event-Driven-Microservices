# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY Directory.Build.props ./
COPY src/BuildingBlocks/EventDrivenShop.Contracts/*.csproj ./src/BuildingBlocks/EventDrivenShop.Contracts/
COPY src/BuildingBlocks/EventDrivenShop.Observability/*.csproj ./src/BuildingBlocks/EventDrivenShop.Observability/
COPY src/BuildingBlocks/EventDrivenShop.Common/*.csproj ./src/BuildingBlocks/EventDrivenShop.Common/
COPY src/Services/Notifications/EventDrivenShop.NotificationService.Domain/*.csproj ./src/Services/Notifications/EventDrivenShop.NotificationService.Domain/
COPY src/Services/Notifications/EventDrivenShop.NotificationService.Application/*.csproj ./src/Services/Notifications/EventDrivenShop.NotificationService.Application/
COPY src/Services/Notifications/EventDrivenShop.NotificationService.Infrastructure/*.csproj ./src/Services/Notifications/EventDrivenShop.NotificationService.Infrastructure/
COPY src/Services/Notifications/EventDrivenShop.NotificationService.Api/*.csproj ./src/Services/Notifications/EventDrivenShop.NotificationService.Api/

# Restore dependencies
RUN dotnet restore ./src/Services/Notifications/EventDrivenShop.NotificationService.Api/EventDrivenShop.NotificationService.Api.csproj

# Copy source code and build
COPY src/BuildingBlocks/ ./src/BuildingBlocks/
COPY src/Services/Notifications/ ./src/Services/Notifications/

WORKDIR /app/src/Services/Notifications/EventDrivenShop.NotificationService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventDrivenShop.NotificationService.Api.dll"]
