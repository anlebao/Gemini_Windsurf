# VanAn Rollback SOP — Immutable Image Tags

**Target: ≤ 2–3 phút rollback.** Không rebuild, không redeploy app không bị ảnh hưởng.

## Nguyên tắc

- Mỗi CD run tag image bằng **full git SHA** (`IMAGE_TAG = github.sha`). Immutable — không bao giờ bị ghi đè.
- VPS lưu `IMAGE_TAG` trong `.env.gateway` / `.env.khachlink` / `.env.shoperp` — rollback = đổi dòng này + pull + up.
- Rollback **không undo DB migration** (migration forward-only). Nếu deploy lỗi đã chạy migration → rollback code, xử lý data fix-forward.

## Bước 1 — Biết VPS đang chạy SHA nào

```bash
# trên từng VPS
cd /opt/vanan
sudo docker compose -f docker-compose.shoperp.yml ps --format json | grep -i image
# hoặc xem CD run gần nhất: job smoke-test -> step "Log deploy scope" -> image_tag
```

## Bước 2 — Rollback 1 app

```bash
# SSH vào VPS tương ứng rồi chạy (script nằm trong repo, CD đã scp sẵn)
./scripts/rollback-app.sh shoperp <prev-sha>
#   gateway   -> SSH vanan-gateway
#   khachlink -> SSH vanan-khachlink  (rollback directory dùng chung script này, chỉ đổi IMAGE_TAG)
#   shoperp   -> SSH vanan-shop-a
```

Script: đổi `IMAGE_TAG` trong `.env.*`, pull, `up -d --wait` (healthcheck gate), in trạng thái.

## Bước 3 — Verify

```bash
curl -fsS http://localhost/health   # trên chính VPS
# + xem log container để xác nhận version cũ đang serve
```

## Khi nào rollback, khi nào fix-forward

| Tình huống | Hành động |
|---|---|
| Crash / 500 / data sai ngay sau deploy | Rollback code ngay (2-3 phút) |
| Migration đã chạy nhưng code mới lỗi | Rollback code + fix-forward data (KHÔNG rollback DB) |
| Lỗi cấu hình env (.env) | Sửa .env + `docker compose up -d` lại — không cần rollback image |
| Lỗi chỉ ở 1 app | Chỉ rollback app đó — không đụng app khác |

## Lưu ý

- CD run kế tiếp sẽ ghi đè `IMAGE_TAG` bằng SHA mới (deploy script viết lại `.env.*` mỗi lần).
- Manual `workflow_dispatch` với `image_tag=latest` vẫn hoạt động như trước (deploy full theo `latest`).
