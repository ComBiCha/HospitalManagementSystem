FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/Services/NotificationService/NotificationService.API/NotificationService.API.csproj", "src/Services/NotificationService/NotificationService.API/"]

RUN dotnet restore "src/Services/NotificationService/NotificationService.API/NotificationService.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Services/NotificationService/NotificationService.API"
RUN dotnet build "NotificationService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NotificationService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.API.dll"]