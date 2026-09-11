#!/usr/bin/env bash
set -euo pipefail

# Fill these in yourself, then run: bash deploy-dokploy.sh
DOKPLOY_URL="https://cloud.turin.my"
API_KEY="PASTE_YOUR_KEY_HERE"
PROJECT_ID="PASTE_PROJECT_ID_HERE"       # dokploy > your project > copy from URL
SERVER_ID=""                              # leave empty for the default/local server

curl -s -X POST "$DOKPLOY_URL/api/application.create" \
  -H "x-api-key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"adofai-cdn-server\",\"projectId\":\"$PROJECT_ID\",\"serverId\":\"$SERVER_ID\"}"

echo
echo "Copy the returned applicationId, then:"
echo '  1. In the Dokploy UI, use the AdofaiRichPresence repository root as the build context. The root Dockerfile starts cdn-server/ automatically. Alternatively set the build context to cdn-server/ and select cdn-server/Dockerfile.'
echo "  2. Set the domain to cdn.adofai.turin.my with HTTPS enabled."
echo "  3. Optionally set env vars RATE_LIMIT_MAX / RATE_LIMIT_WINDOW_MS / STORAGE_DIR."
echo "  4. Trigger the deploy:"
echo "     curl -s -X POST \"$DOKPLOY_URL/api/application.deploy\" -H \"x-api-key: $API_KEY\" -H \"Content-Type: application/json\" -d '{\"applicationId\":\"<APPLICATION_ID>\"}'"
