#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# RV W3f-b — Learning Suggest opt-in (feat 3540b3aa) — SWEEP trên VPS vanan-ai.
#
# Focus: deploy marker · migration system_settings · learning endpoints
# (GET default OFF → PUT on → PG row → PUT off restore) · sourceRef trong
# docs list · global flag absent=ON · auth subset · chat regression · logs.
#
# Secrets đọc từ /opt/vanan-ai/.env.tenantai — KHÔNG in ra log.
# Chạy:  bash /tmp/rv-w3f/rv-w3fb-sweep.sh   (từ gcloud ssh / scp)
# ─────────────────────────────────────────────────────────────────────────────
set -uo pipefail

AI_BASE="${AI_BASE:-http://localhost:5060}"
ENV_FILE="${ENV_FILE:-/opt/vanan-ai/.env.tenantai}"
TENANT_A="${RV_TENANT_A:-e2d91cdf-d3c4-4399-b96e-e772a7a41630}"   # An Tâm
EXPECT_IMAGE="${EXPECT_IMAGE:-3540b3aa20a9e40a13f89e445a2bed4e6e5de9d8}"
RESP=/tmp/rv-w3fb-resp.json
PASS=0; FAIL=0; SKIP=0

say()  { echo "[$(date +%H:%M:%S)] $*"; }
ok()   { PASS=$((PASS+1)); echo "  PASS  $1"; }
bad()  { FAIL=$((FAIL+1)); echo "  FAIL  $1"; }
skip() { SKIP=$((SKIP+1)); echo "  SKIP  $1"; }
expect_code() { if [ "$2" = "$3" ]; then ok "$1 ($3)"; else bad "$1 (expected=$2 got=$3)"; fi }

# ---- secrets ----
if [ ! -r "$ENV_FILE" ]; then
  TMPENV=$(mktemp); chmod 600 "$TMPENV"
  sudo -n cat "$ENV_FILE" > "$TMPENV" 2>/dev/null && ENV_FILE="$TMPENV" \
    || { echo "ERROR: không đọc được $ENV_FILE"; exit 2; }
fi
val() { grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2- | tr -d '\r'; }
SIGN_SECRET="$(val TENANTAI_CTX_SIGNING_SECRET)"
BOOT_INGEST="$(val TENANTAI_BOOTSTRAP_INGEST)"
GW_ID="$(val TENANTAI_BOOTSTRAP_CALLER)"; GW_KEY="${GW_ID#*:}"; GW_ID="${GW_ID%%:*}"
IN_ID="${BOOT_INGEST%%:*}"; IN_KEY="${BOOT_INGEST#*:}"
[ -n "$SIGN_SECRET" ] && [ -n "$IN_KEY" ] || { echo "ERROR: thiếu secrets"; exit 2; }

b64url() { base64 -w0 | tr '+/' '-_' | tr -d '='; }
sign_ctx() { # tenant caller reqid
  CTX=$(printf '{"tenantId":"%s","callerId":"%s","iat":%s,"exp":%s,"requestId":"%s"}' \
        "$1" "$2" "$(date +%s)" "$(($(date +%s)+120))" "$3" | b64url)
  SIG=$(printf '%s' "$CTX" | openssl dgst -sha256 -hmac "$SIGN_SECRET" -binary | b64url)
}
req() { # m p caller key tenant [body]
  local m="$1" p="$2" caller="$3" key="$4" tenant="$5" body="${6:-}"
  local args=(-sS -o "$RESP" -w '%{http_code}' -X "$m" "$AI_BASE$p"
              -H "X-Caller-Id: $caller" -H "X-Ai-Key: $key")
  sign_ctx "$tenant" "$caller" "rv-$RANDOM"
  args+=(-H "X-Tenant-Ctx: $CTX" -H "X-Tenant-Ctx-Sig: $SIG")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' -d "$body")
  curl --max-time 40 "${args[@]}"
}

PG_PASS="$(val TENANTAI_PG_PASSWORD)"
PGQ() { sudo docker exec -e PGPASSWORD="$PG_PASS" vanan-ai-pg-1 \
        psql -U vanan_ai -d vanan_ai -tAc "$1"; }

