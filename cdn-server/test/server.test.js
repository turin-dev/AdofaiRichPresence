const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const net = require("node:net");
const os = require("node:os");
const path = require("node:path");
const { spawn } = require("node:child_process");
const { test } = require("node:test");

const serverPath = path.join(__dirname, "..", "server.js");
const png = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
    "base64",
);

async function reservePort() {
    const probe = net.createServer();
    await new Promise((resolve, reject) => {
        probe.once("error", reject);
        probe.listen(0, "127.0.0.1", resolve);
    });
    const port = probe.address().port;
    await new Promise((resolve, reject) => {
        probe.close((error) => (error ? reject(error) : resolve()));
    });
    return port;
}

async function startServer(secret = "") {
    const port = await reservePort();
    const storageDir = await fs.mkdtemp(path.join(os.tmpdir(), "adofai-cdn-test-"));
    const child = spawn(process.execPath, [serverPath], {
        env: {
            ...process.env,
            PORT: String(port),
            STORAGE_DIR: storageDir,
            PUBLIC_BASE_URL: `http://127.0.0.1:${port}`,
            UPLOAD_SECRET: secret,
        },
        stdio: ["ignore", "pipe", "pipe"],
    });

    await new Promise((resolve, reject) => {
        let output = "";
        const onData = (chunk) => {
            output += chunk.toString();
            if (output.includes("adofai cdn listening")) {
                cleanup();
                resolve();
            }
        };
        const onExit = (code) => {
            cleanup();
            reject(new Error(`server exited before ready (${code}): ${output}`));
        };
        const cleanup = () => {
            child.stdout.off("data", onData);
            child.off("exit", onExit);
        };
        child.stdout.on("data", onData);
        child.once("exit", onExit);
    });

    return {
        baseUrl: `http://127.0.0.1:${port}`,
        storageDir,
        async stop() {
            if (!child.killed) {
                child.kill();
            }
            await new Promise((resolve) => child.once("close", resolve));
            await fs.rm(storageDir, { recursive: true, force: true });
        },
    };
}

test("accepts a real image, ignores query strings, and does not rate-limit invalid MIME requests", async (t) => {
    const app = await startServer();
    t.after(() => app.stop());

    const invalid = await fetch(`${app.baseUrl}/upload`, {
        method: "POST",
        headers: { "Content-Type": "text/plain" },
        body: "not an image",
    });
    assert.equal(invalid.status, 400);

    const uploaded = await fetch(`${app.baseUrl}/upload?source=test`, {
        method: "POST",
        headers: { "Content-Type": "image/png; charset=binary" },
        body: png,
    });
    assert.equal(uploaded.status, 200);
    const result = await uploaded.json();
    assert.match(result.url, /\/i\/[a-f0-9]{64}\.png$/);

    const image = await fetch(result.url);
    assert.equal(image.status, 200);
    assert.equal(image.headers.get("content-type"), "image/png");
    assert.equal(image.headers.get("x-content-type-options"), "nosniff");
    assert.deepEqual(Buffer.from(await image.arrayBuffer()), png);

    const health = await fetch(`${app.baseUrl}/health?details=1`);
    assert.equal(health.status, 200);
    assert.equal(health.headers.get("x-content-type-options"), "nosniff");
    assert.equal((await health.json()).storedImages, 1);
});

test("enforces the optional upload secret", async (t) => {
    const app = await startServer("test-secret");
    t.after(() => app.stop());

    const unauthorized = await fetch(`${app.baseUrl}/upload`, {
        method: "POST",
        headers: { "Content-Type": "image/png" },
        body: png,
    });
    assert.equal(unauthorized.status, 401);

    const authorized = await fetch(`${app.baseUrl}/upload`, {
        method: "POST",
        headers: {
            Authorization: "Bearer test-secret",
            "Content-Type": "image/png",
        },
        body: png,
    });
    assert.equal(authorized.status, 200);
});

test("reports storage failures without terminating the server", async (t) => {
    const app = await startServer();
    t.after(() => app.stop());

    await fs.rm(app.storageDir, { recursive: true, force: true });
    const unavailable = await fetch(`${app.baseUrl}/health`);
    assert.equal(unavailable.status, 503);
    assert.deepEqual(await unavailable.json(), { ok: false, error: "storage unavailable" });
});
