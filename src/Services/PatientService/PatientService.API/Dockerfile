# Use the official .NET 9 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_HTTPS_PORT=""
ENV HTTP_PORTS=80
ENV HTTPS_PORTS=""

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/Services/PatientService/PatientService.API/PatientService.API.csproj", "src/Services/PatientService/PatientService.API/"]
RUN dotnet restore "src/Services/PatientService/PatientService.API/PatientService.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Services/PatientService/PatientService.API"
RUN dotnet build "PatientService.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "PatientService.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM base AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "PatientService.API.dll"]
