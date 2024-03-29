#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MusicDataService/MusicDataService.csproj", "MusicDataService/"]
COPY ["KeycloakAuthProvider/KeycloakAuthProvider.csproj", "KeycloakAuthProvider/"]
RUN dotnet restore "MusicDataService/MusicDataService.csproj"
COPY . .
WORKDIR "/src/MusicDataService"
RUN dotnet build "MusicDataService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MusicDataService.csproj" -c Release -o /app/publish

FROM base AS final
# Add Ffmpeg to container
# RUN apt-get update && apt-get install -y software-properties-common
# RUN add-apt-repository ppa:mc3man/trusty-media
RUN apt-get update && apt-get install -y ffmpeg

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MusicDataService.dll"]