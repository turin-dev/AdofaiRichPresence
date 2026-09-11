# adofai-cdn-server

Tiny, dependency-free Node HTTP server that lets AdofaiRichPresence upload each
level's cover image once and get back a public HTTPS URL, so Discord can show
it as the Rich Presence large image. No npm packages required.

Uploads can be public (no key needed) or protected with `UPLOAD_SECRET`.
They are also guarded by a per-IP rate limit and a 2-hour TTL on stored images.

## Run

```
node server.js
```

Env vars (all optional):
- `PORT` (default `8787`)
- `STORAGE_DIR` (default `./storage`)
- `PUBLIC_BASE_URL` (default `https://cdn.adofai.turin.my`) — public URL prefix returned after upload
- `UPLOAD_SECRET` (default empty) — when set, clients must send `Authorization: Bearer <secret>`
- `TRUST_PROXY` (default `0`) — set to `1` only when a trusted reverse proxy overwrites `X-Forwarded-For`
- `RATE_LIMIT_MAX` (default `20`) — max uploads per IP per window
- `RATE_LIMIT_WINDOW_MS` (default `600000` = 10 min)

## Endpoints

- `POST /upload` — body is the raw image bytes, `Content-Type: image/png|image/jpeg|image/webp`. Returns `{ "url": "https://cdn.adofai.turin.my/i/<sha256>.<ext>" }`. Same image content always returns the same URL (content-addressed, so re-uploads just refresh the TTL instead of duplicating storage). Rate-limited per IP; over the limit returns `429`.
- `GET /i/<hash>.<ext>` — serves the stored image. Images older than 2 hours since their last upload are deleted (checked both lazily on read and via a background sweep every 10 minutes) and return `404`.
- `GET /health` — `{ "ok": true, "storedImages": <count>, "uptimeSeconds": <n> }`, for uptime monitoring. Returns `503` when the storage directory is not readable and writable.

The server accepts PNG, JPEG, and WebP bodies only when their basic file
signature matches the declared content type. The upload size limit is 8MB.
Clients that disconnect or upload too slowly are closed by the server after the
configured HTTP request/header timeouts.

## Deploying on your VPS

1. Copy this folder to the server (or `git clone` if you push it to its own repo).
2. Point `cdn.adofai.turin.my` (A/AAAA record) at the VPS.
3. Put a reverse proxy in front for TLS + the public port 443, e.g. Caddy:
   ```
   cdn.adofai.turin.my {
       reverse_proxy 127.0.0.1:8787
   }
   ```
   (Caddy issues the certificate automatically. Use whatever you already run — nginx/Caddy/Cloudflare Tunnel all work the same way. If you're behind a reverse proxy, make sure it forwards `X-Forwarded-For` so rate limiting sees real client IPs instead of the proxy's.)
4. Run it persistently, e.g. with a systemd unit:
   ```ini
   [Unit]
   Description=adofai cdn server
   After=network.target

   [Service]
   WorkingDirectory=/opt/adofai-cdn-server
   ExecStart=/usr/bin/node server.js
   Environment=STORAGE_DIR=/var/lib/adofai-cdn/storage
   Restart=on-failure
   User=nobody

   [Install]
   WantedBy=multi-user.target
   ```
   Then `systemctl enable --now adofai-cdn-server`.

## Deploying from the merged repository

When Dokploy clones `turin-dev/AdofaiRichPresence` and uses the repository root
as the Docker build context, leave the Dockerfile path at the root default. The
root `Dockerfile` copies this service from `cdn-server/` and exposes port `8787`.
If you configure `cdn-server/` as the build context instead, select this folder's
`Dockerfile` and keep the same exposed port.
