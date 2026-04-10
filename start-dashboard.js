const http = require('http');
const fs = require('fs');
const path = require('path');

const server = http.createServer((req, res) => {
    // Enable CORS
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    
    // Handle file requests
    let filePath = path.join(__dirname, req.url === '/' ? 'VanAn_Dashboard.html' : req.url);
    
    // Security: prevent directory traversal
    if (!filePath.startsWith(__dirname)) {
        res.writeHead(403);
        res.end('Forbidden');
        return;
    }
    
    const ext = path.extname(filePath);
    const contentType = {
        '.html': 'text/html',
        '.css': 'text/css',
        '.js': 'text/javascript',
        '.json': 'application/json',
        '.png': 'image/png',
        '.jpg': 'image/jpeg',
        '.ico': 'image/x-icon'
    }[ext] || 'text/plain';
    
    fs.readFile(filePath, (err, data) => {
        if (err) {
            res.writeHead(404);
            res.end('File not found');
            return;
        }
        
        res.writeHead(200, { 'Content-Type': contentType });
        res.end(data);
    });
});

const PORT = 8082;
server.listen(PORT, () => {
    console.log(`🚀 VanAn Dashboard server running on http://localhost:${PORT}`);
    console.log(`📊 Open http://localhost:${PORT} to view dashboard`);
});

// Auto-shutdown after 5 minutes to prevent hanging
setTimeout(() => {
    console.log('🔧 Auto-shutting down dashboard server...');
    server.close();
    process.exit(0);
}, 300000);
