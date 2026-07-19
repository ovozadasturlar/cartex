FROM --platform=$BUILDPLATFORM node:22-alpine AS build
WORKDIR /repo
COPY src/web/Cartex.Web.Angular/package.json src/web/Cartex.Web.Angular/package-lock.json ./src/web/Cartex.Web.Angular/
WORKDIR /repo/src/web/Cartex.Web.Angular
RUN npm ci
WORKDIR /repo
COPY src/desktop/Cartex.UI/Assets/Languages/ ./src/desktop/Cartex.UI/Assets/Languages/
COPY src/web/Cartex.Web.Angular/ ./src/web/Cartex.Web.Angular/
WORKDIR /repo/src/web/Cartex.Web.Angular
RUN npm run build

FROM nginx:alpine
COPY src/web/Cartex.Web.Angular/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /repo/src/web/Cartex.Web.Angular/dist/cartex-web/browser/ /usr/share/nginx/html/
EXPOSE 80
