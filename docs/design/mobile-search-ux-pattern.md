# Mobile Search UX Pattern — "Google-style minimal + smart search"

> **Status:** APPROVED A+B (user 2026-11-15) · **First implementation:** timlathay.com (Directory)
> **Reusable cho:** mọi KhachLink/Directory instance — `hoptacxa.timlathay.com`, `vanan.timlathay.com`,
> `commienphi.timlathay.com`, `diemthuong2.khachvip.online`, và Directory-style apps sau này.

## 1. Vấn đề gốc (verified trên timlathay.com, mobile 375px)

- Hero block to (icon 2.5rem + h3 + subtitle) chiếm ~150px đầu trang.
- Search card chứa ~6 controls có label → rối, mất cân đối.
- `input-group-lg` → font input ~1.25rem, to gấp 1.5× chuẩn mobile.
- Nút "Tìm kiếm" nằm dưới input → **bị bàn phím ảo che** (user không bấm được, dù Enter đã submit).
- Backend search chỉ `ILIKE contains → token → prefix → suggested`: không bỏ-dấu tiếng Việt,
  không fuzzy → gõ "sua hang" / sai chính tả → rơi vào "suggested" (10 tenant gần đúng tên).

## 2. Pattern A — UI tối giản (mobile-first)

| Rule | Chi tiết |
|---|---|
| Hero | Icon ≤1.25rem inline với title nhỏ, hoặc bỏ hero — search card lên đầu trang |
| Input | `input-group` size thường (KHÔNG `-lg`), font 1rem, `enterkeyhint="search"` |
| Nút Tìm kiếm | Đưa VÀO TRONG input-group (icon-only mobile, icon+text desktop) → luôn tầm tay, không bị keyboard che |
| Actions phụ | Mic + "Dùng vị trí" + clear → icon buttons cùng hàng input (title/aria-label giữ text) |
| Tab/filter | Segmented pill nhỏ gọn, không chiếm hàng riêng nếu được |
| Enter | `@onkeyup` Enter → submit (đã có sẵn — giữ) |

## 3. Pattern B — Smart search (typo-tolerant)

Backend search thêm 1 level trước `suggested`:

1. **Normalize tiếng Việt:** strip dấu + lowercase. ⚠️ **KHÔNG dùng `string.Normalize(FormD)`** —
   Gateway prod chạy `System.Globalization.Invariant=true` (runtimeconfig) → FormD là NO-OP,
   dấu không bao giờ bị strip (bug thật 2026-11-15). Dùng fold map thủ công
   (`DiacriticFold` trong `TenantStoreController`) — 67 ký tự TV + Latin-1, ICU-free.
   "sua hang" khớp được "Sửa chữa"/"cửa hàng" sau normalize.
2. **Normalized matching ở MỌI level** (chứa→token→fuzzy), không chỉ fuzzy fallback:
   token-level `ILIKE` accent-sensitive + ANY-token quá lỏng → chặn fuzzy, relevance sai
   ("tra sua" → Central Mall thay vì "Trà Sữa 79"). Load bounded candidates (≤500) 1 lần,
   match in-memory trên normalized text.
3. **`DidYouMean`:** field mới trong `TenantSearchResultDto` — tên match tốt nhất khi
   không có exact/contains → UI hiển thị chip "Ý bạn là: **X**?" 1-tap search lại.

Ngưỡng gợi ý: similarity ≥ ~0.55; `DidYouMean` chỉ set khi match fuzzy (không set khi
contains/exact — tránh noise).

## 4. Áp dụng cho instance khác — checklist

- [ ] **timlathay.com (Directory, Blazor Server)** — DONE 2026-11-15 (`Home.razor` + `TenantStoreController.Search`)
- [ ] **KhachLink WASM instances** (hoptacxa/vanan/commienphi/diemthuong2): search surface là
      `Pages/StoreFinder.razor` + `Pages/Home.razor` (semantic search bar) — port cùng rules;
      voice input `vananVoiceInput` đã có sẵn trên WASM.
- [ ] Mỗi instance: verify Enter-key submit + keyboard không che nút search ở 375px.
- [ ] E2E: layout change → spec mới theo Gate 4.

## 5. Lưu ý kỹ thuật

- Normalize in-memory (C#) — KHÔNG cần PG `unaccent`/`pg_trgm` extension (tránh migration risk).
- **InvariantGlobalization gotcha:** mọi API phụ thuộc ICU normalization/casing mở rộng
  (FormD, CharUnicodeInfo, CompareOptions.IgnoreNonSpace) đều NO-OP trên Gateway prod —
  luôn verify trên prod, không tin local test (local có ICU đầy đủ).
- Fuzzy quét bounded candidates (≤500) — đủ cho quy mô directory hiện tại; nếu tenant > ~2k
  thì chuyển sang `pg_trgm` sau.
- `DidYouMean` là additive field — client cũ bỏ qua an toàn.
- Giữ nguyên match order hiện có (contains→token→prefix) — fuzzy CHỈ chạy khi các level trên fail.
