# ==================================================
# SSSP Web Dashboard - React + TypeScript + Vite
# Multi-stage Dockerfile for optimized production
# ==================================================

# ==================================================
# Stage 1: Base
# ==================================================
ARG NODE_VERSION=20
FROM node:${NODE_VERSION}-alpine AS base

# Install dumb-init for proper signal handling
RUN apk add --no-cache dumb-init

WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 sssp && \
    adduser -u 1000 -G sssp -s /bin/sh -D sssp

# ==================================================
# Stage 2: Dependencies
# ==================================================
FROM base AS dependencies

# Copy package files
COPY apps/web/package*.json ./

# Install dependencies
RUN npm ci --only=production && \
    cp -R node_modules /prod_node_modules && \
    npm ci

# ==================================================
# Stage 3: Build
# ==================================================
FROM dependencies AS build

# Copy source code
COPY apps/web/ .
COPY packages/shared-types/ /packages/shared-types/

# Build arguments
ARG VITE_API_BASE=http://localhost:8080
ARG VITE_WS_BASE=ws://localhost:8080
ARG NODE_ENV=production

ENV VITE_API_BASE=${VITE_API_BASE}
ENV VITE_WS_BASE=${VITE_WS_BASE}
ENV NODE_ENV=${NODE_ENV}

# Build application
RUN npm run build

# ==================================================
# Stage 4: Development
# ==================================================
FROM base AS development

COPY apps/web/package*.json ./
RUN npm install

COPY apps/web/ .
COPY packages/shared-types/ /packages/shared-types/

USER sssp
EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# ==================================================
# Stage 5: Production with Nginx
# ==================================================
FROM nginx:1.25-alpine AS production

# Copy custom nginx config
COPY apps/web/nginx.conf /etc/nginx/nginx.conf

# Copy built assets
COPY --from=build --chown=nginx:nginx /app/dist /usr/share/nginx/html

# Add healthcheck
RUN apk add --no-cache curl

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

EXPOSE 80

# Use nginx user
USER nginx

CMD ["nginx", "-g", "daemon off;"]