echo "════════════════════════════════════════════════════════"
say "RV W3f-b sweep @ $AI_BASE · tenant=$TENANT_A · expect=$EXPECT_IMAGE"

# ── 1. deploy marker ──
say "1) deploy marker"
IMG=$(sudo docker ps --format '{{.Image}}' | grep -i 'vanan-ai' | head -1)
case "$IMG" in *"$EXPECT_IMAGE"*) ok "image = $EXPECT_IMAGE";; *) bad "image=$IMG";; esac

# ── 2. health ──
say "2) health"
code=$(curl -sS -o "$RESP" -w '%{http_code}' --max-time 15 "$AI_BASE/v1/health")
expect_code "health 200" 200 "$code"
grep -q '"db":"up"' "$RESP" && ok "db=up" || bad "db not up"

# ── 3. migration system_settings ──
say "3) migration AddSystemSettings"
TBL=$(PGQ "SELECT to_regclass('system_settings') IS NOT NULL;")
[ "$TBL" = "t" ] && ok "bảng system_settings tồn tại" || bad "system_settings missing"
COLS=$(PGQ "SELECT count(*) FROM information_schema.columns WHERE table_name='system_settings' AND column_name IN ('key','value');")
[ "$COLS" = "2" ] && ok "cols key/value" || bad "cols=$COLS"
GLOBAL=$(PGQ "SELECT count(*) FROM system_settings WHERE key='LearningSuggest:Enabled';")
[ "$GLOBAL" = "0" ] && ok "global row absent → default ON (ops chưa set)" \
  || ok "global row = $(PGQ "SELECT value FROM system_settings WHERE key='LearningSuggest:Enabled';")"

# ── 4. learning endpoints (GET default → PUT on → PG → PUT off restore) ──
say "4) learning opt-in endpoints"
KEY="LearningSuggest:${TENANT_A}:Enabled"
BEFORE=$(PGQ "SELECT coalesce((SELECT value FROM system_settings WHERE key='$KEY'),'absent');")

code=$(req GET "/v1/admin/tenants/$TENANT_A/learning" "$IN_ID" "$IN_KEY" "$TENANT_A")
expect_code "GET /learning → 200" 200 "$code"
grep -q '"enabled":false' "$RESP" && ok "absent = enabled:false (default OFF)" \
  || { grep -q '"enabled":true' "$RESP" && ok "enabled:true (đã opt-in sẵn)" || bad "body: $(cat "$RESP")"; }

code=$(req PUT "/v1/admin/tenants/$TENANT_A/learning" "$IN_ID" "$IN_KEY" "$TENANT_A" '{"enabled":true}')
expect_code "PUT enabled:true → 200" 200 "$code"
code=$(req GET "/v1/admin/tenants/$TENANT_A/learning" "$IN_ID" "$IN_KEY" "$TENANT_A")
grep -q '"enabled":true' "$RESP" && ok "GET sau PUT = enabled:true" || bad "GET sau PUT: $(cat "$RESP")"
VAL=$(PGQ "SELECT value FROM system_settings WHERE key='$KEY';")
[ "$VAL" = "true" ] && ok "PG row $KEY = true" || bad "PG row: $VAL"

# restore về trạng thái ban đầu (không để tenant opt-in trên prod)
if [ "$BEFORE" = "absent" ]; then
  code=$(req PUT "/v1/admin/tenants/$TENANT_A/learning" "$IN_ID" "$IN_KEY" "$TENANT_A" '{"enabled":false}')
  expect_code "restore PUT enabled:false → 200" 200 "$code"
  PGQ "DELETE FROM system_settings WHERE key='$KEY';" >/dev/null
  [ "$(PGQ "SELECT count(*) FROM system_settings WHERE key='$KEY';")" = "0" ] \
    && ok "restore: row về absent" || bad "restore fail"
else
  code=$(req PUT "/v1/admin/tenants/$TENANT_A/learning" "$IN_ID" "$IN_KEY" "$TENANT_A" "{\"enabled\":$BEFORE}")
  expect_code "restore PUT enabled:$BEFORE → 200" 200 "$code"
