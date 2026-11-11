// Mint HS256 staff JWT cho voice demo / E2E — claims sub + tenant_id (giống JwtTokenService).
// Usage: node mint-token.mjs <userId> <tenantId> <secret> [issuer] [audience]
import { createHmac, randomBytes } from 'node:crypto';

const [userId, tenantId, secret, issuer = 'VanAnShopERP', audience = 'VanAnApi'] = process.argv.slice(2);
if (!userId || !tenantId || !secret) {
    console.error('Usage: node mint-token.mjs <userId> <tenantId> <secret> [issuer] [audience]');
    process.exit(1);
}

const now = Math.floor(Date.now() / 1000);
const header = { alg: 'HS256', typ: 'JWT' };
const payload = {
    sub: userId,
    tenant_id: tenantId,
    nbf: now,
    exp: now + 3600,
    iat: now,
    iss: issuer,
    aud: audience
};

const b64 = (obj) => Buffer.from(JSON.stringify(obj)).toString('base64url');
const signingInput = `${b64(header)}.${b64(payload)}`;
const signature = createHmac('sha256', secret).update(signingInput).digest('base64url');
console.log(`${signingInput}.${signature}`);