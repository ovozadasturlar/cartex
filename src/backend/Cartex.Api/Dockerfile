FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet publish src/backend/Cartex.Api/Cartex.Api.csproj -c Release -a $TARGETARCH -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV Urls=http://0.0.0.0:5015
EXPOSE 5015
ENTRYPOINT ["/bin/sh", "-c", "if [ -d /app/storage ] && [ -n \"$Storage__LocalPath\" ]; then mkdir -p \"$Storage__LocalPath\" && cp -rn /app/storage/. \"$Storage__LocalPath\"/ 2>/dev/null || true; fi; exec dotnet Cartex.Api.dll"]
