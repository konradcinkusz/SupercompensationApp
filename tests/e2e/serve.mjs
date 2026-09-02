// A static server that mimics GitHub Pages closely enough for the smoke test to mean
// something.
//
// Two behaviours matter and a plain file server gets both wrong:
//
//   1. The app is served under a BASE PATH (/SupercompensationApp/), because that is
//      where a project page lives and what pages.yml rewrites <base href> to. Serving it
//      at / would bypass the exact thing most likely to be broken.
//   2. An unknown path returns 404.html WITH STATUS 404, which is what Pages does and
//      what makes the SPA fallback work for a deep link or a refresh on /chart.
//
// Written rather than pulled from npm so the server's behaviour is reviewable and pinned
// to nothing.

import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { join, extname, normalize } from 'node:path';

const root = process.argv[2];
const basePath = process.argv[3] || '/';
const port = Number(process.argv[4] || 8080);

// Explicit, because a wrong Content-Type on .wasm makes the browser fall back from
// streaming instantiation with a console warning, and .br/.gz would be served as
// octet-stream.
const TYPES = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.wasm': 'application/wasm',
    '.dat': 'application/octet-stream',
    '.dll': 'application/octet-stream',
    '.pdb': 'application/octet-stream',
    '.blat': 'application/octet-stream',
    '.svg': 'image/svg+xml',
    '.png': 'image/png',
    '.ico': 'image/x-icon',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.txt': 'text/plain; charset=utf-8',
};

async function readIfFile(path) {
    try {
        const s = await stat(path);
        if (!s.isFile()) return null;
        return await readFile(path);
    } catch {
        return null;
    }
}

const server = createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    let pathname = decodeURIComponent(url.pathname);

    if (!pathname.startsWith(basePath)) {
        res.writeHead(404, { 'Content-Type': 'text/plain' });
        res.end(`outside base path ${basePath}`);
        return;
    }

    let rel = pathname.slice(basePath.length) || 'index.html';
    if (rel.endsWith('/')) rel += 'index.html';

    // normalize collapses any ../ before it reaches the filesystem.
    const file = join(root, normalize('/' + rel));
    const body = await readIfFile(file);

    if (body) {
        res.writeHead(200, {
            'Content-Type': TYPES[extname(file).toLowerCase()] || 'application/octet-stream',
            'Cache-Control': 'no-store',
        });
        res.end(body);
        return;
    }

    // The SPA fallback, with the status Pages actually returns.
    const notFound = await readIfFile(join(root, '404.html'));
    res.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
    res.end(notFound ?? 'not found');
});

server.listen(port, () => {
    console.log(`serving ${root} at http://localhost:${port}${basePath}`);
});
