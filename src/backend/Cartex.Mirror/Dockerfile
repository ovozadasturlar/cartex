FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/backend/Cartex.Mirror/Cartex.Mirror.csproj src/backend/Cartex.Mirror/
RUN dotnet restore src/backend/Cartex.Mirror/Cartex.Mirror.csproj
COPY src/backend/Cartex.Mirror/ src/backend/Cartex.Mirror/
RUN dotnet publish src/backend/Cartex.Mirror/Cartex.Mirror.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV Mirror__DataPath=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cartex.Mirror.dll"]
