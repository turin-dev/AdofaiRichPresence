# Root deployment image for the CDN service in this monorepo.
# Dokploy builds from the repository root, so the CDN-specific Dockerfile is
# mirrored here with paths relative to that build context.
FROM node:22-alpine

WORKDIR /app
COPY cdn-server/server.js cdn-server/package.json ./

RUN mkdir -p /app/storage && chown -R node:node /app
USER node

ENV PORT=8787
EXPOSE 8787

VOLUME ["/app/storage"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD node -e "require('http').get('http://127.0.0.1:' + (process.env.PORT || 8787) + '/health', r => process.exit(r.statusCode === 200 ? 0 : 1)).on('error', () => process.exit(1))"

CMD ["node", "server.js"]
