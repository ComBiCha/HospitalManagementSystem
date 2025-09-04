FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Services/ImageService/ImageService.API/ImageService.API.csproj", "src/Services/ImageService/ImageService.API/"]
RUN dotnet restore "src/Services/ImageService/ImageService.API/ImageService.API.csproj"
COPY . .
WORKDIR "/src/src/Services/ImageService/ImageService.API"
RUN dotnet build "ImageService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ImageService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ImageService.API.dll"]
