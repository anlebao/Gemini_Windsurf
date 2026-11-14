# MASTER PLAN — ENTRY POINTS RETENTION UPGRADE

> Created: 2026-11-14 · SRS: `docs/specs/SRS_EntryPoints_Retention_Upgrade_v1.0.md` (✅ APPROVED)
> Status: PLAN APPROVED — chờ implement theo wave
> Scope: 3 public entry points — `timlathay.com` (Directory SSR), `diemthuong2.khachvip.online` (KhachLink FullCommerce), `commienphi.timlathay.com` (Reseller, giữ nguyên) + portal MỚI `hoptacxa.timlathay.com` (profile `Hoptacxa`, platform multi-tenant)

---

## 1. MỤC TIÊU

1. Giảm bounce/giữ chân trên cả 3 entry point hiện tại.
2. Tạo platform portal `hoptacxa.timlathay.com` — khuôn mẫu cho các hợp tác xã (mỗi HTX = nhóm member tenants), chừa sẵn chỗ cho modules Mua chung + Sàn việc ship sớm.
3. Mobile-first: không bể layout 320–430px.
4. Mỗi wave = giá trị deploy được + RV pass (không merge wave dở dang).

## 2. DECISIONS ĐÃ CHỐT (SRS §7.1)

| # | Quyết định |
|---|---|
| D1 | Domain: `hoptacxa.timlathay.com` (không phải hoptacxaa) |
| D2 | Flagship toggles (AIChat/VoiceCall/VoiceNote) bật **theo member tenant HTX** — portal không có `OwnerTenantId` |
| D3 | Profile mới `KhachLinkProfile.Hoptacxa` |
| D4 | `commienphi.timlathay.com` GIỮ NGUYÊN song song — không redirect/migrate |
| D5 | `Home_*` toggles giữ global |
| D6 | Platform assistant (`PlatformTenantId` scope) — đầu tư riêng, ngoài scope |

## 3. MÔ HÌNH HOPTXA (reframed)

```
hoptacxa.timlathay.com  = PORTAL platform (KHÔNG OwnerTenantId)
  ├── HTX A → member tenants {shop A1, shop A2, dịch vụ A3}
  ├── HTX B → ...
  └── commienphi.timlathay.com = storefront đơn (Reseller, OwnerTenantId) — có thể trở thành member
```

**Gap chính:** khái niệm "HTX = nhóm member tenants" chưa tồn tại → FR-G1 (Cooperative grouping) là prerequisite cho portal data.

## 4. WAVE MAP — mỗi wave deploy + RV được

| Wave | Giá trị thấy được | Cards | SRS refs |
|---|---|---|---|
| **W1 — "hoptacxa live + mobile không bể"** | Domain mới truy cập được với nav riêng; bottom nav ≤5 tab, không che nội dung; product grid 2 cột; splash skeleton | W1-1 (provision), W1-2 (mobile pack) | FR-D1/D2, FR-P1/P2, FR-L1/L2/L4/L6/L10 |
| **W2 — "portal có nội dung + retention"** | Portal landing có member shops + module cards; timlathay below-fold không rỗng; new user có hero + CTA; AI/voice entry có context | W2-1 (Cooperative + endpoints), W2-2 (portal landing), W2-3 (timlathay retention), W2-4 (KhachLink retention), W2-5 (FeatureContext + visibility) | FR-G1/G2, FR-D4, FR-U1–U5, FR-V1/V2/V6/V7 |
| **W3 — "contextual features + polish"** | AI/voice entry per member-card; icon/meta/SEO chuẩn | W3-1 (contextual surfacing), W3-2 (polish pack) | FR-V3–V5/V8–V10, FR-P3, FR-L3/L5/L7–L9, FR-U6, FR-D5 |
| **M — module waves (song song, rất sớm)** | Sàn việc live; Mua chung | M1 (`/jobs` + ShowJobs), M2 (spec trước) | SRS §7.2.1 |

### Dependencies

```
W1-1 (instance + profile) ──► W2-2 (portal landing) ──► cần W2-1 data cho member list
W1-2 (mobile pack) ── độc lập, song song W1-1
W2-1 (Cooperative model) ──► W2-2 member listing, FR-V2 member toggles
W2-5 (FeatureContextService) ──► W3-1 (per-card AI/voice)
M1 (jobs) ── độc lập; preset Hoptacxa đã bật ShowJobs sẵn ở W1-1
```

## 5. HARD STOPS / GOVERNANCE

- UI Platform components only — không custom HTML/CSS bypass.
- Gate 4: mọi thay đổi layout → E2E spec tại `6_Testing/e2e-tests/` (mobile viewport 375px bắt buộc trong W1-2 trở đi).
- Gate 5: `KhachLinkProfile` enum + `Cooperative` model = shared aggregate changes → card phải ghi rõ domain impact, user duyệt trước khi sửa.
- `Home_*` toggles giữ global — KHÔNG thêm per-instance variant.
- Deploy: multi-VPS CD (`cd-multivps.yml`); cert/DNS là thao tác VPS thủ công (script có sẵn `scripts/init-ssl-timlathay.sh` pattern).
- RV 5-layer sau mỗi deploy: API → static → Playwright → UI flow → manual browser.

## 6. RISKS

| Risk | Mitigation |
|---|---|
| Thêm enum `KhachLinkProfile.Hoptacxa` phá vỡ deserialize cũ (SW cache config cũ) | Enum additive + `Enum.TryParse` fallback FullCommerce đã có; bump cache key nếu cần |
| Cooperative model scope creep (Gate 5) | W2-1 giữ model tối thiểu: Cooperative + member list; không permission/role phức tạp |
| `ShowJobs` bật trong preset Hoptacxa nhưng `/jobs` chưa có → nav link chết | W1-1 preset đặt `ShowJobs=false`; M1 ship page rồi mới đổi preset bật |
| Mobile nav 5-tab cap làm mất entry cho Reseller/FullCommerce | Nhóm ít dùng vào "Thêm" bottom sheet — không xoá route, chỉ nhóm nav |
| DNS/cert manual steps | Checklist rõ trong W1-1; verify bằng curl trước khi coi card done |

## 7. TASK CARDS (docs/AI/tasks/entry_points_retention/)

| Card | Wave | Nội dung |
|---|---|---|
| `task_w1_1_hoptacxa_provision.md` | W1 | Profile enum + preset + labels + instance row + DNS/cert checklist |
| `task_w1_2_mobile_layout_pack.md` | W1 | FR-L1/L2/L4/L6/L10 + E2E mobile 375px |
| `task_w2_1_cooperative_model.md` | W2 | Cooperative grouping + portal endpoints (Gate 5) |
| `task_w2_2_hoptacxa_landing.md` | W2 | Portal landing: hero + module cards + member shops |
| `task_w2_3_timlathay_retention.md` | W2 | Below-fold content + on-domain finder |
| `task_w2_4_khachlink_retention.md` | W2 | New-user hero + empty-state CTA + retention teasers |
| `task_w2_5_feature_context.md` | W2 | FeatureContextService + render rule + visibility matrix + member toggles |
| `task_w3_1_contextual_features.md` | W3 | Public feature exposure + per-card AI/voice entries |
| `task_w3_2_polish_pack.md` | W3 | FR-V3–V5, icon/meta/SEO polish, guides |
| `task_m1_san_viec_jobs.md` | M | `/jobs` page + ShowJobs activation (spec stub — viết chi tiết khi bắt đầu) |
| `task_m2_mua_chung.md` | M | Group-buy — spec riêng, chỉ placeholder |
