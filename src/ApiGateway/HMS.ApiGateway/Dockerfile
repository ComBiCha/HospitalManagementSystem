# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/ApiGateway/HMS.ApiGateway/HMS.ApiGateway.csproj", "src/ApiGateway/HMS.ApiGateway/"]
RUN dotnet restore "src/ApiGateway/HMS.ApiGateway/HMS.ApiGateway.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/ApiGateway/HMS.ApiGateway"
RUN dotnet build "HMS.ApiGateway.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "HMS.ApiGateway.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Expose port
EXPOSE 80

ENTRYPOINT ["dotnet", "HMS.ApiGateway.dll"]