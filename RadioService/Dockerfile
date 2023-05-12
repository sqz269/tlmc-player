#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Prepare client library

COPY ["OASClientApi/musicdataservice-client/src/ClientApi.MusicDataServiceClientApi/ClientApi.MusicDataServiceClientApi.csproj", "OASClientApi/musicdataservice-client/src/ClientApi.MusicDataServiceClientApi/"]
COPY . .
RUN dotnet restore "OASClientApi/musicdataservice-client/src/ClientApi.MusicDataServiceClientApi/ClientApi.MusicDataServiceClientApi.csproj"
RUN dotnet pack "OASClientApi/musicdataservice-client/src/ClientApi.MusicDataServiceClientApi/ClientApi.MusicDataServiceClientApi.csproj" -c Release

COPY ["RadioService/Nuget.config", "RadioService/Nuget.config"]
COPY ["RadioService/RadioService.csproj", "RadioService/"]

RUN dotnet restore "RadioService/RadioService.csproj" --configfile RadioService/Nuget.config
WORKDIR "/src/RadioService"
RUN dotnet build "RadioService.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "RadioService.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RadioService.dll"]