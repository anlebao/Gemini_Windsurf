# TASK CARD — Sprint 5: nginx + SSL Multi-domain (KhachLink Multi-Profile R1)

> **Status:** ✅ COMPLETE (merged `afe84723` → `5047ed8c`)
> **Priority:** P1 — After Sprint 4 approval
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (Infrastructure Phase)

## Objective
Add nginx wildcard server block for `*.khachvip.online` KhachLink routing + create SSL SAN expand script + update deployment guide.

## Prerequisites
- [x] Sprint 4 complete (Admin UI)
- [x] Build pass

## Task 1: nginx wildcard server block
**File:** `nginx/templates/vanan.multivps.conf.template`
- Add wildcard server block AFTER existing explicit server blocks (api2, app2, www2, diemthuong2):
  ```nginx
  # KhachLink — wildcard domain support (multi-instance)
  # All *.khachvip.online (except api2/app2/www2/diemthuong2) → KhachLink container
  # KhachLink runtime resolves instance config by Host header
  server {
      listen 80;
      server_name ~^(?!api2\.|app2\.|www2\.|diemthuong2\.).+\.${VANAN_DOMAIN}$;
      location /.well-known/acme-challenge/ { root /var/www/certbot; }
      location / { return 301 https://$host$request_uri; }
  }
  server {
      listen 443 ssl http2;
      server_name ~^(?!api2\.|app2\.|www2\.|diemthuong2\.).+\.${VANAN_DOMAIN}$;
      ssl_certificate     /etc/letsencrypt/live/www2.${VANAN_DOMAIN}/fullchain.pem;
      ssl_certificate_key /etc/letsencrypt/live/www2.${VANAN_DOMAIN}/privkey.pem;
      # ... same SSL + location blocks as diemthuong2 server (proxy to ${KHACHLINK_REMOTE_HOST}:80)
  }
  ```
- Exclude diemthuong2 from wildcard (existing explicit block takes precedence, but explicit exclusion in regex is cleaner)
- Verify existing explicit server blocks still work (nginx matches most specific server_name first)

## Task 2: SSL SAN expand script
**File:** `scripts/init-ssl-khachlink-instances.sh`
- Read all CustomDomain from KhachLinkInstances table (via `docker exec vanan-postgres psql ...` OR Gateway API)
- Run `certbot certonly --webroot --expand` with all subdomains as `-d` flags (include existing www2/api2/app2/diemthuong2 + all KhachLinkInstance CustomDomains)
- Restart nginx: `docker compose -f docker-compose.gateway.yml restart nginx`
- Usage: `sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh`
- Pattern: follows `scripts/init-ssl-multivps.sh` (webroot challenge, no DNS challenge)

## Task 3: Deployment guide update
**File:** `docs/operations/Multi_VPS_Deployment_Guide.md`
- Update §6 "Mở rộng — thêm VPS mới" → add subsection "Thêm KhachLinkInstance mới":
  1. SystemAdmin → /admin/khachlink-instances → create instance with CustomDomain
  2. DNS A record `<subdomain>.khachvip.online` → gateway VPS IP
  3. Run `scripts/init-ssl-khachlink-instances.sh` on gateway VPS (expand cert)
  4. nginx auto-routes via wildcard (no restart needed if cert already covers domain)
  5. Verify: `curl https://<subdomain>.khachvip.online` → KhachLink loads

## Task 4: CD pipeline verify
- **Open question from plan:** Adding KhachLinkInstance new — does KhachLink container need restart?
- **Expected answer:** NO — runtime fetch config via API on page load. New instance available immediately.
- Verify in Sprint 5: create instance via API, access domain without restart → config loads

## Validation
- [ ] nginx config syntax check: `nginx -t` (in container)
- [ ] SSL expand script tested on dev VPS
- [ ] Wildcard routes `test.khachvip.online` → KhachLink container (existing explicit blocks still work)
- [ ] Deployment guide updated

## Files Modified (expected)
1. `nginx/templates/vanan.multivps.conf.template` — ADD wildcard server block
2. `scripts/init-ssl-khachlink-instances.sh` — NEW
3. `docs/operations/Multi_VPS_Deployment_Guide.md` — UPDATE §6

## Approval Gate
- [ ] Build pass (nginx config valid)
- [ ] User approval before Sprint 6
