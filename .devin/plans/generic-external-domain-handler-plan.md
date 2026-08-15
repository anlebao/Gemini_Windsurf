# DETAIL CODING PLAN — Generic External Domain Handler for nginx

> **Scope:** Infrastructure (nginx config + entry point shell script)
> **Risk:** Medium (nginx misconfig → VPS down, nhưng có `nginx -t` gate + git rollback)
> **Files:** 2 modified, 0 new
> **No Domain/Service/UI changes**

---

## Problem Statement

Current entry point hardcodes `timlathay.com` check. Adding `cafevanan.net` (or any future external domain) will:
1. Not be detected → HTTPS block with missing cert → nginx fails to start → **entire VPS down**
2. HTTP-only fallback uses `return 301 https` → redirect to HTTPS when cert doesn't exist → `ERR_CERT_COMMON_NAME_INVALID`
3. No `default_server` on 443 → SNI mismatch falls back to wrong cert (`www2.khachvip.online`)

---

## Solution: 3 Changes

### Change 1: Template — marker comments + default 443 reject

**File:** `nginx/templates/vanan.multivps.conf.template`

#### 1a. Add default 443 reject block (TOP of file, before first server block)

```nginx
# ============================================================
# Default HTTPS reject — prevents wrong cert fallback for
# external domains whose cert hasn't been issued yet.
# Without this, SNI mismatch falls back to first 443 block
# (www2.khachvip.online) → ERR_CERT_COMMON_NAME_INVALID.
# ============================================================
server {
    listen 443 ssl default_server;
    ssl_reject_handshake on;
}
```

**Why:** When `timlathay.com` HTTPS block is stripped (cert not yet issued), browser sends SNI=`timlathay.com` → no match → this block catches it → connection rejected (clean error) instead of serving wrong cert.

#### 1b. Wrap timlathay.com blocks with marker comments

**Before (current):**
```nginx
# timlathay.com — KhachLink Directory instance (external domain)
# ...comment...
server {
    listen 80;
    server_name timlathay.com;
    ...
}
server {
    listen 443 ssl http2;
    server_name timlathay.com;
    ...
}
```

**After:**
```nginx
# @@EXT_DOMAIN_START:timlathay.com@@
# timlathay.com — KhachLink Directory instance (external domain)
server {
    listen 80;
    server_name timlathay.com;
    ...
    location / {
        # CHANGED: proxy instead of redirect (cert not yet issued = HTTPS would fail)
        proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
server {
    listen 443 ssl http2;
    server_name timlathay.com;
    ...
}
# @@EXT_DOMAIN_END:timlathay.com@@
```

**Key changes:**
- `@@EXT_DOMAIN_START:<domain>@@` / `@@EXT_DOMAIN_END:<domain>@@` markers — entry point parses these
- HTTP block `location /`: `return 301 https` → `proxy_pass http://${KHACHLINK_REMOTE_HOST}:80` (serve content over HTTP while cert pending)

### Change 2: Entry point — generic domain detection loop

**File:** `nginx/docker-entrypoint.multivps.sh`

**Replace** the entire hardcoded timlathay.com block (lines 51-81) with:

```sh
# ============================================================
# Generic external domain handler
# Detects all @@EXT_DOMAIN_START:<domain>@@ markers in config.
# For each domain, if SSL cert doesn't exist yet:
#   - Strip the HTTPS server block (443) — prevents nginx fail on missing cert
#   - Keep the HTTP server block (80) — serves ACME challenge + proxies content
# After certbot issues cert, restart nginx → entry point keeps HTTPS block.
# ============================================================
EXT_DOMAINS=$(grep -oP '@@EXT_DOMAIN_START:\K[^@]+(?=@@)' "${CONF_DIR}/vanan.conf" 2>/dev/null || echo "")

if [ -n "$EXT_DOMAINS" ]; then
    for DOMAIN in $EXT_DOMAINS; do
        CERT="/etc/letsencrypt/live/${DOMAIN}/fullchain.pem"
        if ! [ -f "$CERT" ]; then
            echo "[nginx-entrypoint-multivps] ${DOMAIN} cert NOT found — stripping HTTPS block"
            # Strip the HTTPS server block (listen 443) for this domain only.
            # Match from "listen 443" line within the domain's section to the closing "}".
            # Use awk for reliable multi-line block deletion.
            awk -v domain="$DOMAIN" '
                /^@@EXT_DOMAIN_START:'"$DOMAIN"'@@/ { in_section=1 }
                in_section && /listen 443/ { skip=1 }
                skip && /^}/ { skip=0; next }
                !skip { print }
                /^@@EXT_DOMAIN_END:'"$DOMAIN"'@@/ { in_section=0 }
            ' "${CONF_DIR}/vanan.conf" > "${CONF_DIR}/vanan.conf.tmp" && mv "${CONF_DIR}/vanan.conf.tmp" "${CONF_DIR}/vanan.conf"
            echo "[nginx-entrypoint-multivps] ${DOMAIN} HTTPS block stripped — HTTP block retained (ACME + proxy)"
        else
            echo "[nginx-entrypoint-multivps] ${DOMAIN} cert found — keeping HTTPS block"
        fi
    done
else
    echo "[nginx-entrypoint-multivps] No external domains detected"
fi
```

