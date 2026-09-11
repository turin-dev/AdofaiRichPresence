const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const PORT = process.env.PORT || 8787;
const STORAGE_DIR = process.env.STORAGE_DIR || path.join(__dirname, "storage");
const MAX_BYTES = 8 * 1024 * 1024; // 8MB, generous for a level cover image
const TTL_MS = 2 * 60 * 60 * 1000; // images expire 2 hours after upload
const SWEEP_INTERVAL_MS = 10 * 60 * 1000;
const RATE_LIMIT_MAX = Number(process.env.RATE_LIMIT_MAX || 20); // uploads
const RATE_LIMIT_WINDOW_MS = Number(process.env.RATE_LIMIT_WINDOW_MS || 10 * 60 * 1000); // per 10 min per IP

const EXT_BY_MIME = {
    "image/png": "png",
    "image/jpeg": "jpg",
    "image/webp": "webp",
};

fs.mkdirSync(STORAGE_DIR, { recursive: true });

const rateLimitHits = new Map(); // ip -> array of timestamps

function send(res, status, body, headers) {
    res.writeHead(status, { "Content-Type": "application/json", ...headers });
    res.end(JSON.stringify(body));
}

function clientIp(req) {
    const forwarded = req.headers["x-forwarded-for"];
    if (forwarded) {
        return forwarded.split(",")[0].trim();
    }
    return req.socket.remoteAddress || "unknown";
}

function isRateLimited(ip) {
    const now = Date.now();
    const hits = (rateLimitHits.get(ip) || []).filter((t) => now - t < RATE_LIMIT_WINDOW_MS);
    hits.push(now);
    rateLimitHits.set(ip, hits);
    return hits.length > RATE_LIMIT_MAX;
}

function isExpired(filePath) {
    try {
        const stat = fs.statSync(filePath);
        return Date.now() - stat.mtimeMs > TTL_MS;
    } catch {
        return true;
    }
}

function sweepExpired() {
    let removed = 0;
    for (const name of fs.readdirSync(STORAGE_DIR)) {
        const filePath = path.join(STORAGE_DIR, name);
        if (isExpired(filePath)) {
            fs.unlinkSync(filePath);
            removed++;
        }
    }
    if (removed > 0) {
        console.log(`sweep: removed ${removed} expired image(s)`);
    }
}

function handleUpload(req, res) {
    const ip = clientIp(req);
    if (isRateLimited(ip)) {
        send(res, 429, { error: "rate limited, try again later" });
        return;
    }

    const ext = EXT_BY_MIME[req.headers["content-type"]];
    if (!ext) {
        send(res, 400, { error: "unsupported content-type, use image/png, image/jpeg, or image/webp" });
        return;
    }

    const chunks = [];
    let total = 0;
    let rejected = false;

    req.on("data", (chunk) => {
        total += chunk.length;
        if (total > MAX_BYTES) {
            rejected = true;
            send(res, 413, { error: "file too large" });
            req.destroy();
            return;
        }
        chunks.push(chunk);
    });

    req.on("end", () => {
        if (rejected) {
            return;
        }
        const buffer = Buffer.concat(chunks);
        const hash = crypto.createHash("sha256").update(buffer).digest("hex");
        const filename = `${hash}.${ext}`;
        const filePath = path.join(STORAGE_DIR, filename);

        // Written fresh (or re-touched) so its 2-hour TTL restarts from this upload.
        fs.writeFileSync(filePath, buffer);

        send(res, 200, { url: `https://cdn.adofai.turin.my/i/${filename}` });
    });

    req.on("error", () => {
        send(res, 400, { error: "upload failed" });
    });
}

function handleServe(req, res, filename) {
    if (!/^[a-f0-9]{64}\.(png|jpg|webp)$/.test(filename)) {
        send(res, 404, { error: "not found" });
        return;
    }
    const filePath = path.join(STORAGE_DIR, filename);
    if (isExpired(filePath)) {
        fs.unlink(filePath, () => {});
        send(res, 404, { error: "not found" });
        return;
    }
    fs.readFile(filePath, (err, data) => {
        if (err) {
            send(res, 404, { error: "not found" });
            return;
        }
        const ext = filename.split(".").pop();
        const mime = Object.keys(EXT_BY_MIME).find((m) => EXT_BY_MIME[m] === ext) || "application/octet-stream";
        res.writeHead(200, {
            "Content-Type": mime,
            "Cache-Control": "public, max-age=7200",
        });
        res.end(data);
    });
}

const server = http.createServer((req, res) => {
    if (req.method === "GET" && req.url === "/health") {
        const count = fs.readdirSync(STORAGE_DIR).length;
        send(res, 200, { ok: true, storedImages: count, uptimeSeconds: Math.floor(process.uptime()) });
        return;
    }
    if (req.method === "POST" && req.url === "/upload") {
        handleUpload(req, res);
        return;
    }
    if (req.method === "GET" && req.url.startsWith("/i/")) {
        handleServe(req, res, req.url.slice("/i/".length));
        return;
    }
    send(res, 404, { error: "not found" });
});

setInterval(sweepExpired, SWEEP_INTERVAL_MS);

server.listen(PORT, () => {
    console.log(`adofai cdn listening on :${PORT}, storage=${STORAGE_DIR}`);
    console.log(`public upload, TTL=2h, rate limit=${RATE_LIMIT_MAX}/${RATE_LIMIT_WINDOW_MS / 60000}min per IP`);
});
