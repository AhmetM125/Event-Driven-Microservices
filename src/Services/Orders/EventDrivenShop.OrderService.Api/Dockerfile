# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY Directory.Build.props ./
COPY src/BuildingBlocks/EventDrivenShop.Contracts/*.csproj ./src/BuildingBlocks/EventDrivenShop.Contracts/
COPY src/BuildingBlocks/EventDrivenShop.Observability/*.csproj ./src/BuildingBlocks/EventDrivenShop.Observability/
COPY src/BuildingBlocks/EventDrivenShop.Common/*.csproj ./src/BuildingBlocks/EventDrivenShop.Common/
COPY src/Services/Orders/EventDrivenShop.OrderService.Domain/*.csproj ./src/Services/Orders/EventDrivenShop.OrderService.Domain/
COPY src/Services/Orders/EventDrivenShop.OrderService.Application/*.csproj ./src/Services/Orders/EventDrivenShop.OrderService.Application/
COPY src/Services/Orders/EventDrivenShop.OrderService.Infrastructure/*.csproj ./src/Services/Orders/EventDrivenShop.OrderService.Infrastructure/
COPY src/Services/Orders/EventDrivenShop.OrderService.Api/*.csproj ./src/Services/Orders/EventDrivenShop.OrderService.Api/

# Restore dependencies
RUN dotnet restore ./src/Services/Orders/EventDrivenShop.OrderService.Api/EventDrivenShop.OrderService.Api.csproj

# Copy source code and build
COPY src/BuildingBlocks/ ./src/BuildingBlocks/
COPY src/Services/Orders/ ./src/Services/Orders/

WORKDIR /app/src/Services/Orders/EventDrivenShop.OrderService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventDrivenShop.OrderService.Api.dll"]
