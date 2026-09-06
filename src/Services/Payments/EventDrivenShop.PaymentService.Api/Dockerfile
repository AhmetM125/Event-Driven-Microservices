# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY Directory.Build.props ./
COPY src/BuildingBlocks/EventDrivenShop.Contracts/*.csproj ./src/BuildingBlocks/EventDrivenShop.Contracts/
COPY src/BuildingBlocks/EventDrivenShop.Observability/*.csproj ./src/BuildingBlocks/EventDrivenShop.Observability/
COPY src/BuildingBlocks/EventDrivenShop.Common/*.csproj ./src/BuildingBlocks/EventDrivenShop.Common/
COPY src/Services/Payments/EventDrivenShop.PaymentService.Domain/*.csproj ./src/Services/Payments/EventDrivenShop.PaymentService.Domain/
COPY src/Services/Payments/EventDrivenShop.PaymentService.Application/*.csproj ./src/Services/Payments/EventDrivenShop.PaymentService.Application/
COPY src/Services/Payments/EventDrivenShop.PaymentService.Infrastructure/*.csproj ./src/Services/Payments/EventDrivenShop.PaymentService.Infrastructure/
COPY src/Services/Payments/EventDrivenShop.PaymentService.Api/*.csproj ./src/Services/Payments/EventDrivenShop.PaymentService.Api/

# Restore dependencies
RUN dotnet restore ./src/Services/Payments/EventDrivenShop.PaymentService.Api/EventDrivenShop.PaymentService.Api.csproj

# Copy source code and build
COPY src/BuildingBlocks/ ./src/BuildingBlocks/
COPY src/Services/Payments/ ./src/Services/Payments/

WORKDIR /app/src/Services/Payments/EventDrivenShop.PaymentService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventDrivenShop.PaymentService.Api.dll"]
