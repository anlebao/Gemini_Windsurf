// Static server cho voice SDK demo (Playwright webServer). Node http, không dependency.
// Phục vụ toàn bộ 10_Voice/vanan-voice/sdk tại http://127.0.0.1:5080
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('../../10_Voice/vanan-voice/sdk/', import.meta.url));
const PORT = 5080;
const MIME = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.json': 'application/json'
};

const server = createServer(async (req, res) => {
    try {
        const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
        const filePath = normalize(join(ROOT, urlPath === '/' ? 'index.html' : urlPath));
        if (!filePath.startsWith(ROOT)) { res.writeHead(403); res.end(); return; }
        const body = await readFile(filePath);
        res.writeHead(200, { 'Content-Type': MIME[extname(filePath)] || 'application/octet-stream' });
        res.end(body);
    } catch {
        res.writeHead(404); res.end('not found');
    }
});

server.listen(PORT, '127.0.0.1', () => console.log(`voice demo server: http://127.0.0.1:${PORT}`));