fi

# tenant khác (chưa opt-in) → default OFF
TENANT_B="99999999-0000-0000-0000-000000000099"
code=$(req GET "/v1/admin/tenants/$TENANT_B/learning" "$IN_ID" "$IN_KEY" "$TENANT_B")
grep -q '"enabled":false' "$RESP" && ok "tenant B absent = OFF" || bad "tenant B: $code $(cat "$RESP")"

# forged sig → 401 (T6 trên endpoint mới)
CTX=$(printf '{"tenantId":"%s","callerId":"%s","iat":%s,"exp":%s,"requestId":"rv-f"}' \
      "$TENANT_A" "$IN_ID" "$(date +%s)" "$(($(date +%s)+120))" | b64url)
code=$(curl -sS -o "$RESP" -w '%{http_code}' --max-time 15 \
  "$AI_BASE/v1/admin/tenants/$TENANT_A/learning" \
  -H "X-Caller-Id: $IN_ID" -H "X-Ai-Key: $IN_KEY" \
  -H "X-Tenant-Ctx: $CTX" -H "X-Tenant-Ctx-Sig: AAAA")
expect_code "forged sig → 401 (T6)" 401 "$code"

# ── 5. sourceRef trong docs list (badge AI đề xuất) ──
say "5) docs list có sourceRef"
code=$(req GET "/v1/admin/knowledge/documents?size=5" "$IN_ID" "$IN_KEY" "$TENANT_A")
expect_code "docs list → 200" 200 "$code"
grep -q '"sourceRef"' "$RESP" && ok "field sourceRef có trong response" || bad "sourceRef missing: $(cat "$RESP")"
N_CL=$(PGQ "SELECT count(*) FROM knowledge_documents WHERE source_ref LIKE 'chat-learn:%';")
[ "$N_CL" = "0" ] && ok "0 chat-learn docs (default OFF đúng)" || ok "$N_CL chat-learn docs (có opt-in)"

# ── 6. chat regression nhanh ──
say "6) chat regression"
chat() { sign_ctx "$1" "$GW_ID" "rv-$RANDOM"
  curl -sS -o "$RESP" -w '%{http_code}' --max-time 40 -X POST "$AI_BASE/v1/chat" \
    -H "X-Caller-Id: $GW_ID" -H "X-Ai-Key: $GW_KEY" \
    -H "X-Tenant-Ctx: $CTX" -H "X-Tenant-Ctx-Sig: $SIG" \
    -H 'Content-Type: application/json' \
    -d "{\"sessionId\":\"rv-w3fb-$RANDOM\",\"messageId\":\"rv-w3fb-m$RANDOM$RANDOM\",\"text\":\"$2\"}"; }
code=$(chat "$TENANT_A" "Cho mình xem menu")
expect_code "chat → 200" 200 "$code"
grep -q '"refused":true' "$RESP" && bad "chat refused" || ok "chat not refused"
# cleanup session test
sudo docker exec -e PGPASSWORD="$PG_PASS" vanan-ai-pg-1 psql -U vanan_ai -d vanan_ai -q \
  -c "DELETE FROM conversation_messages WHERE session_id LIKE 'rv-w3fb-%'; DELETE FROM conversation_sessions WHERE session_id LIKE 'rv-w3fb-%';" >/dev/null 2>&1

# ── 7. container logs ──
say "7) container logs"
sudo docker logs vanan-ai-1 --since 15m 2>&1 | grep -iE "exception|unhandled|fail.*migrat" | grep -v "log level" | head -5 \
  > /tmp/rv-w3fb-loglines.txt
if [ -s /tmp/rv-w3fb-loglines.txt ]; then bad "log error: $(cat /tmp/rv-w3fb-loglines.txt)"; else ok "log sạch"; fi

echo "════════════════════════════════════════════════════════"
say "KẾT QUẢ: PASS=$PASS FAIL=$FAIL SKIP=$SKIP"
[ "$FAIL" -eq 0 ]
