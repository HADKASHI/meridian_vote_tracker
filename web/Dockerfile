# The Angular app, built without .NET from the committed contract, served by nginx.
# Build context: the repository root.
FROM node:24-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY contracts/openapi.json /contracts/openapi.json
COPY web/ ./
RUN npm run build

FROM nginx:1.30-alpine
COPY web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/web/browser /usr/share/nginx/html
