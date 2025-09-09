FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["HospitalManagementSystem.API/HospitalManagementSystem.API.csproj", "HospitalManagementSystem.API/"]
COPY ["HospitalManagementSystem.Domain/HospitalManagementSystem.Domain.csproj", "HospitalManagementSystem.Domain/"]
COPY ["HospitalManagementSystem.Application/HospitalManagementSystem.Application.csproj", "HospitalManagementSystem.Application/"]
COPY ["HospitalManagementSystem.Infrastructure/HospitalManagementSystem.Infrastructure.csproj", "HospitalManagementSystem.Infrastructure/"]
RUN dotnet restore "HospitalManagementSystem.API/HospitalManagementSystem.API.csproj"
COPY . .
WORKDIR "/src/HospitalManagementSystem.API"
RUN dotnet build "HospitalManagementSystem.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HospitalManagementSystem.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HospitalManagementSystem.API.dll"]