**Why generic:**
- `grep -oP '@@EXT_DOMAIN_START:\K[^@]+(?=@@)'` extracts ALL domain names from markers
- Loop processes each domain independently
- Adding `cafevanan.net` = just add block to template with markers — **zero entry point changes**

### Change 3: Workflow — no changes needed

`setup-timlathay.yml` already:
1. Deploys nginx config + entry point via scp
2. Restarts nginx (entry point strips HTTPS block if cert missing)
3. Runs certbot (ACME challenge via HTTP block)
4. Restarts nginx again (entry point keeps HTTPS block now cert exists)
5. Seeds KhachLinkInstance
6. Verifies

**No workflow changes required** — the generic handler is transparent.

---

## Future: Adding `cafevanan.net` (Reseller tenant)

After this fix, adding a new external domain requires **only 1 file change**:

**File:** `nginx/templates/vanan.multivps.conf.template` — append:

```nginx
# @@EXT_DOMAIN_START:cafevanan.net@@
# cafevanan.net — KhachLink Reseller instance
server {
    listen 80;
    server_name cafevanan.net;
    location /.well-known/acme-challenge/ { root /var/www/certbot; }
    location / {
        proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
server {
    listen 443 ssl http2;
    server_name cafevanan.net;
    ssl_certificate     /etc/letsencrypt/live/cafevanan.net/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/cafevanan.net/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;
    # ... (same proxy blocks as timlathay.com)
}
# @@EXT_DOMAIN_END:cafevanan.net@@
```

Then: DNS A record → CD deploy → run `setup-timlathay.yml` equivalent for cafevanan.net → done.

**Zero entry point changes. Zero risk of VPS down.**

---

## Verification Plan

| Step | How | Expected |
|---|---|---|
| 1. Local `nginx -t` | `docker run --rm -v $(pwd)/nginx:/etc/nginx nginx:1.25-alpine nginx -t` | Syntax OK |
| 2. Entry point dry run | Simulate: create temp config with markers, run awk logic | HTTPS block stripped correctly |
| 3. CD deploy | Push to main → CD workflow runs | nginx container starts |
| 4. HTTP access | `curl http://timlathay.com` | 200 (proxied to KhachLink) |
| 5. HTTPS before cert | `curl -k https://timlathay.com` | Connection rejected (ssl_reject_handshake) |
| 6. Run certbot | `setup-timlathay.yml` workflow | Cert issued |
| 7. HTTPS after cert | `curl https://timlathay.com` | 200 (valid cert) |
| 8. by-domain API | `curl https://api2.khachvip.online/api/v1/khachlink-instances/by-domain/timlathay.com` | 200 + Directory profile DTO |

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| awk strips wrong block | Low | Medium (nginx wrong config) | awk pattern matches exact domain name in marker |
| `ssl_reject_handshake` not supported | Low | Low (nginx version check) | nginx 1.25-alpine supports it (added in 1.19.4) |
| envsubst breaks markers | Low | Low | Markers use `@@` not `${}` — envsubst ignores them |
| CD deploys mid-certbot | Low | Medium | Workflow is manual trigger, not auto |

---

## Files Summary

| File | Change | Lines |
|---|---|---|
| `nginx/templates/vanan.multivps.conf.template` | Add default 443 reject + markers + proxy in HTTP block | ~30 added, ~5 changed |
| `nginx/docker-entrypoint.multivps.sh` | Replace hardcoded timlathay block with generic loop | ~30 replaced |
| `.github/workflows/setup-timlathay.yml` | No changes | 0 |

**Total: ~65 lines changed across 2 files. No Domain/Service/UI/business logic changes.**
