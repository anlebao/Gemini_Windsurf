# CI/CD Benchmark — Baseline & Acceptance Targets

## Baseline (measured 2026-09-26, 10 most recent CD runs before P1 wiring)

Source: GitHub Actions runs of `cd-multivps.yml` (events: push, all success).

| Metric | p50 | p95 | max |
|---|---|---|---|
| `Build & Push Images` job (5 images, no-cache, sequential) | 346s (~5.8m) | 381s (~6.4m) | 381s |
| Total CD workflow (build + deploy 3 VPS + smoke) | 610s (~10.2m) | 708s (~11.8m) | 708s |

Plus local pre-push `ci-full.ps1 -SkipE2E`: ~12-17 min.
=> Fix 1 dòng → production ≈ **25-30 phút**. Đây là gốc bệnh P1 giải quyết.

## First P1 run (2026-09-26, commit 945413bf, CD run 36222283090 — SUCCESS)

Full pipeline (5 builds, no cache yet — cache warms on this run):

| Metric | Baseline p50 | First P1 run | Delta |
|---|---|---|---|
| Total CD workflow | 610s | **418s** | -31% |
| Longest build (gateway, was sequential sum 346s) | 346s | **202s** | -42% |
| Directory / crawler / khachlink / shoperp builds | — | 53s / 51s / 104s / 156s (parallel) | — |

- Impact analysis job ran correctly: `deploy_action=full deploy_set=[gateway,khachlink,shoperp,directory,crawler] nginx_changed=False`
- nginx force-recreate step **SKIPPED** (nginx untouched) — no unnecessary restart window
- All deploys + smoke: SUCCESS

Next runs: cache hit + scoped builds (single-app changes) → expect ≥50% further reduction.
Re-measure p50/p95 after 3-5 P1 runs (method below).

## Second P1 run (2026-09-26, commit 7c28645, CD run 36226473770 — SUCCESS, cache hit)

| Metric | Baseline p50 | Run 1 (no cache) | Run 2 (cache hit) |
|---|---|---|---|
| Total CD workflow | 610s | 418s | **368s** |
| Longest build (gateway) | 346s | 202s | **161s** |

Cache effect: gateway -20% (202→161s). Scoped single-app runs (not yet exercised) are
the remaining big win. Prod smoke scheduled run #1 (36226890919): **4 passed in 26.2s**
— health, auth path, KhachLink render (2573 chars, title OK), instance config drift
check (isActive=true, profile=Directory, navFlags captured).

## Measurement method (re-run after a few P1 deploys)

```bash
# build job durations
for run in <run-ids>; do
  gh api "repos/anlebao/Gemini_Windsurf/actions/runs/$run/jobs" \
    -q '.jobs[] | select(.name | startswith("Build & Push")) |
        [(.started_at|fromdateiso8601), (.completed_at|fromdateiso8601)] | .[1]-.[0]'
done | sort -n | awk '... p50/p95 ...'

# total workflow durations
gh api "repos/anlebao/Gemini_Windsurf/actions/runs/$run" \
  -q '[(.run_started_at|fromdateiso8601), (.updated_at|fromdateiso8601)] | .[1]-.[0]'
```

## Acceptance targets (P1 sign-off)

| Scenario | Before | Target |
|---|---|---|
| Sửa 1 dòng ShopERP | ~30 phút | ≤ 8 phút |
| Sửa 1 dòng KhachLink | ~30 phút | ≤ 8 phút |
| Shared/CoreHub change | ~30 phút | ≤ 20 phút |
| Deploy lỗi → rollback | manual recovery | ≤ 2-3 phút (`scripts/rollback-app.sh`) |

**Adversarial gate (bắt buộc trước khi coi P1 đạt):**
Cố tình tạo thay đổi Shared/CoreHub nhưng ép scoped deploy sai → classifier PHẢI
chặn (đã có trong test matrix: `scripts/tests/impact-analysis.tests.ps1` D1/D2,
66/66 PASS). Nếu test này fail → không được đưa optimization vào production.

## Notes

- P1 đã triển khai: scoped build/deploy (impact analysis), GHCR layer cache
  (xóa no-cache), immutable SHA tags, conditional nginx recreate, ci-quick.ps1.
- Kỳ vọng: build 1 image có cache ~1.5-3 phút; deploy 1 app ~2-4 phút.
  Đo lại sau 3-5 CD runs để xác nhận p50/p95 thật (không đoán, không ghi số ảo).
