# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["src/Services/DoctorService/DoctorService.API/DoctorService.API.csproj", "src/Services/DoctorService/DoctorService.API/"]
RUN dotnet restore "src/Services/DoctorService/DoctorService.API/DoctorService.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Services/DoctorService/DoctorService.API"
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /out .

# Expose port
EXPOSE 80

ENTRYPOINT ["dotnet", "DoctorService.API.dll"]