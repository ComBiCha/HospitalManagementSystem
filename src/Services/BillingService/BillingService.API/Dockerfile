FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/BillingService/BillingService.API/BillingService.API.csproj", "src/Services/BillingService/BillingService.API/"]
RUN dotnet restore "src/Services/BillingService/BillingService.API/BillingService.API.csproj"
COPY . .
WORKDIR /src/src/Services/BillingService/BillingService.API
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# Cài curl cho healthcheck / debug
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
ENTRYPOINT ["dotnet", "BillingService.API.dll"]
