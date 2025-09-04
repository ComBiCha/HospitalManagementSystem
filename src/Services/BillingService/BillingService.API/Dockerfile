FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/BillingService/BillingService.API/BillingService.API.csproj", "src/Services/BillingService/BillingService.API/"]
RUN dotnet restore "src/Services/BillingService/BillingService.API/BillingService.API.csproj"
COPY . .
WORKDIR "/src/src/Services/BillingService/BillingService.API"
RUN dotnet build "BillingService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BillingService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BillingService.API.dll"]
