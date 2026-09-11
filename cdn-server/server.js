const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const PORT = process.env.PORT || 8787;
const STORAGE_DIR = process.env.STORAGE_DIR || path.join(__dirname, "storage");
const PUBLIC_BASE_URL = (process.env.PUBLIC_BASE_URL || "https://cdn.adofai.turin.my").replace(/\/+$/, "");
const UPLOAD_SECRET = process.env.UPLOAD_SECRET || "";
const TRUST_PROXY = process.env.TRUST_PROXY === "1";
const MAX_BYTES = 8 * 1024 * 1024; // 8MB, generous for a level cover image
const TTL_MS = 2 * 60 * 60 * 1000; // images expire 2 hours after upload
const SWEEP_INTERVAL_MS = 10 * 60 * 1000;
const RATE_LIMIT_MAX = positiveInteger(process.env.RATE_LIMIT_MAX, 20); // uploads
const RATE_LIMIT_WINDOW_MS = positiveInteger(process.env.RATE_LIMIT_WINDOW_MS, 10 * 60 * 1000); // per 10 min per IP

const EXT_BY_MIME = {
    "image/png": "png",
    "image/jpeg": "jpg",
    "image/webp": "webp",
};

fs.mkdirSync(STORAGE_DIR, { recursive: true });

const rateLimitHits = new Map(); // ip -> array of timestamps

function positiveInteger(value, fallback) {
    const parsed = Number(value);
    return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function send(res, status, body, headers) {
    if (res.writableEnded) {
        return;
    }
    res.writeHead(status, {
        "Content-Type": "application/json",
        "X-Content-Type-Options": "nosniff",
        ...headers,
    });
    res.end(JSON.stringify(body));
}

function clientIp(req) {
    const forwarded = req.headers["x-forwarded-for"];
    if (TRUST_PROXY && forwarded) {
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

function cleanupRateLimitHits() {
    const now = Date.now();
    for (const [ip, hits] of rateLimitHits) {
        const activeHits = hits.filter((t) => now - t < RATE_LIMIT_WINDOW_MS);
        if (activeHits.length === 0) {
            rateLimitHits.delete(ip);
        } else {
            rateLimitHits.set(ip, activeHits);
        }
    }
}

function isExpired(filePath) {
    try {
        const stat = fs.statSync(filePath);
        return Date.now() - stat.mtimeMs > TTL_MS;
    } catch {
        return true;
    }
}

function listImageEntries() {
    try {
        return fs.readdirSync(STORAGE_DIR, { withFileTypes: true })
            .filter((entry) => entry.isFile() && /^[a-f0-9]{64}\.(png|jpg|webp)$/.test(entry.name));
    } catch (error) {
        console.error(`storage read failed: ${error.message}`);
        return null;
    }
}

function sweepExpired() {
    let removed = 0;
    const entries = listImageEntries();
    if (!entries) {
        return;
    }
    for (const entry of entries) {
        const filePath = path.join(STORAGE_DIR, entry.name);
        if (isExpired(filePath)) {
            try {
                fs.unlinkSync(filePath);
                removed++;
            } catch (error) {
                console.warn(`sweep: could not remove ${entry.name}: ${error.message}`);
            }
        }
    }
    if (removed > 0) {
        console.log(`sweep: removed ${removed} expired image(s)`);
    }
}

function handleUpload(req, res) {
    const contentType = String(req.headers["content-type"] || "")
        .split(";", 1)[0]
        .trim()
        .toLowerCase();
    const ext = EXT_BY_MIME[contentType];
    if (!ext) {
        send(res, 400, { error: "unsupported content-type, use image/png, image/jpeg, or image/webp" });
        req.resume();
        return;
    }

    if (UPLOAD_SECRET && req.headers.authorization !== `Bearer ${UPLOAD_SECRET}`) {
        send(res, 401, { error: "unauthorized" });
        req.resume();
        return;
    }

    const ip = clientIp(req);
    if (isRateLimited(ip)) {
        send(res, 429, { error: "rate limited, try again later" }, {
            "Retry-After": String(Math.ceil(RATE_LIMIT_WINDOW_MS / 1000)),
        });
        req.resume();
        return;
    }

    const declaredLength = Number(req.headers["content-length"]);
    if (Number.isFinite(declaredLength) && declaredLength > MAX_BYTES) {
        send(res, 413, { error: "file too large" });
        req.on("error", () => {});
        req.destroy();
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
        if (rejected || res.writableEnded) {
            return;
        }
        const buffer = Buffer.concat(chunks);
        if (!isValidImage(buffer, ext)) {
            send(res, 400, { error: "request body is not a valid image" });
            return;
        }
        const hash = crypto.createHash("sha256").update(buffer).digest("hex");
        const filename = `${hash}.${ext}`;
        const filePath = path.join(STORAGE_DIR, filename);

        // Written fresh (or re-touched) so its 2-hour TTL restarts from this upload.
        fs.promises.writeFile(filePath, buffer)
            .then(() => send(res, 200, { url: `${PUBLIC_BASE_URL}/i/${filename}` }))
            .catch((error) => {
                console.error(`upload write failed: ${error.message}`);
                send(res, 500, { error: "upload failed" });
            });
    });

    req.on("error", () => {
        send(res, 400, { error: "upload failed" });
    });
}

function isValidImage(buffer, ext) {
    if (ext === "png") {
        return buffer.length >= 8
            && buffer.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]));
    }
    if (ext === "jpg") {
        return buffer.length >= 3 && buffer[0] === 0xff && buffer[1] === 0xd8 && buffer[2] === 0xff;
    }
    return buffer.length >= 12
        && buffer.toString("ascii", 0, 4) === "RIFF"
        && buffer.toString("ascii", 8, 12) === "WEBP";
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
            "X-Content-Type-Options": "nosniff",
            "Cache-Control": "public, max-age=7200",
        });
        res.end(data);
    });
}

