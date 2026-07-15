FROM node:22-alpine AS web
WORKDIR /repo
COPY src/web/Cartex.Web.Angular/package.json src/web/Cartex.Web.Angular/package-lock.json ./src/web/Cartex.Web.Angular/
WORKDIR /repo/src/web/Cartex.Web.Angular
RUN npm ci
WORKDIR /repo
COPY src/desktop/Cartex.UI/Assets/Languages/ ./src/desktop/Cartex.UI/Assets/Languages/
COPY src/web/Cartex.Web.Angular/ ./src/web/Cartex.Web.Angular/
WORKDIR /repo/src/web/Cartex.Web.Angular
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
COPY --from=web /repo/src/web/Cartex.Web.Angular/dist/cartex-web/browser/ ./wwwroot/
ENV Urls=http://0.0.0.0:5015
EXPOSE 5015
ENTRYPOINT ["dotnet", "Cartex.Api.dll"]
