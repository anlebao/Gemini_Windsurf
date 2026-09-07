# VANAN ECOSYSTEM — MASTER BUSINESS MODEL v1.0

> **Created:** 2026-09-06
> **Sources:** `docs/requirements/hình dung Vạn An - BOM.md` (tầm nhìn 5 tầng) · `docs/requirements/mô hình kinh doanh (khoan thủng thị trường).md` (GTM) · 2 session review BOM + GTM (2026-09-06) · codebase verification
> **Quy ước số liệu:** **[V]** = đã verify trong code/production · **[A]** = assumption — phải validate bằng thị trường, mọi con số giá là ĐỀ XUẤT chờ chốt
> **Tính chất:** tài liệu chiến lược kinh doanh — KHÔNG phải implementation plan. Task code sẽ tạo riêng tại `docs/AI/tasks/`.
> **Cách dùng:** re-đọc Section 0 (Ground Rules) + Section 8 (BOM Registry) đầu mỗi sprint planning; Section 6 (Roadmap) là nguồn truth cho thứ tự ưu tiên.

---

## 0. GROUND RULES (điều hành mọi quyết định)

1. **North Star = Active Local GMV** — tổng giá trị giao dịch phát sinh QUA mạng lưới Vạn An trong khu vực/thời gian. KHÔNG dùng tổng doanh thu merchant làm GMV (lỗi phép nhân trong tài liệu gốc đã sửa tại Section 7).
2. **KPI cascades:** Active Merchants → Active Customers → Orders → GMV → Repeat Rate → Merchant Retention → Contribution Margin.
3. **Bootstrapped reality:** Vạn An không được rót vốn — dòng tiền sớm từ những layer KHÔNG bóp chết network (thu merchant cho tool/dịch vụ; không thu khách; không thu per-order nặng giết đơn) là nhiên liệu để tới được network effect, không phải mục tiêu cuối.
4. **Danh sách KHÔNG:** không ôm hàng tồn kho · không thu phụ phí ship của khách (CTV nhận phí ship trực tiếp; Vạn An thu 0 ở L5 — resolve mâu thuẫn nội tại tài liệu gốc) · không đốt tiền đối đầu trực diện Grab/ShopeeFood · không bán data cá nhân (ND356/Luật 91).
5. **Số liệu trung thực:** chỉ hiển thị số ĐO ĐƯỢC cho merchant (lượt tìm/xem/đơn thật). Mọi mô hình hóa phải dán nhãn "ước tính". Cấm fake precision — một report sai kiểu "420 lượt tìm quanh đây" đủ giết niềm tin cộng đồng địa phương, ngược chiều flywheel.
6. **Business-driven gate:** mọi feature mới phải trả lời tăng gì trong {Merchant acquisition · Transaction · Retention · Revenue}. Không trả lời được → defer. Feature tồn tại từ trước (Loyalty Alliance, Guard QR, OCR Hub) được grandfather — review metric hằng quý, 2 quý không có metric → cắt.

---

## 1. VALUE CHAIN — 5 TẦNG × 10 LAYER DOANH THU

### 1.1 Chuỗi giá trị theo 5 tầng (theo tài liệu gốc, giữ nguyên)

```
Tầng 1 TOOL          Tầng 2 DEMAND       Tầng 3 TRANSACTION    Tầng 4 FINTEL         Tầng 5 FININFRA
ShopERP+Accounting   TimLaThay           Order+Delivery+Wallet FI premium (MVP-2)    Credit data →
= mỏ neo hàng ngày   = local discovery   = network effect      = moat dữ liệu        referral tài chính
                     "tìm là thấy"       GMV = North Star
```

Entry point bán hàng luôn là câu đơn giản nhất: *"Tôi giúp anh/chị bán hàng, xuất hóa đơn và biết hôm nay lời hay lỗ."* Mỗi bước bán bước tiếp theo (tool → discovery → order → delivery → finintel).

### 1.2 Trạng thái 10 layer doanh thu

