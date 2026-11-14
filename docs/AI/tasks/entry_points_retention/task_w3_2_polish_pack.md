# Task W3-2 — POLISH PACK (FR-V3–V5, FR-P3, FR-L3/L5/L7–L9, FR-U6, FR-D5)

> **Status:** PENDING · **Wave:** W3 · **Gate 4:** CÓ cho phần layout
> **Phụ thuộc:** W1–W2 xong (gom đuôi).

## Mục tiêu giá trị

Dọn consistency + SEO + icon/meta chuẩn — các điểm nhỏ nhưng ảnh hưởng trải nghiệm chuyên nghiệp và khả năng tìm thấy.

## Scope

| FR | Việc |
|---|---|
| FR-V3 | `ShowJobs` dead flag: ẩn khỏi admin cho tới khi M1 ship (hoặc bỏ nếu M1 đã xong). |
| FR-V4 | `ProfileGuard RequiredFlag="ShowStores"` cho `/stores` — đồng nhất enforcement. |
| FR-V5 | Admin note gate kép `ShowAllianceWallet` (flag + alliance mode). |
| FR-P3 | Icon/logo/theme instance hoptacxa (upload icon, set theme/nav colors). |
| FR-L3 | Header icon overflow: ≤3 icon + menu ⋮ trên <400px. |
| FR-L5 | Thay emoji 🎤/⏹️ → `bi-mic`/`bi-stop` (`Home.razor` KhachLink). |
| FR-L7 | `<head>` meta/title per-instance — proxy động như manifest.json pattern. |
| FR-L8 | Placeholder ảnh: placehold.co → inline SVG/local asset (offline-safe). |
| FR-L9 | Design tokens: mobile grid + spacing convention trong `UI.Platform` (doc + tokens, không refactor lùi). |
| FR-U6 | SEO Directory: JSON-LD `WebSite` + `LocalBusiness` (store cards), footer category links. |
| FR-D5 | Guides update: hoptacxa vào community-commerce guides + PWA install guide domain mới. |

## Tests

- E2E cập nhật cho FR-V4 + FR-L3/L5.
- guard-check + build + Core.Tests.

## RV checklist

- [ ] `/stores` trên instance ShowStores=false → redirect + toast.
- [ ] Head meta đúng theo domain (view-source từng domain).
- [ ] timlathay view-source có JSON-LD hợp lệ (validator).
- [ ] Icon hoptacxa: manifest + header logo đúng brand.
