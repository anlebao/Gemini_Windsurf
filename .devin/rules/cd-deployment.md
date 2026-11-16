# CD / DEPLOYMENT RULES — cd-multivps.yml (multi-VPS GitHub Actions)

> Lessons từ ATRS Wave 2 deploy session (2026-11-16). Áp dụng cho mọi thay đổi
> `.github/workflows/cd-multivps.yml`, `docker-compose.*.yml`, hoặc service mới
> deploy lên VPS riêng.

## 1. SECRETS TRONG `script: |` BLOCK PHẢI CÙNG INDENT (lesson 2026-11-16)

`${{ secrets.X }}` và heredoc delimiter (`EOF`) đặt ở **column 0** bên trong
`script: |` block scalar → YAML parse fail → **CD fail với 0 jobs**
("workflow file issue"), không phải job failure — rất dễ chẩn đoán sai.

- **Verify trước khi push:** `python -c "import yaml; yaml.safe_load(open('.github/workflows/cd-multivps.yml'))"`
- Render check: parse xong in `step.with.script` ra xem heredoc/ENVEOF có đúng
  column 0 trong shell script cuối cùng không (YAML strip indent chung).

## 2. GHCR PACKAGE PHẢI CONNECT VÀO REPO THÌ `GITHUB_TOKEN` PUSH ĐƯỢC

`permissions: packages: write` chỉ cấp quyền trên package **đã link repository**.
Package tạo bằng PAT/manual `docker push` có `repository: null` → push từ Actions
fail `denied: permission_denied: read_package` dù build image thành công.

- **Kiểm tra:** `gh api /users/anlebao/packages/container/<name> --jq '.repository.full_name'`
  (null = chưa connect). Package đúng (vanan-gateway/khachlink/metering/voice/ai)
  đều `anlebao/Gemini_Windsurf`.
- **Fix self-service (đã verify):** `gh api -X DELETE /users/anlebao/packages/container/<name>`
  → `gh run rerun <run-id> --failed` → GITHUB_TOKEN push tạo package MỚI auto-linked
  vào repo (giống cách vanan-metering/vanan-ai được tạo). VPS vẫn chạy image cũ
  trong cache — service không downtime; mất khả năng rollback về tag cũ (rebuild
  được từ git history nếu cần).
- GitHub **không có API** connect package↔repo — chỉ web UI; delete+recreate là
  đường self-service duy nhất.

## 3. SERVICE MỚI TRÊN VPS RIÊNG = 3 CHỖ WIRING (lesson 2026-11-16)

Thêm route/forward tới service chạy VPS khác (pattern `ATRS_REMOTE_HOST`,
`TENANTAI_REMOTE_HOST`, `METERING_REMOTE_HOST`) cần ĐỦ 3 chỗ — thiếu 1 = 502
runtime trong khi build xanh:

1. **Compose default:** `docker-compose.<svc>.yml` hoặc override env trong
   `docker-compose.gateway.yml` (`http://${X_REMOTE_HOST:-fallback}:port/`).
2. **`.env` generation trong workflow deploy** — theo preserve-pattern:
   `EXISTING_X=$(grep ...)` + `FINAL_X="${DEPLOY_X:-$EXISTING_X}"` +
   `"X_REMOTE_HOST=${FINAL_X}"` trong `printf` block + thêm `DEPLOY_X_REMOTE_HOST`
   vào `envs:` list của ssh-action + `DEPLOY_X_REMOTE_HOST: ${{ secrets.X_REMOTE_HOST }}`.
   KHÔNG thêm vào danh sách ghi đè đầy đủ nếu không có preserve — giá trị set tay
   trên VPS sẽ mất ở deploy sau.
3. **GH secret** `X_REMOTE_HOST` = IP/host thật.

Verify sau deploy: `curl https://api2.khachvip.online/<route>` — 502 = DNS/route
sai; 401/200 = route OK (auth fail là expected cho endpoint protected).

## 4. VPS OPS CHECKLIST (lesson 2026-11-16)

- **SSH GCP instance:** `gcloud compute ssh <name> --zone <zone> --project vanan-prod`
  (dùng `~/.ssh/google_compute_engine`, không cần key riêng). List:
  `gcloud compute instances list --project vanan-prod`.
- **Deploy dir ownership:** thư mục `/opt/<service>` trên VPS phải thuộc deploy
  user của service đó (`cdatrs` cho atrs, `vanan-deploy` cho gateway) — scp-action
  fail `tar: Permission denied` nếu root-owned. Fix:
  `sudo chown -R <deploy-user>:<deploy-user> /opt/<service>`.
- **ASP.NET runtime image KHÔNG có curl/wget** → docker `healthcheck: curl -sf`
  fail vĩnh viễn → container báo `unhealthy` dù app `/health` 200. Healthcheck
  nên dùng `bash -c '</dev/tcp/localhost/<port>'` hoặc loại bỏ.
- **`gh run rerun <id> --failed`** rerun failed jobs + skipped dependents —
  dùng được sau khi fix infra; `gh run watch` vẫn CẤM (credential hang — governance).