| Layer | Sản phẩm | Code [V] | Monetize |
|---|---|---|---|
| L1 SaaS | ShopERP POS/kho/CRM | live | **NOW** — giá gần vốn (BOM #1) |
| L2 Accounting/HĐĐT | kế toán TT 152 + e-invoice | live | **NOW** — gói HĐĐT prepaid (BOM #2) |
| L3 TimLaThay commerce | Directory SSR + storefront | live | **NOW** Verified/slot promo (BOM #4) + commerce margin SAU |
| L4 Order transaction | Gateway + Wallet settlement | live · R2.2 3-bookset DONE [V] (PR #169) | **NOW** — chỉ cần bật PlatformFeeRate (BOM #5) |
| L5 Delivery | CTV shipper network | community roles live | **NEVER thu khách** · margin từ shop = optional Year 3 |
| L6 Marketing | Featured/promo | FeaturedProduct live, chưa kiếm tiền | **NOW** — slot dán nhãn (BOM #4) |
| L7 Payment | VNPay/Momo | deferred v3.0 [V] | SAU khi GMV chứng minh (Gate G2) |
| L8 AI Business | Financial Intelligence | MVP-2 merged PR #152 [V] | **NOW** — premium tier (BOM #3) |
| L9 Financial | credit scoring / insurance referral | chưa có | Year 3 |
| L10 Data B2B | aggregate demand/supply data | chưa có | Year 3+ · rào pháp lý ND356 |

**Bối cảnh vĩ mô [V]:** Quyết định 1568/QĐ-BCT (Kế hoạch TMĐT quốc gia 2026-2030): 60% DNNVV kinh doanh trên nền tảng TMĐT · 70% doanh nghiệp ứng dụng TMĐT · **100% giao dịch có HĐĐT** · 80% thanh toán không tiền mặt. Cộng thêm TT 152/2025/TT-BTC áp dụng kế toán HKD từ 2026 → nhu cầu ở L1/L2 là cưỡng bức theo luật, không phải nhu cầu phải "tạo ra".

---

## 2. 4 NHÓM ACTOR

| Actor | Nhận giá trị | Trả / đóng góp | Dòng tiền tới Vạn An |
|---|---|---|---|
| **Merchant (HKD/SME)** | POS+kế toán đúng luật · được tìm thấy bởi khách gần đó · thấy rõ lời/lỗ · đơn online tự chảy vào | subscription [A] · gói HĐĐT · slot promo · take-rate trên đơn online | L1+L2+L3+L6+L8 hiện tại, L4 sau R2.2 |
| **Customer (cư dân)** | tìm là thấy · chat/call · nhận hàng nhanh · tích điểm liên minh | KHÔNG trả phí nền · (thu phí ship trả trực tiếp CTV) | 0 trực tiếp — giá trị = demand signal + data ẩn danh |
| **CTV (Salesman/Shipper)** | thu nhập từ đơn/commission · không phải "đi bán" | giới thiệu merchant (QR referral) · giao hàng | chi phí biến đổi chỉ khi có giao dịch (Wallet commission coded [V]) |
| **Vạn An (platform)** | hạ tầng + network + data | vận hành, dev, hỗ trợ remote | tổng 10 layer theo Section 7 |

**Kênh phân phối đặc biệt — Kế toán viên dịch vụ (KTV):** mỗi KTV đang phục vụ 20-100 HKD [A] = kênh 1-nhiều duy nhất với CAC ≈ 0. Commission cho KTV phải là **recurring rev-share trên subscription** (không phải per-transaction như CTV commerce) — 2 schema hoa hồng tách bạch theo product line.

---

## 3. FLYWHEEL + 2 ĐỘNG CƠ ZERO-CAC

### 3.1 Flywheel (giữ nguyên tài liệu gốc)

```
NHIỀU SHOP → NHIỀU SẢN PHẨM → TIMLATHAY HẤP DẪN → NHIỀU KHÁCH → NHIỀU ĐƠN
→ NHIỀU GIAO DỊCH → NHIỀU DỮ LIỆU → AI TỐT HƠN → SHOP KINH DOANH TỐT HƠN
→ SHOP GIỮ VẠN AN → THÊM NHIỀU SHOP ↺
```

Tài sản thật = network + transaction data + merchant relationship + customer relationship + local supply/demand graph. Không phải source code/VPS.

### 3.2 Hai động cơ zero-CAC đã tồn tại trong code [V]

1. **Crawler (supply-side):** crawl doanhnghiep.vn (API hợp pháp, 17 fields) + trangvang (batch nhỏ) → hàng nghìn Pending profile → chủ shop tự tìm thấy tên mình (SEO) → Claim (GPKD) → Active storefront. Đã deploy, RV pass, 8/8 phase. Chi phí biên ≈ 0.
2. **KTV channel (anchor-side):** xem Section 2.

Không có 2 động cơ này, flywheel không tự khởi động — đây là phần tài liệu gốc bỏ ngỏ (đặt mục tiêu Year 1 = 1.000 SME nhưng không nêu cơ chế).

### 3.3 Chicken-and-egg delivery (đã giải bằng staging)

"Giao trong 5-10 phút" chỉ khả thi khi mật độ CTV đủ dày = SAU khi network scale. Giai đoạn 0: positioning = **"tìm là thấy + chat/call"**, delivery best-effort khi có CTV gần. Không hứa SLA giao hàng cho tới khi đo được (quy tắc số liệu trung thực).

---

## 4. GTM — MÁY KHOAN THỦNG (bản đã vá 3 hố)

Nguyên lý giữ nguyên từ tài liệu GTM: **merchant tự phát hiện → tự trải nghiệm → tự thấy lợi ích → tự đăng ký → hệ thống qualify → remote chốt deal giá trị cao. Sales chỉ xuất hiện sau khi có intent.** Bán kết quả, không bán phần mềm.

### 4.1 Ba hố của bản gốc đã vá

| Hố | Bản gốc | Bản vá |
|---|---|---|
| Fake precision trong Merchant Analyzer | "420 người có hành vi tìm kiếm quanh 2km", "+10-20 đơn/ngày" | Chỉ hiển thị số đo được (search/view/order thật trên TimLaThay) + nhãn "ước tính" cho mô hình. Trước khi có traffic: audit chỉ từ dữ liệu crawl công khai. KHÔNG tích Google Places/FB Graph pha 1 (phí + ToS + không scrape page người khác) |
| Cold-start demand | Giả định có traffic khách tìm | Traffic khách chính = **SEO từ chính các trang listing crawl** (free, hàng nghìn trang ngành×tỉnh) — kênh này tài liệu gốc không nhắc. Ads chỉ test nhỏ SAU khi funnel có chuyển hóa tự nhiên. Metric trung gian trung thực: searches → views → chat (chưa hứa GMV) |
| Scoring không có dữ liệu | Bảng điểm 9 tín hiệu | Codebase hiện **0 event tracking** [V]. Scoring tự động defer (Gate G1). Giai đoạn 0: scoring thủ công bằng ClaimsQueue + admin dashboard đã có |

### 4.2 Build map (đã cắt từ 7 mảnh còn 5, tổng ~5 tuần)

| Mảnh GTM | Đã có [V] | Phải xây | Ước lượng |
|---|---|---|---|
| Merchant Audit (từ crawl) | Crawler + CrawlSources + TenantSearch 4 cấp | Landing + report generator từ dữ liệu crawl | ~1-1,5 tuần |
| Interactive Demo (preview storefront) | Pending→Claim→storefront + ImageUploadService | Preview mode: nhập tên quán → storefront mock tự sửa logo/menu/giá → nút "Đưa lên TimLaThay" | ~1 tuần |
| Free onboarding | Claim→Verify→Active+user (đủ) | — | 0 |
| Revenue Proof dashboard | Order data PG đầy đủ | counter view/chat/search per store + trang "Tháng này" per tenant | ~1 tuần |
| Merchant referral (QR → claim) | Wallet commission (COD split) + Order.ReferralCode [V] (salesman-level, chưa có merchant-level) | referral attribution lúc claim + trigger hoa hồng | ~1-1,5 tuần |
| ~~AI SDR + scoring~~ | — | event pipeline trước | **DEFER** (Gate G1) |
| Remote closing | Impersonation #103 [V] | KHÔNG build co-browse — Zalo/Meet screen-share + 1 trang consent | 2-3 ngày |

### 4.3 Remote closer (giữ nguyên tinh thần bản gốc)

Thông điệp: *"Em thấy tài khoản cửa hàng của anh đang ở đây, có 37 sản phẩm và 126 lượt xem 3 ngày qua. Em vào cùng màn hình 10 phút cấu hình luôn nhé?"* → nút [Cho phép Vạn An hỗ trợ từ xa] → người thật xuất hiện. 1 closer duy nhất giai đoạn đầu.

### 4.4 Revenue Proof = pay-after-value (giữ nguyên, bổ sung)

Dashboard "THÁNG NÀY: tìm kiếm X · xem cửa hàng Y · nhắn tin Z · đặt hàng N · GMV" → *"Vạn An đã tạo ra ...đ doanh số cho bạn. Muốn tiếp tục nhận khách?"* → upgrade. Ghi chú: đây đồng thời là **tính năng retention** — nối thẳng vào FI premium (một công đôi việc).

---

## 5. UNIT ECONOMICS

### 5.1 Per anchor-merchant / tháng

| Dòng | Giá trị | Ghi chú |
|---|---|---|
| SaaS anchor kit | 79.000đ [A] | giá gần vốn — mục tiêu DAU merchant, không phải profit |
| HĐĐT margin | ~45.000đ [A] | gói 200 hóa đơn bán ~150k, margin ~30% trên giá provider — PHỤ THUỘC hợp đồng đại lý (ẩn số #1) |
| FI premium attach | ~30.000đ [A] | 99k × attach 30% [A] |
| Promo/domain slot | ~8.000đ [A] | 5% tenants × 150k TB |
| **ARPU** | **~160.000đ/th [A]** | |
| Chi phí biến đổi/merchant | ~10.000đ/th [A] | hỗ trợ, Cloudinary/R2, thanh toán tay |
| **Contribution** | **~150.000đ/th [A]** | |
| Infra cố định | ~3-4 triệu/th [A — verify billing GCP] | 4 VPS + domains + R2 [V số lượng] |

**Điểm hòa vốn hạ tầng ≈ 25-30 anchor** → mốc tháng 3: 40 anchor = hạ tầng tự nuôi.

### 5.2 Per order / giao dịch online qua mạng lưới

| Dòng | Giá trị | Ghi chú |
|---|---|---|
| Giá TB đơn local | 50-100kđ [A] | |
| PlatformFeeRate | 1-2% [A] | field per-tenant ĐÃ CÓ [V] (ShopFeatureSettings, migration 20260809) |
| Thu Vạn An/đơn | 1-2kđ | KHÔNG đặt cao hơn — take-rate giết đơn là tự phá network |
| Wallet settlement | đã code [V] | `ConfirmCodResellerAsync`: PlatformFee + CommunityFund + Commission |

Take-rate chỉ có nghĩa ở volume: 100k đơn/th × 1,5k = 150 triệu/th. Đây là lý do BOM #5 xếp cuối về tốc độ nhưng giữ vì tự động cộng dồn.

### 5.3 Per claimed listing (funnel TimLaThay)

5.000 listing crawl [A] → 3% claim = 150 [A] → 15% trả phí = ~22 × 99-299k = **2-6 triệu/th recurring** + domain attach ~10% claimed × margin 100-300k one-time. Mở rộng bằng crawl thêm ngành×tỉnh (chi phí biên ≈ 0).

---

## 6. ROADMAP 0 → 1.000 → 10.000 SME

### 6.1 Hai tuyến song song — 8 tuần đầu

| Tuần | Tuyến A (tiền mặt) | Tuyến B (cỗ máy khoan thủng) |
|---|---|---|
| 1 | Bảng giá + tiếp cận 5 KTV + gọi provider HĐĐT + dogfood 2-3 HKD (sổ trọn gói 300-500k/th [A]) | — |
| 2-3 | Ký provider (nếu được) · thu chuyển khoản + bật tay qua admin | Build mảnh 1-2 (audit + demo preview) |
| 3-4 | Dogfood mở rộng 3-5 HKD · FI premium kèm mỗi onboarding | Build mảnh 4-5 (counters + referral) |
| 4-6 | 30-50 anchor mục tiêu | Crawl batch lớn theo ngành×tỉnh chọn · landing SEO |
| 6-8 | Upsell Verified/slot/domain cho claimed shops | Bật take-rate: config `PlatformFeeRate` + seed `PlatformAccountingTenantId` (R2.2 đã deploy + RV pass [V]) |

Tuyến A trả lời "ai trả tiền tháng này", Tuyến B trả lời "làm sao scale không cần đội sales". Không thay thế nhau.

### 6.2 Year 1 — "Chiếm shop": 1.000 Daily Active Merchant

Cơ chế: KTV × N (mỗi KTV 20-100 HKD [A]) + crawler funnel + máy khoan thủng. Đo bằng **Daily Active Merchant**, không phải số đăng ký. Mốc nội bộ: 100 (tháng 3) → 300 (tháng 6) → 700-1.000 (tháng 12) [A].

### 6.3 Year 2 — "Chiếm giao dịch địa phương": 5.000-10.000 merchant

TimLaThay bắt đầu có ý nghĩa khi mật độ phường đủ: ~50-80 merchant/phường [A]. Network GMV mục tiêu 5-40 tỷ/th [A]. Bật L7 payment khi vượt Gate G2.

### 6.4 Year 3 — "Chiếm dòng tiền": lớp tài chính

Credit scoring từ dữ liệu cashflow thật của merchant (L9) — referral cho tổ chức tài chính, Vạn An là lớp dữ liệu + intelligence, KHÔNG làm ngân hàng.

### 6.5 Phase Gates (điều kiện mở khóa việc lớn)

- **G1 — AI SDR/scoring tự động:** chỉ build sau khi event pipeline có ≥ 30 ngày dữ liệu thật (hiện = 0 [V]).
- **G2 — Payment gateway (L7):** chỉ build khi network GMV > 500 triệu/th [A threshold].
- **G3 — Ngành có điều kiện pháp lý** (thuốc, thực phẩm chức năng...): chặn tới khi review pháp lý xong.
- **G4 — Scale VPS/tier:** chỉ khi CPU > 70% / MEM > 80% (trùng rule Hybrid Strategy Bước 2 [V]).
- **G5 — Tuyển sales:** KHÔNG tuyển cho tới khi funnel tự phục vụ > 500 merchant/tháng mà closer quá tải — khi đó tuyển closer thứ 2, không phải đội đi cửa.

---

## 7. MÔ HÌNH TÀI CHÍNH 5 NĂM — SANITY CHECK

### 7.1 Sửa lỗi phép nhân của tài liệu gốc

Tài liệu gốc: 300 đơn/ngày × 50k × 10.000 shop = 54.000 tỷ GMV/năm. **Sai hai lần:** (a) đó là tổng doanh thu merchant, không phải GMV qua mạng lưới (North Star tự định nghĩa); (b) 300 đơn/ngày là mức quán đông, không phải HKD trung bình. Mọi mô hình dưới đây dùng **network GMV** (chỉ đơn được route qua Vạn An).

### 7.2 Công thức doanh thu

```
Revenue/tháng = (M × ARPU_anchor)                    // L1+L2+L6+L8
              + (GMV_network × PlatformFeeRate)       // L4
Trong đó M = số anchor active, ARPU ≈ 160k [A], PlatformFeeRate ≈ 1,5% [A]
```

### 7.3 Ba kịch bản [A — mọi con số là assumption để validate bằng Tuyến A tháng 1-3]

| Mốc | Thận trọng | Cơ sở | Tích cực |
|---|---|---|---|
| Y1 anchor | 300 | 700 | 1.200 |
| Y1 GMV_network | ~0 | 1 tỷ/th (cuối năm) | 3 tỷ/th |
| Y2 anchor | 2.000 | 5.000 | 10.000 |
| Y2 GMV_network | 3 tỷ/th | 15 tỷ/th | 40 tỷ/th |
| Y5 anchor | 5.000 | 10.000+ | 20.000 |
| Y5 GMV_network | 10 tỷ/th | 25 tỷ/th | 60 tỷ/th |
| Y5 revenue/tháng | ~0,9 tỷ | ~2,2 tỷ | ~4,5 tỷ |
| Y5 profit/năm (margin 25-35% [A]) | ~3 tỷ | ~7-9 tỷ | ~14-19 tỷ |

Tính Y5 cơ sở: SaaS 10.000×79k=790tr + HĐĐT 225tr + FI 300tr + promo 75tr + take-rate 25 tỷ×1,5%=375tr ≈ 1,77 tỷ/th ≈ 21 tỷ/năm.

### 7.4 Phép kiểm mục tiêu "100 tỷ tài sản + 1 triệu cư dân"

- **Như TIỀN MẶT tích lũy: KHÔNG khả thi trong 5 năm** — kịch bản tích cực cũng chỉ tích lũy vài chục tỷ profit.
- **Như VALUATION (equity story): khả thi có điều kiện** — 100 tỷ ≈ 5x revenue năm 5 (kịch bản cơ sở) — mức multiple bình thường cho platform có tăng trưởng. Điều kiện bắt buộc: (1) 10.000 Daily Active Merchant thật, (2) network GMV ≥ 20-25 tỷ/th với take-rate sống, (3) data moat tài chính-địa phương không đối thủ nào có, (4) merchant retention ≥ 90%/năm [A].
- **1 triệu cư dân:** ~50-80 phường × 10-20k dân/phường [A] → cần mật độ 50-80 merchant/phường trong vùng phủ — nhất quán với mục tiêu Year 2 (5.000-10.000 merchant). Không phải con số marketing — là hàm của mật độ merchant.

**Kết luận phần 7:** kiến trúc công nghệ hiện tại phục vụ đúng business model ở Tầng 1-4; phần nên CUT/từ chối building thêm: L5 monetize, L7 sớm, AI SDR sớm (Gates). Thứ tự đúng = Roadmap 6.1.

---

## 8. BOM REGISTRY (chốt 2026-09-06)

Tiêu chí: nhanh có doanh thu × ít đầu tư × không bóp network.

| Hạng | BOM | Layer | Đầu tư | Doanh thu bắt đầu | Trạng thái code [V] |
|---|---|---|---|---|---|
| 1 | **Anchor Kit siêu rẻ qua KTV** (+biến thể dogfood: tự serve 3-5 HKD sổ trọn gói 300-500k/th [A]) | L1+L2 | ~0 | tuần 1-2 | live |
| 2 | **HĐĐT gói prepaid** (margin provider — ẩn số #1: hợp đồng đại lý) | L2 | 0 code | tuần 2-4 | module live |
| 3 | **FI premium** ("biết lời/lỗ + cảnh báo dòng tiền") | L8 | ~0 | theo từng anchor | MVP-2 merged |
| 4 | **TimLaThay claim funnel + Verified 99k + slot Quảng cáo dán nhãn 199-299k + domain margin GoDaddy** | L3+L6 | ~5 ngày code | tuần 4-8 | crawler live · FeaturedProduct live chưa kiếm tiền · GoDaddy live |
| 5 | **Transaction take-rate** (PlatformFee qua Wallet) | L4 | ~0 — R2.2 DONE [V] (PR #169), còn config `PlatformFeeRate` + seed `PlatformAccountingTenantId` | khi GMV > 0 | 3 tenant booksets + margin split coded [V] |

**Đã loại:** Loyalty monetization (trái pivot demand-first) · bán data crawl (ND356) · payment margin sớm (Gate G2) · dịch vụ sổ standalone (tốn người — chỉ giữ làm dogfood variant của #1) · dropship ôm hàng (danh sách KHÔNG).

**2 ẩn số thế giới thực (hóa giải tuần 1, bằng hành động không bằng code):**
1. Provider HĐĐT có cho margin đại lý? → gọi 2-3 nhà. Nếu không: bán gói giá vốn, vẫn giữ khách cho #1/#3.
2. KTV/HKD có thật sự trả tiền? → dogfood 2-3 HKD tuần 1 = câu trả lời willingness-to-pay thật trước khi viết bất kỳ dòng marketing code nào.

---

## 9. RỦI RO & THEO DÕI

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Không tìm được provider HĐĐT có margin | TB | BOM #2 tụt về giá vốn; #1/#3 không phụ thuộc |
| WTP thấp hơn dự kiến | Cao | dogfood tuần 1; giá là đề xuất [A], chỉnh theo dữ liệu thật |
| Fake precision trong audit giết niềm tin | Cao | Ground Rule 5 — chỉ số đo được |
| Thin-content SEO bị Google phạt | TB | mỗi landing ngành×tỉnh ≥ 10 listing thật; không tạo trang rỗng |
| Claim giả (GPKD fake) | TB | MST trên GPKD phải khớp TaxCode (đã có approve thủ công SysAdmin [V]) |
| Đối thủ copy rồi补贴 | TB | không đối đầu giá — moat là dữ liệu tài chính + density địa phương (Section 10 cạnh tranh) |
| Over-build (cái chết §13 tài liệu gốc) | Cao | Ground Rule 6 + Gates G1-G5 + kill-list hằng quý |

**Chiến lược cạnh tranh (không đối đầu trực diện):** Grab = delivery → Vạn An = merchant OS + local network · Shopee/TikTok = marketplace quốc gia → Vạn An = hyperlocal + dữ liệu thuộc merchant network · Google Maps = discovery → Vạn An = transactional discovery (tìm → chat → đơn → kế toán) · MISA/KiotViet = accounting/POS → Vạn An = accounting + vận hành + commerce + nhu cầu địa phương trong một dòng dữ liệu. Không ai trong 4 nhóm có được chuỗi Sales+Orders+Inventory+Accounting+Cashflow+Local demand gộp lại của Tầng 4.

---

## 10. SOURCES & VERIFIED FACTS (2026-09-06)

**[V] Từ codebase/production:**
- Production hiện 3 tenant test (GCP Data Seeding pending) — mọi BOM bị chặn bởi acquisition, hence 2 động cơ zero-CAC dẫn đầu.
- Crawler + Claim pipeline deployed (PR #162-164, RV pass) · doanhnghiep.vn API 17 fields, no phone.
- Wallet COD reseller settlement: PlatformFee + CommunityFund + Commission split coded; `PlatformFeeRate` per-tenant field (migration 20260809).
- `Order.ReferralCode` = salesman product referral (Domain.cs:1839) — merchant-referral attribution CHƯA có.
- Event tracking/telemetry: **0 file** (grep ViewCount/PageView/SearchLog/EventTrack/Telemetry) — nền tảng cho scoring chưa tồn tại.
- FeaturedProduct entity + SysAdmin CRUD live, tham gia search, chưa kiếm tiền. Search ranking: status → relevance → distance, KHÔNG có paid ranking (giữ nguyên organic thuần).
- FI MVP-2 merged (PR #152, 61/61 tests). Impersonation admin (#103, RV pass). Domain Reseller GoDaddy R1 live.
- R2.2 Reseller Accounting-Cashflow Alignment COMPLETE + deployed + RV pass (PR #169, `main` @ `2d98ee77`): 3 tenant booksets (Supplier/Reseller/Platform) + Auditor UI `/admin/reseller-accounting-reconciliation`. Follow-up 1 lần: cấu hình `PlatformAccountingTenantId` SystemSetting (SysAdmin).

**[V] Chính sách:** Quyết định 1568/QĐ-BCT (TMĐT quốc gia 2026-2030): 60% DNNVV trên nền tảng TMĐT · 70% DN ứng dụng TMĐT · 100% giao dịch có HĐĐT · 80% thanh toán không tiền mặt. TT 152/2025/TT-BTC (kế toán HKD). ND356/2025/NĐ-CP + Luật 91/2025 (dữ liệu cá nhân — quy tắc ẩn SĐT Pending đã xử lý M3).

**Tài liệu nguồn:** `docs/requirements/hình dung Vạn An - BOM.md` · `docs/requirements/mô hình kinh doanh (khoan thủng thị trường).md` · session reviews 2026-09-06 (5 BOM v1 → BOM 2.0 → GTM review).

**[A] Toàn bộ price points, attach rates, funnel conversions, ARPU, chi phí infra** — xem như hypothesis, validate qua Tuyến A trong 8 tuần đầu.

---

*Maintenance: file này cập nhật khi (a) ẩn số #1/#2 có答案, (b) vượt mốc 100/300/1.000 anchor, (c) quyết định chiến lược mới làm thay đổi Ground Rules hoặc Gates.*