const server = http.createServer((req, res) => {
    let url;
    try {
        url = new URL(req.url, "http://localhost");
    } catch {
        send(res, 400, { error: "invalid request URL" });
        return;
    }

    if (req.method === "GET" && url.pathname === "/health") {
        const entries = listImageEntries();
        if (!entries) {
            send(res, 503, { ok: false, error: "storage unavailable" });
            return;
        }
        const count = entries.length;
        send(res, 200, { ok: true, storedImages: count, uptimeSeconds: Math.floor(process.uptime()) });
        return;
    }
    if (req.method === "POST" && url.pathname === "/upload") {
        handleUpload(req, res);
        return;
    }
    if (req.method === "GET" && url.pathname.startsWith("/i/")) {
        handleServe(req, res, url.pathname.slice("/i/".length));
        return;
    }
    send(res, 404, { error: "not found" });
});

const sweepTimer = setInterval(sweepExpired, SWEEP_INTERVAL_MS);
const rateLimitTimer = setInterval(cleanupRateLimitHits, RATE_LIMIT_WINDOW_MS);
sweepTimer.unref();
rateLimitTimer.unref();

server.listen(PORT, () => {
    console.log(`adofai cdn listening on :${server.address().port}, storage=${STORAGE_DIR}`);
    console.log(`public upload, TTL=2h, rate limit=${RATE_LIMIT_MAX}/${RATE_LIMIT_WINDOW_MS / 60000}min per IP`);
    console.log(`upload authentication=${UPLOAD_SECRET ? "enabled" : "disabled"}, trustProxy=${TRUST_PROXY}`);
});

function shutdown() {
    clearInterval(sweepTimer);
    clearInterval(rateLimitTimer);
    server.close(() => process.exit(0));
}

process.once("SIGTERM", shutdown);
process.once("SIGINT", shutdown);
