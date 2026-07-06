FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV Urls=http://0.0.0.0:5015
EXPOSE 5015
ENTRYPOINT ["dotnet", "Cartex.Api.dll"]
