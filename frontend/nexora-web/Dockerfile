FROM node:24.20.0-alpine3.24 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm test -- --watch=false
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29.1-alpine3.22 AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/nexora-web/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 CMD ["wget", "--no-verbose", "--tries=1", "--spider", "http://127.0.0.1:8080/"]
