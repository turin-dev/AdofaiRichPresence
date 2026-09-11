FROM node:22-alpine

WORKDIR /app
COPY server.js package.json ./

ENV PORT=8787
EXPOSE 8787

VOLUME ["/app/storage"]

CMD ["node", "server.js"]
