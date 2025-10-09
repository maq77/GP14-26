# =========================
#  Build stage (Debian Node)
#  - regenerate Linux lockfile to avoid Rollup optional-deps issue
# =========================
FROM node:20 AS build
WORKDIR /app

# Copy only package.json first (do NOT copy package-lock.json here)
COPY package.json ./

# Install deps and produce a Linux package-lock.json
RUN npm install --no-audit --no-fund

# Now copy the rest of your app (includes your src, vite.config.js, inner folder, etc.)
COPY . .

# Build the SPA (Vite outputs to /app/dist)
RUN npm run build

# =========================
#  Runtime stage (Nginx)
# =========================
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copy built assets
COPY --from=build /app/dist ./

# Replace default site with an SPA-aware config + gzip + caching
RUN rm -f /etc/nginx/conf.d/default.conf
COPY <<'NGINX' /etc/nginx/conf.d/default.conf
server {
  listen 80;
  server_name _;
  root /usr/share/nginx/html;
  index index.html;

  # Gzip for text assets
  gzip on;
  gzip_types text/plain text/css application/javascript application/json application/xml image/svg+xml;
  gzip_min_length 1024;

  # Long cache for hashed assets, no-store for HTML
  location ~* \.(?:js|css|woff2?|ttf|eot|svg)$ {
    add_header Cache-Control "public, max-age=31536000, immutable";
    try_files $uri =404;
  }

  location = /index.html {
    add_header Cache-Control "no-store";
  }

  # SPA fallback
  location / {
    try_files $uri /index.html;
  }

  # ---- OPTIONAL: proxy to .NET API & SignalR ----
  # location /api/ {
  #   proxy_pass http://api-dotnet:8080/;
  #   proxy_http_version 1.1;
  #   proxy_set_header Host $host;
  #   proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  #   proxy_set_header X-Forwarded-Proto $scheme;
  # }

  # location /hub {
  #   proxy_pass http://api-dotnet:8080/hub;
  #   proxy_http_version 1.1;
  #   proxy_set_header Upgrade $http_upgrade;
  #   proxy_set_header Connection "Upgrade";
  #   proxy_set_header Host $host;
  # }
}
NGINX

EXPOSE 80
