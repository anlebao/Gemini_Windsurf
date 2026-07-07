import { readFileSync, existsSync } from 'fs';
import { join } from 'path';

/**
 * W4 Fix: Gateway API uses JWT Bearer auth (cookies are origin-bound).
 * global-setup.ts saves JWT tokens to auth/{role}.token files.
 * This helper creates authenticated API request contexts for Gateway calls.
 */

const AUTH_DIR = join(__dirname, '..', '..', 'auth');

/**
 * Read the JWT token for a given role.
 * Falls back to 'admin' if the requested role token doesn't exist.
 */
export function getJwtToken(role: string = 'admin'): string | null {
  const tokenPath = join(AUTH_DIR, `${role}.token`);
  if (existsSync(tokenPath)) {
    return readFileSync(tokenPath, 'utf-8').trim();
  }

  // Fallback: try admin token
  const adminTokenPath = join(AUTH_DIR, 'admin.token');
  if (existsSync(adminTokenPath)) {
    return readFileSync(adminTokenPath, 'utf-8').trim();
  }

  return null;
}

/**
 * Get the Authorization header value for Gateway API calls.
 * Returns null if no token is available.
 */
export function getAuthHeader(role: string = 'admin'): Record<string, string> {
  const token = getJwtToken(role);
  if (token) {
    return { Authorization: `Bearer ${token}` };
  }
  return {};
}
