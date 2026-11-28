# BẢN MÔ TẢ KỸ THUẬT PHẦN MỀM
## HỆ THỐNG PHẦN MỀM QUẢN LÝ VÀ HẠCH TOÁN THƯƠNG MẠI ShopERP
### (ShopERP System)

> **Tài liệu kèm theo Hồ sơ đăng ký quyền tác giả đối với chương trình máy tính**
> — Nộp Cục Bản quyền tác giả, Bộ Văn hóa, Thể thao và Du lịch —
>
> Theo quy định tại **Nghị định 17/2023/NĐ-CP** (Điều 43) và **Thông tư 08/2026/TT-BVHTTDL** (Mẫu số 03).

---

## THÔNG TIN CHUNG

| Mục | Nội dung |
|---|---|
| **Tên chương trình máy tính** | Hệ thống Phần mềm Quản lý và Hạch toán Thương mại ShopERP (ShopERP System) |
| **Tên viết tắt / Tên thương mại** | ShopERP Engine — ecoNexus ShopERP |
| **Ngày hoàn thành** | ............ (ngày) ............ (tháng) 2026 |
| **Công bố tác phẩm** | ☑ Đã công bố — SaaS đa thuê bao (Multi-tenant Cloud), vận hành trên hạ tầng Google Cloud Platform (GCP) đa máy chủ ảo (Multi-VPS) |
| **Loại hình tác phẩm** | Chương trình máy tính (theo khoản 1 Điều 22 Luật Sở hữu trí tuệ 2005, sửa đổi bổ sung 2009, 2019, 2022) |
| **Ngôn ngữ lập trình** | C# (.NET 8 LTS), JavaScript/TypeScript, Razor/Blazor (C#), SQL (PostgreSQL, SQLite), HTML/CSS |
| **Công nghệ chính** | ASP.NET Core, Blazor Server, Blazor WebAssembly (PWA), EF Core 8, PostgreSQL, SQLite (WAL), NATS JetStream, SignalR, YARP, Docker, GitHub Actions |

> **Ghi chú điền mẫu:** Các ô chấm (......) là thông tin pháp lý do chủ sở hữu tự điền trước khi in ấn nộp hồ sơ (xem Hướng dẫn tại file `03_Huong_Dan_Va_Checklist_Nop_Ho_So.md`).

---

# MỤC LỤC

- Chương 1 — Giới thiệu tổng quan và mục đích của phần mềm
- Chương 2 — Môi trường vận hành & Yêu cầu kỹ thuật
- Chương 3 — Kiến trúc hệ thống
- Chương 4 — Mô tả chi tiết các phân hệ chức năng chính
- Chương 5 — Mô hình cơ sở dữ liệu logic
- Chương 6 — Cơ chế bảo mật và cách ly dữ liệu đa thuê bao
- Phụ lục A — Tóm tắt tác phẩm (dùng cho Mẫu số 03 — Tờ khai đăng ký)

---

# CHƯƠNG 1 — GIỚI THIỆU TỔNG QUAN VÀ MỤC ĐÍCH CỦA PHẦN MỀM

## 1.1. Giới thiệu chung

**ShopERP System** là một nền tảng phần mềm quản lý bán hàng và kế toán tích hợp (Integrated Commerce & Accounting Platform) dạng SaaS đa thuê bao, được phát triển bằng ngôn ngữ C# trên nền tảng .NET 8 theo mô hình **Clean Architecture kết hợp Domain-Driven Design (DDD)**, vận hành theo kiến trúc **đám mây lai Hybrid Cloud** với khả năng chạy **đa máy chủ ảo (Multi-VPS)**.

Phần mềm phục vụ nhóm đối tượng chính là **hộ kinh doanh (HKD), doanh nghiệp siêu nhỏ, doanh nghiệp vừa và nhỏ (SME)**, hợp tác xã và các chuỗi cửa hàng bán lẻ, với các nhu cầu cốt lõi:

1. **Bán hàng và quản lý đơn hàng** — từ kênh khách hàng tự phục vụ (PWA trên điện thoại) đến quầy POS tại cửa hàng, theo dõi trạng thái đơn hàng theo thời gian thực.
2. **Hạch toán kế toán** — ghi nhận mọi nghiệp vụ kinh tế phát sinh thành bút toán kép (Nợ/Có), tự động xuất sổ sách kế toán theo đúng **chế độ kế toán Việt Nam** (Thông tư 152/2025/TT-BTC cho HKD, TT 133/2016, TT 99/2025, TT 71/2024, TT 58/2026 cho doanh nghiệp).
3. **Trí tuệ tài chính (Financial Intelligence)** — phân tích hòa vốn, kinh tế đơn vị sản phẩm (Unit Economics), biên an toàn, tổng hợp lợi nhuận phục vụ quyết định quản trị.
4. **Quản trị đa thuê bao** — mỗi cửa hàng (thuê bao) có không gian dữ liệu độc lập tuyệt đối, có thể chạy trên máy chủ riêng (VPS) theo mô hình phân tán.

## 1.2. Mục đích sáng tạo

Phần mềm được sáng tạo nhằm giải quyết bài toán **thiếu công cụ kế toán – bán hàng hợp nhất, chi phí thấp, phù hợp hộ kinh doanh Việt Nam**: các hộ kinh doanh và cửa hàng nhỏ thường không có kiến thức chuyên sâu về kế toán nhưng vẫn có nghĩa vụ lập sổ sách, kê khai thuế theo quy định mới (TT 152/2025/TT-BTC có hiệu lực từ 01/01/2026). ShopERP tự động hóa toàn bộ hành trình: *phát sinh nghiệp vụ bán hàng → hạch toán bút toán → lập sổ kế toán đúng mẫu → phân tích tài chính*, giúp người kinh doanh vừa bán hàng vừa "tự động" có bộ sổ sách hợp lệ.

## 1.3. Đặc điểm sáng tạo nổi bật

- **Lõi kế toán hai lớp (Two-Layer Accounting Core):** lớp sổ phụ bút toán chi tiết (`AccountingEntry`) và lớp sổ cái tổng hợp (`JournalEntry`/`JournalEntryLine`) — mô hình kế toán kép hiện đại, mọi bút toán bất biến (immutable), chỉ có thể sửa sai bằng bút toán đảo (reversal entry) — đảm bảo tính bất biến tuyệt đối của sổ sách (append-only).
- **Công cụ tạo sổ theo chuẩn mực động (Dynamic Book Template Engine):** mô hình template khai báo (S1a, S2a, S2b, S2c, S2d, S2e, S3a) với công thức tính tự động (formula engine) — phần mềm có thể sinh ra các loại sổ kế toán khác nhau chỉ bằng khai báo cấu hình, không cần viết lại code.
- **Khóa chuẩn mực kế toán theo loại hình (Regime Locking):** hệ thống tự động khóa chế độ kế toán hợp lệ cho từng loại hình kinh doanh (HKD → TT 152; HTX → TT 71 khóa cứng; DN siêu nhỏ → TT 58, được phép tự nguyện áp dụng TT 133...), ngăn chặn sai sót pháp lý.
- **Cách ly dữ liệu đa thuê bao ở mọi lớp (Multi-Tenancy by Contract):** mọi thực thể dữ liệu bắt buộc triển khai hợp đồng `IMustHaveTenant`, EF Core áp dụng bộ lọc toàn cục (Global Query Filters) — dữ liệu giữa các cửa hàng không thể lẫn nhau kể cả ở tầng truy vấn dữ liệu.
- **Đồng bộ dữ liệu bất khả thất bại (Outbox Pattern + Event Bus):** mọi sự kiện nghiệp vụ được ghi vào bảng Outbox trong cùng giao dịch với dữ liệu nghiệp vụ (atomic), sau đó được chuyển phát qua hàng đợi tin nhắn NATS JetStream — đảm bảo không mất sự kiện, đồng bộ hóa dữ liệu giữa máy chủ đám mây và máy chủ cửa hàng.
- **Trợ lý nhập liệu kế toán thông minh (Smart Entry):** người dùng nhập nghiệp vụ bằng câu mô tả tiếng Việt tự nhiên ("thu 5 triệu tiền mặt bán cafe"), hệ thống tự phân tích ý định (intent), tách số tiền, đối tượng, phương thức thanh toán, ngày nghiệp vụ và đề xuất bút toán — kết hợp công cụ phân tích quy tắc (rule-based parser) và mô hình ngôn ngữ lớn (LLM) có kiểm soát.

## 1.4. Phạm vi bảo hộ đăng ký

Hồ sơ này đăng ký quyền tác giả cho **chương trình máy tính** (mã nguồn, cấu trúc chương trình, giao diện) của toàn bộ hệ thống ShopERP, bao gồm các phân hệ:

1. Core Accounting Engine (Lõi kế toán)
2. HKD & Regime Accounting Module (Sổ kế toán hộ kinh doanh & quản trị chuẩn mực)
3. Financial Intelligence Layer (Tầng trí tuệ tài chính)
4. KhachLink & Order Management (Bán hàng, giỏ hàng đa thuê bao, quản lý đơn hàng)
5. Multi-Tenancy & Platform Architecture (Nền tảng đa thuê bao, phân quyền, hạ tầng đa VPS)
6. Data Sync & Outbox Engine (Đồng bộ dữ liệu & hàng đợi sự kiện bất khả thất bại)

---

# CHƯƠNG 2 — MÔI TRƯỜNG VẬN HÀNH & YÊU CẦU KỸ THUẬT

## 2.1. Mô hình triển khai

ShopERP vận hành theo mô hình **SaaS đa thuê bao, đám mây lai (Hybrid Cloud), đa máy chủ ảo (Multi-VPS)**:

| Thành phần | Vai trò | Hạ tầng điển hình |
|---|---|---|
| **Gateway (Cổng đám mây)** | Tạo lập đơn hàng, kho dữ liệu trung tâm (PostgreSQL), xác thực, định tuyến API (YARP) | 1 VPS (GCP) |
| **ShopERP App (Máy chủ cửa hàng)** | Quản trị cửa hàng, POS, kế toán, sổ sách — dữ liệu cửa hàng (SQLite WAL) | 1 VPS/cửa hàng hoặc cụm |
| **KhachLink PWA** | Kênh khách hàng tự phục vụ trên điện thoại (Blazor WebAssembly) | 1 VPS (nginx) |
| **Metering Service (Phí nền tảng)** | Tính cước phí nền tảng, ví nạp tiền, hạn mức sử dụng | 1 VPS nội bộ |
| **NATS JetStream** | Hàng đợi tin nhắn sự kiện giữa các máy chủ | Trên Gateway hoặc cụm riêng |

## 2.2. Yêu cầu phần mềm / nền tảng

- **Hệ điều hành máy chủ:** Linux (Ubuntu LTS) trong container Docker.
- **Runtime:** .NET 8 (ASP.NET Core 8, EF Core 8.0.x), Node.js cho tiến trình build PWA.
- **Cơ sở dữ liệu:** PostgreSQL 15+ (dữ liệu đám mây), SQLite 3 (dữ liệu cửa hàng, chế độ WAL), có cơ chế backup và khôi phục.
- **Trình duyệt hỗ trợ:** Chrome, Edge, Firefox, Safari (máy tính); Chrome/Android, Safari/iOS (PWA trên điện thoại).
- **Giao thức:** HTTP/HTTPS (TLS 1.2+), WebSocket (SignalR), NATS (TCP, JetStream), REST/JSON.
- **Công cụ phát triển & kiểm thử:** Visual Studio / .NET SDK, xUnit, Playwright (E2E), GitHub Actions (CI/CD multi-VPS), Docker Compose.

## 2.3. Tính năng vận hành nổi bật

- **Offline-first tại cửa hàng:** dữ liệu cửa hàng nằm ngay trên máy chủ cửa hàng (SQLite), POS vẫn hoạt động khi mất kết nối internet; đồng bộ hóa về đám mây khi có mạng.
- **Cập nhật liên tục (Continuous Delivery):** tự động build 6 image container, triển khai tới 4+ VPS và chạy kiểm thử khói (smoke test) trong pipeline GitHub Actions.
- **Đa ngôn ngữ giao diện:** tiếng Việt (chính), tiếng Anh.
- **Hóa đơn điện tử & mã QR thanh toán VietQR** tích hợp sẵn.

---

# CHƯƠNG 3 — KIẾN TRÚC HỆ THỐNG

## 3.1. Sơ đồ kiến trúc tổng thể

```
┌─────────────────────────────────────────────────────────────────────────┐
│                       KHÁCH HÀNG (Customer)                              │
│      KhachLink PWA (Blazor WASM, điện thoại)   ·   POS tại quầy          │
└──────────────────────────────┬──────────────────────────────────────────┘
                               │ HTTPS / REST
┌──────────────────────────────▼──────────────────────────────────────────┐
│                 GATEWAY — Cổng đám mây (YARP + API)                      │
│   · Xác thực JWT · Định tuyến · Tạo đơn hàng (Order Creator)             │
│   · PostgreSQL: Orders, Accounting, Tenants, ShopInstances, Users,       │
│     FeaturedProducts, Metering                                           │
│   · Cổng xuất bản sự kiện: NATS JetStream (Outbox Publisher)             │
└───────┬───────────────────────────────┬──────────────────────────────────┘
        │ NATS JetStream (routed theo ShopInstanceId)
        │ vanan.cloud.order.created.{shopInstId} · loyalty.changed ...      │
┌───────▼───────────────┐   ┌───────────▼──────────────┐   ┌───────────────┐
│  ShopERP-A (VPS)      │   │  ShopERP-B (VPS)         │   │  ... ShopERP-N│
│  Blazor Server · POS  │   │  Blazor Server · POS     │   │               │
│  Kế toán · Sổ HKD     │   │  Kế toán · Sổ HKD        │   │               │
│  SQLite (WAL) —       │   │  SQLite (WAL)            │   │               │
│  dữ liệu cửa hàng     │   │                          │   │               │
└───────┬───────────────┘   └───────────┬──────────────┘   └───────────────┘
        │ SignalR (trạng thái đơn hàng thời gian thực: bếp/POS)
        └────────────────────────────────────────────────
```

## 3.2. Kiến trúc phần mềm theo lớp (Clean Architecture)

Phần mềm được tổ chức theo mô hình phân lớp, chiều phụ thuộc hướng vào trong (Dependency Inversion):

```
┌──────────────────────────────────────────────────────┐
│  TẦNG API (2_Gateway — Controllers; 5_WebApps — UI)   │  →  chỉ gọi Service
├──────────────────────────────────────────────────────┤
│  TẦNG SERVICE (3_CoreHub — nghiệp vụ, giao dịch)      │  →  chỉ dùng Domain
├──────────────────────────────────────────────────────┤
│  TẦNG DOMAIN (1_Shared — thực thể, quy tắc, VO)       │  →  thuần túy, không
│  — Single Source of Truth, không phụ thuộc EF/DB      │     phụ thuộc hạ tầng
└──────────────────────────────────────────────────────┘
```

**Quy tắc kiến trúc được thực thi nghiêm ngặt (kiểm tra tự động bằng architecture guard trong CI):**
- Tầng Domain **thuần túy**: không được sử dụng EF Core, DbContext, DataAnnotations.
- Mọi thực thể Domain chỉ tồn tại duy nhất tại `1_Shared/Domain`.
- Không có nghiệp vụ trong Controllers; không có truy vấn cơ sở dữ liệu trực tiếp từ giao diện người dùng.

## 3.3. Kiến trúc dữ liệu và luồng sự kiện

**Mô hình Order Creator + Giao nhận không đồng bộ theo định tuyến (Routed Async Delivery):**

1. Khách hàng đặt hàng qua KhachLink PWA → **Gateway tạo lập đơn hàng** và ghi vào PostgreSQL (nguồn sự thật cho Đơn hàng, Kế toán, Thuê bao, Cửa hàng, Người dùng, Sản phẩm nổi bật).
2. Gateway ghi sự kiện vào **Outbox** (cùng giao dịch với đơn hàng) → tiến trình xuất bản (Publisher) gửi tới **NATS JetStream** theo chủ đề định tuyến `vanan.cloud.order.created.{shopInstanceId}`.
3. Máy chủ cửa hàng (ShopERP) **lắng nghe (Subscriber)** sự kiện đúng mã cửa hàng của mình → ghi vào SQLite cửa hàng → hiển thị tại quầy bếp/POS theo thời gian thực qua SignalR.
4. Cập nhật trạng thái đơn hàng từ cửa hàng (bếp xác nhận, hoàn tất...) đồng bộ ngược về Gateway để cập nhật nguồn sự thật.

**Lợi ích:** phân tán tải đa VPS, mỗi cửa hàng có kho dữ liệu riêng, mất kết nối không làm mất đơn hàng (hàng đợi + retry theo cơ chế lùi theo cấp số nhân), dữ liệu nhất quán cuối cùng (eventual consistency) có kiểm soát.

## 3.4. Kiến trúc ứng dụng web

- **ShopERP (Blazor Server):** giao diện quản trị cửa hàng — POS, quản lý đơn hàng, kế toán, sổ sách, báo cáo; kết nối thời gian thực bằng SignalR.
- **KhachLink (Blazor WebAssembly + PWA):** ứng dụng khách hàng chạy trên trình duyệt điện thoại, hoạt động như app di động (cài đặt được, hoạt động offline, thông báo đẩy).
- **Directory (Blazor SSR):** trang chủ và cấu hình thương hiệu cho thuê bao.
- **UI.Platform:** thư viện thành phần giao diện dùng chung (hệ thống thiết kế riêng).

---

# CHƯƠNG 4 — MÔ TẢ CHI TIẾT CÁC PHÂN HỆ CHỨC NĂNG CHÍNH

## 4.1. Phân hệ 1 — Core Accounting Engine (Lõi kế toán)

**Vị trí mã nguồn:** `1_Shared/Domain/JournalEntry.cs`, `1_Shared/Domain.cs` (AccountingEntry), `3_CoreHub/` (Accounting Services).

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Ghi sổ nhật ký chung | Tạo bút toán kép Nợ/Có theo chuẩn mực kế toán Việt Nam; tự động kiểm soát cân đối ΣNợ = ΣCó trước khi ghi sổ |
| 2 | Sổ phụ bút toán chi tiết | `AccountingEntry` — ghi nhận từng nghiệp vụ kinh tế phát sinh kèm thuế GTGT (VAT), đối tượng, phương thức thanh toán |
| 3 | Bất biến sổ sách (Append-only) | Mọi bút toán là bất biến; sai sót chỉ được sửa bằng bút toán đảo (Reversal Entry) — truy vết đầy đủ |
| 4 | Công cụ quy tắc kế toán | `AccountingRule` — chuẩn hóa cách hạch toán theo loại nghiệp vụ (bán hàng, mua hàng, thu, chi, công nợ...) |
| 5 | Dãy số chứng từ | Sinh số chứng từ tự động theo khuôn dạng (mã phiếu, số hóa đơn, số phiếu thu/chi) |
| 6 | Phân bổ thanh toán | Phân bổ khoản thu/chi vào công nợ theo nguyên tắc FIFO, truy ngược lịch sử thanh toán |
| 7 | Sổ cái & báo cáo | Sổ nhật ký chung, Sổ Cái, Bảng cân đối số phát sinh, Báo cáo thu chi, báo cáo công nợ (phải thu/phải trả) |
| 8 | Nhập liệu thông minh (Smart Entry) | Nhập nghiệp vụ bằng câu mô tả tiếng Việt; phân tích ý định, số tiền, ngày, đối tượng; đề xuất bút toán (kết hợp quy tắc + LLM có kiểm soát) |
| 9 | Khóa sổ kỳ (Period Closing) | Kiểm tra tính hợp lệ của kỳ kế toán trước khi khóa sổ; cảnh báo và yêu cầu nhập lý do khi sửa chữa |
| 10 | Nhập liệu từ Excel | Import nghiệp vụ từ bảng tính Excel, số dư đầu kỳ, đối soát |

**Dịch vụ cốt lõi:** `JournalEntryService`, `AccountingEntryService`, `GeneralLedgerService`, `AccountingRuleEngine`, `PaymentAllocationService` (FIFO công nợ), `VoucherSequenceService`, `VoucherTextParseService` (Smart Entry), `LlmEnhanceVoucherTextParseService` (LLM Hybrid), `PeriodClosingService`, `CongNoService` (phải thu/phải trả).

## 4.2. Phân hệ 2 — HKD & Regime Accounting Module (Sổ kế toán hộ kinh doanh & quản trị chuẩn mực)

**Vị trí mã nguồn:** `1_Shared/Domain/HKDTemplates.cs`, `1_Shared/Domain/GenericHKDBook.cs`, `1_Shared/Domain/AccountingStandardRules.cs`.

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Tạo sổ kế toán HKD theo TT 152/2025/TT-BTC | Tự động sinh bộ sổ S1a, S2a, S2b, S2c, S2d, S2e, S3a đúng mẫu quy định tại Thông tư 152/2025/TT-BTC (hiệu lực 01/01/2026) |
| 2 | Tính thuế theo nhóm ngành | S2a — phân nhóm 4 ngành (phân phối, sản xuất/vận tải, dịch vụ, hoạt động khác) với tỷ lệ thuế GTGT/TNCN tương ứng (1%–0,5%; 3%–1,5%; 5%–2%; 2%–1%) |
| 3 | Công cụ mẫu sổ động | Khai báo template (record + công thức tính) — thêm sổ mới không cần viết lại chương trình |
| 4 | Khóa chuẩn mực theo loại hình (Regime Locking) | HKD → TT 152; DN siêu nhỏ → TT 58 (tự nguyện TT 133); DN nhỏ/vừa → TT 133; DN lớn → TT 99; HTX → TT 71 (khóa cứng); chặn áp dụng sai chế độ |
| 5 | Xuất sổ & báo cáo thuế | Xuất các sổ HKD, bảng kê doanh thu/chi phí, tổng hợp lợi nhuận phục vụ kê khai |
| 6 | Hỗ trợ nhiều chuẩn mực | TT 133/2016 (DN nhỏ), TT 99/2025 (DN lớn), TT 71/2024 (HTX), TT 58/2026 (DN siêu nhỏ) |

**Ví dụ khai báo template (trích từ mã nguồn):** Mẫu S2a_HKD khai báo 12 trường tính toán — doanh thu phân theo 4 nhóm ngành, thuế GTGT và thuế TNCN tính bằng công thức `DoanhThu × tỷ lệ`, tổng hợp lợi nhuận.

**Dịch vụ cốt lõi:** `HKDBookGenerationService`, `TemplateCalculationEngine` (công thức SUM_ACCOUNT, SUM_ACCOUNT_BY_INDUSTRY), `AccountingStandardService` (đăng ký/lựa chọn chuẩn mực).

## 4.3. Phân hệ 3 — Financial Intelligence Layer (Tầng trí tuệ tài chính)

**Vị trí mã nguồn:** `1_Shared/Domain/FinancialIntelligenceRecords.cs`, `1_Shared/Domain/BusinessProfile.cs`.

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Phân tích hòa vốn (Break-even) | Điểm hòa vốn theo doanh thu và theo sản lượng: Doanh thu hòa vốn = Chi phí cố định / Tỷ lệ lãi trên biến phí |
| 2 | Hòa vốn đa sản phẩm | Trọng số đóng góp bình quân theo cơ cấu doanh thu từng sản phẩm |
| 3 | Kinh tế đơn vị (Unit Economics) | Lãi góp từng sản phẩm: Giá bán − Biến phí; xếp hạng sản phẩm theo tỷ suất lãi góp |
| 4 | Biên an toàn (Margin of Safety) | Mức doanh thu dưới điểm hòa vốn còn an toàn; trạng thái trên/dưới điểm hòa vốn |
| 5 | Tổng hợp lợi nhuận (Profit Summary) | Bảng tổng hợp: Doanh thu, Giá vốn, Lãi gộp, Chi phí vận hành, Lợi nhuận ròng, tỷ suất |
| 6 | Hồ sơ kinh doanh (Business Profile) | Khai báo chi phí cố định tháng, năng lực sản xuất, mô hình định giá; mọi tính toán có phiên bản (FinancialModelVersion) — đảm bảo truy vết |

**Đặc điểm:** toàn bộ kết quả phân tích là **thuần tính toán, không lưu trữ** (recompute on demand), đảm bảo tin cậy cấp độ 1 (deterministic), mọi tính toán được gắn phiên bản mô hình tài chính.

**Dịch vụ cốt lõi:** `BreakEvenAnalysisService`, `UnitEconomicsService`, `ProfitSummaryService`, `BusinessProfileService`.

## 4.4. Phân hệ 4 — KhachLink & Order Management (Bán hàng, giỏ hàng đa thuê bao, quản lý đơn hàng)

**Vị trí mã nguồn:** `1_Shared/Domain/CartItem.cs`, `FeaturedProduct.cs`, `Domain.cs` (Order, OrderItem, OrderFlowProfile...), `5_WebApps/KhachLink`, `5_WebApps/ShopERP` (POS).

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Giỏ hàng đa thuê bao | `CartItem` — giỏ hàng có thể chứa sản phẩm của nhiều cửa hàng (TenantId), tách đơn theo cửa hàng khi thanh toán |
| 2 | Kênh khách hàng KhachLink PWA | Ứng dụng PWA trên điện thoại: xem sản phẩm, đặt hàng, theo dõi trạng thái, thanh toán VietQR, chương trình khách hàng thân thiết |
| 3 | Quầy POS | Nhập đơn nhanh, thanh toán tiền mặt/VietQR, in hóa đơn, tích hợp bếp (kitchen display) |
| 4 | Sản phẩm nổi bật (Featured Products) | Trang chủ KhachLink gợi ý sản phẩm do quản trị viên tuyển chọn (PG-only), gồm sản phẩm trả phí, miễn phí, từ thiện |
| 5 | Luồng đơn hàng động (Order Workflow) | Mỗi thuê bao/ngành hàng cấu hình các bước xử lý đơn (chờ xác nhận, chế biến, giao, hoàn tất, hủy...) kèm lịch sử trạng thái |
| 6 | Đặt lịch hẹn (Booking) | Đặt lịch dịch vụ theo nhân viên/khung giờ, đặt cọc, nhắc lịch, hủy theo chính sách |
| 7 | Theo dõi thời gian thực | SignalR — trạng thái đơn hàng hiển thị tức thời tại quầy, bếp và khách hàng |
| 8 | Tích hợp hóa đơn điện tử | Khởi tạo hóa đơn điện tử từ đơn hàng (InvoiceStatus, InvoiceType, điều chỉnh hóa đơn) |
| 9 | Chương trình khách hàng thân thiết | Tích điểm, đổi quà, liên minh đối tác (Alliance), ưu đãi — theo dõi phát hành điểm |

**Dịch vụ cốt lõi:** `OrderService`, `OrderWorkflowService` (luồng động), `CartService`, `FeaturedProductService`, `BookingService`, `LoyaltyService`, `InvoiceService`, `CatalogService`.

## 4.5. Phân hệ 5 — Multi-Tenancy & Platform Architecture (Nền tảng đa thuê bao)

**Vị trí mã nguồn:** `1_Shared/Domain/Common.cs`, `Domain.cs` (Tenant, ShopInstance, UserTenant), `2_Gateway`, `5_WebApps/ShopERP` (Admin).

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Hợp đồng đa thuê bao `IMustHaveTenant` | Mọi thực thể dữ liệu bắt buộc gắn `TenantId`; EF Core áp dụng bộ lọc toàn cục tự động — cách ly dữ liệu không thể vô tình vi phạm |
| 2 | Quản trị thuê bao (Tenant) | Đăng ký, phân loại (HKD, DN siêu nhỏ, DN nhỏ/vừa, DN lớn, HTX), cấu hình cửa hàng |
| 3 | Phân quyền vai trò | Vai trò nền tảng: `SystemAdmin` (quản trị toàn hệ thống); vai trò thuê bao: `Owner` (chủ cửa hàng), `StoreKeeper` (quản lý quầy), nhân viên bán hàng |
| 4 | Quản lý máy chủ cửa hàng (`ShopInstance`) | Đăng ký VPS chạy từng cửa hàng, định tuyến sự kiện NATS theo mã máy chủ — nền tảng đa VPS |
| 5 | Cấu hình trực quan | Giao diện quản trị: tạo thuê bao, gán người dùng, khóa chuẩn kế toán, bật/tắt tính năng theo thuê bao (feature flags) |
| 6 | Hóa đơn điện tử & tuân thủ | Rà soát tuân thủ pháp lý kế toán/ERP theo checklist; cảnh báo nghiệp vụ vượt ngưỡng |

## 4.6. Phân hệ 6 — Data Sync & Outbox Engine (Đồng bộ dữ liệu & hàng đợi sự kiện bất khả thất bại)

**Vị trí mã nguồn:** `1_Shared/Domain/OutboxMessage.cs`, `3_CoreHub/Infrastructure/OutboxMessage.cs`, `2_Gateway` (NATS Publisher/Subscriber), NATS JetStream.

**Chức năng chính:**

| STT | Chức năng | Mô tả |
|---|---|---|
| 1 | Mẫu Outbox (Transactional Outbox) | Ghi sự kiện vào bảng `OutboxMessage` trong cùng giao dịch với nghiệp vụ — không mất sự kiện khi ghi đơn hàng |
| 2 | Xuất bản sự kiện (Publisher) | Tiến trình đọc Outbox → gửi tới NATS JetStream theo chủ đề định tuyến theo `ShopInstanceId` |
| 3 | Lắng nghe sự kiện (Subscriber) | Máy chủ cửa hàng nhận đúng sự kiện của mình, đồng bộ vào SQLite; đánh dấu đã xử lý |
| 4 | Cơ chế thử lại (Retry) | Lùi theo cấp số nhân (1, 2, 4, 8... đến 60 phút), ghi nhận lỗi, hẹn giờ thử lại — không mất tin |
| 5 | Danh mục sự kiện chuẩn | `EventTypes`: OrderCompleted, AccountingEntryCreated, HKDBooksGenerated, LoyaltyChanged, TenantTypeChanged... |
| 6 | Đồng bộ hai chiều | Đơn hàng: PG → SQLite (mới tạo); trạng thái đơn: SQLite → PG (cập nhật từ bếp/POS); cấu hình thuê bao: PG → SQLite |

**Ví dụ chủ đề định tuyến (NATS subject):** `vanan.cloud.order.created.{shopInstanceId}` — mỗi VPS cửa hàng chỉ nhận đơn của chính mình.

## 4.7. Các phân hệ bổ trợ (được gom trong cùng chương trình máy tính)

| Phân hệ bổ trợ | Chức năng chính |
|---|---|
| **Phí nền tảng (Metering)** | Tính cước phí nền tảng theo mức sử dụng, ví nạp tiền (top-up VietQR), cảnh báo hạn mức (≤50% warn, ≤10% critical), chặn khi hết hạn mức; nhật ký kiểm toán |
| **Hóa đơn điện tử (e-Invoice)** | Khởi tạo, điều chỉnh hóa đơn điện tử; trạng thái nhà cung cấp hóa đơn |
| **Thu chi & công nợ (AR/AP)** | Phiếu thu/chi, theo dõi phải thu/phải trả, tuổi nợ FIFO (<30, 30-60, 60-90, >90 ngày) |
| **Đặt lịch & lịch làm việc (Booking & Staff Scheduling)** | Đặt lịch theo khung giờ/nhân viên, chính sách đặt cọc, hủy, nhắc lịch |
| **Kho & nguyên vật liệu (Inventory)** | Quản lý nguyên liệu, công thức chế biến (Recipe), nhập/xuất kho, định mức tiêu hao |
| **Truyền thông & khách hàng thân thiết** | Thông báo đẩy (Push), chiến dịch khuyến mãi, chương trình khách hàng thân thiết, liên minh điểm thưởng |
| **Quản trị bán hàng qua Facebook Lead** | Tiếp nhận khách hàng tiềm năng, onboarding |

---

# CHƯƠNG 5 — MÔ HÌNH CƠ SỞ DỮ LIỆU LOGIC

## 5.1. Tổng quan

Phần mềm sử dụng **hai kho dữ liệu bổ trợ**:

- **PostgreSQL (Kho dữ liệu đám mây — Gateway):** Thuê bao (Tenant), Người dùng (User), Máy chủ cửa hàng (ShopInstance), Đơn hàng (Order, OrderItem), Kế toán (JournalEntry, AccountingEntry), Sản phẩm nổi bật (FeaturedProduct), Ví phí nền tảng, Nhật ký sự kiện (OutboxMessage).
- **SQLite (Kho dữ liệu cửa hàng — ShopERP):** Sản phẩm (Product), Nguyên liệu (Ingredient), Công thức (Recipe), Kho (Inventory), Khách hàng (Customer), Đơn hàng phản chiếu cho bếp/POS, Sổ kế toán HKD (GenericHKDBook), Cấu hình thuê bao.

## 5.2. Danh mục thực thể chính

### a) Nhóm kế toán (Core Accounting)

| Thực thể | Kiểu | Mô tả |
|---|---|---|
| `AccountingEntry` | Bút toán chi tiết (sổ phụ) | Ghi nhận từng nghiệp vụ: số tiền, thuế GTGT, phương thức thanh toán, đối tượng, ngày nghiệp vụ — **bất biến, append-only** |
| `JournalEntry` / `JournalEntryLine` | Bút toán tổng hợp (sổ cái) | Bút toán kép Nợ/Có, kiểm soát cân đối ΣNợ = ΣCó, số chứng từ tự sinh, hỗ trợ bút toán đảo |
| `BusinessProfile` | Hồ sơ kinh doanh | Chi phí cố định tháng, năng lực sản xuất, mô hình định giá, phiên bản mô hình tài chính |
| `AccountingPeriod` | Kỳ kế toán | Năm/Tháng — gom nhóm bút toán theo kỳ |
| `Money`, `TenantId`, `AccountingEntryId` | Value Object | Biểu diễn giá trị và định danh nghiệp vụ |

### b) Nhóm sổ HKD & chuẩn mực

| Thực thể | Kiểu | Mô tả |
|---|---|---|
| `HKDBookTemplate` (S1a, S2a...S3a) | Mẫu sổ khai báo | Định nghĩa trường + công thức tính cho từng loại sổ TT 152 |
| `GenericHKDBook` | Sổ kế toán HKD | Kết quả sinh sổ theo kỳ, chứa giá trị tính toán |
| `AccountingStandardRules` | Quy tắc chuẩn mực | Khóa chuẩn mực hợp lệ theo loại hình thuê bao |

### c) Nhóm bán hàng & đơn hàng

| Thực thể | Kiểu | Mô tả |
|---|---|---|
| `Order` / `OrderItem` | Đơn hàng / dòng đơn | Đơn hàng đa thuê bao, khuyến mãi, khách hàng, nguồn đặt hàng (POS, PWA, Booking) |
| `CartItem` | Dòng giỏ hàng | Sản phẩm + số lượng + giá + thuế suất snapshot (RC-7) + thuê bao sở hữu |
| `FeaturedProduct` | Sản phẩm nổi bật | Sản phẩm marketing trên trang chủ (Paid/Free/Charity) |
| `OrderFlowProfile` / `OrderFlowStep` / `OrderStatusHistory` | Luồng đơn động | Cấu hình bước xử lý đơn + lịch sử chuyển trạng thái |
| `Product`, `Customer` | Danh mục | Sản phẩm cửa hàng, khách hàng (mức danh tính 1-2-3) |

### d) Nhóm nền tảng & đồng bộ

| Thực thể | Kiểu | Mô tả |
|---|---|---|
| `Tenant` | Thuê bao | Loại hình kinh doanh, cấu hình, trạng thái |
| `UserTenant`, `UserRole` | Người dùng - thuê bao | Gán vai trò Owner/StoreKeeper/SystemAdmin |
| `ShopInstance` | Máy chủ cửa hàng | Đăng ký VPS, mã định tuyến sự kiện |
| `OutboxMessage` | Hộp thư sự kiện | Sự kiện chờ xuất bản: loại, payload JSON, số lần thử lại, lỗi |
| `BaseEntity`, `IMustHaveTenant`, `IAuditableEntity` | Nền tảng | Định danh, đa thuê bao, nhật ký kiểm toán (CreatedAt/UpdatedAt/CreatedBy/UpdatedBy) |

### e) Nhóm bổ trợ

`Ingredient`, `Recipe`, `RecipeLine`, `Inventory`, `InventoryCount`, `Shift`, `ShiftAlert`, `TheoreticalConsumption` (kho & ca làm việc); `ElectronicInvoice` (hóa đơn điện tử); `LoyaltyIssuanceRecord`, `AllianceWallet`, `AllianceTransaction` (khách hàng thân thiết); `Voucher`, `PromoCampaign`, `PushSubscription` (truyền thông); `TenantNotification`; `IdempotentOperation` (chống trùng lặp); `EntityVersion` (kiểm soát phiên bản).

---

# CHƯƠNG 6 — CƠ CHẾ BẢO MẬT VÀ CÁCH LY DỮ LIỆU ĐA THUÊ BAO

## 6.1. Xác thực và phân quyền

- **Xác thực:** JWT Bearer Token (ASP.NET Core Identity), mật khẩu băm an toàn; phiên đăng nhập qua Cookie cho ứng dụng web.
- **Phân quyền hai tầng:**
  - Tầng nền tảng: `PlatformRole.SystemAdmin` — quản trị đa thuê bao (tạo cửa hàng, cấu hình máy chủ, khóa chuẩn kế toán).
  - Tầng thuê bao: `Owner` (toàn quyền cửa hàng), `StoreKeeper` (vận hành quầy, kho, báo cáo), nhân viên (POS cơ bản).
- **Áp dụng nghiêm ngặt:** mọi API yêu cầu xác thực và kiểm tra quyền; endpoint ẩn danh được rà soát và liệt kê tường minh (kiểm tra kiến trúc tự động trong CI).

## 6.2. Cách ly dữ liệu đa thuê bao (Multi-Tenancy)

- **Hợp đồng bắt buộc:** mọi thực thể dữ liệu triển khai `IMustHaveTenant` — lập trình viên không thể tạo bảng dữ liệu mới mà thiếu cột thuê bao.
- **Bộ lọc toàn cục (Global Query Filters):** EF Core tự động thêm điều kiện `TenantId = <thuê bao hiện tại>` vào **mọi** truy vấn — ngăn chặn rò rỉ dữ liệu giữa các cửa hàng ở tầng dữ liệu.
- **Xác định thuê bao từ ngữ cảnh HTTP:** middleware trích `X-Tenant-ID`/đường dẫn, xác thực quyền truy cập thuê bao của người dùng trước khi phục vụ.
- **Cách ly vật lý:** dữ liệu nghiệp vụ cửa hàng nằm trên SQLite của chính máy chủ cửa hàng (VPS riêng); dữ liệu nền tảng tập trung tại PostgreSQL Gateway.
- **Phân tách tác vụ quản trị:** SystemAdmin thao tác qua API quản trị riêng, có nhật ký kiểm toán (audit log) đầy đủ.

## 6.3. Bảo mật dữ liệu

- **Truyền tải:** HTTPS/TLS 1.2+ toàn tuyến; WebSocket qua SignalR có xác thực.
- **Mã hóa nhạy cảm:** token đặt lịch (QR) được mã hóa bằng Data Protection; khóa API dịch vụ chỉ lưu trên máy chủ.
- **Nhật ký kiểm toán:** mọi thay đổi quan trọng (thuê bao, chuẩn kế toán, cấu hình, ví phí nền tảng) đều ghi nhật ký kèm người thực hiện (IP/User-Agent).
- **Chống trùng lặp & nhất quán:** bảng `IdempotentOperation` chặn xử lý trùng sự kiện; `EntityVersion` kiểm soát xung đột cập nhật đồng thời.
- **Tuân thủ:** quy trình rà soát tuân thủ pháp lý kế toán/ERP được xây dựng thành checklist kiểm soát tự động (architecture guard + test) ngay trong pipeline phát triển.

---

# PHỤ LỤC A — TÓM TẮT TÁC PHẨM
*(Dùng để điền Mục "Nêu tóm tắt về tác phẩm" của Mẫu số 03 — Tờ khai đăng ký quyền tác giả đối với chương trình máy tính, ban hành kèm Thông tư 08/2026/TT-BVHTTDL)*

**Chức năng, thành phần, cấu tạo:**

Hệ thống Phần mềm Quản lý và Hạch toán Thương mại ShopERP (ShopERP System) là nền tảng SaaS đa thuê bao gồm 6 phân hệ chính:

1. **Core Accounting Engine** — lõi kế toán hai lớp (sổ phụ bút toán chi tiết + sổ cái bút toán kép), bút toán bất biến, kiểm soát cân đối Nợ/Có, bút toán đảo, dãy số chứng từ, phân bổ thanh toán công nợ FIFO, khóa sổ kỳ, nhập liệu kế toán thông minh bằng câu mô tả tiếng Việt.
2. **HKD & Regime Accounting Module** — công cụ tự động sinh sổ kế toán hộ kinh doanh S1a, S2a, S2b, S2c, S2d, S2e, S3a theo Thông tư 152/2025/TT-BTC; tính thuế GTGT/TNCN theo nhóm ngành; khóa chuẩn mực kế toán theo loại hình doanh nghiệp (TT 133, TT 99, TT 71, TT 58).
3. **Financial Intelligence Layer** — phân tích hòa vốn (đơn sản phẩm và đa sản phẩm), kinh tế đơn vị sản phẩm, biên an toàn, tổng hợp lợi nhuận; mọi tính toán gắn phiên bản mô hình tài chính.
4. **KhachLink & Order Management** — ứng dụng PWA cho khách hàng (đặt hàng, theo dõi, thanh toán VietQR), giỏ hàng đa thuê bao, quầy POS, luồng xử lý đơn hàng động, sản phẩm nổi bật, đặt lịch hẹn, hóa đơn điện tử.
5. **Multi-Tenancy & Platform Architecture** — cách ly dữ liệu đa thuê bao ở mọi tầng dữ liệu (IMustHaveTenant + Global Query Filters), phân quyền Owner/StoreKeeper/SystemAdmin, quản trị nhiều máy chủ ảo (ShopInstance).
6. **Data Sync & Outbox Engine** — đồng bộ dữ liệu nhất quán giữa PostgreSQL (đám mây) và SQLite (cửa hàng) qua mẫu Outbox kết hợp hàng đợi tin nhắn NATS JetStream, định tuyến theo máy chủ cửa hàng, thử lại theo cấp số nhân.

**Ngôn ngữ lập trình:** C# (.NET 8), JavaScript/TypeScript, Razor/Blazor (C#), SQL (PostgreSQL, SQLite), HTML/CSS.

**Sử dụng mã nguồn mở:** ☐ Không sử dụng / ☑ Có sử dụng — danh sách mã nguồn mở kèm đường dẫn được liệt kê tại Phụ lục B.

**Sử dụng hệ thống trí tuệ nhân tạo trong quá trình sáng tạo tác phẩm:** ☑ Có sử dụng — tên hệ thống AI: *(điền tên công cụ AI hỗ trợ phát triển)* — bản mô tả việc sử dụng và cam đoan trực tiếp sáng tạo tác phẩm kèm theo tài liệu này (Phụ lục C).

---

# PHỤ LỤC B — DANH MỤC MÃ NGUỒN MỞ SỬ DỤNG

*(Căn cứ yêu cầu khai báo tại Mẫu số 03 TT 08/2026/TT-BVHTTDL — chủ sở hữu cần rà soát file khai báo phụ thuộc (NuGet, npm) để hoàn thiện danh sách trước khi nộp. Các gói chính gồm:)*

| Nhóm | Ví dụ thư viện mã nguồn mở | Mục đích |
|---|---|---|
| .NET / ASP.NET Core | .NET 8 Runtime, ASP.NET Core, EF Core 8, SignalR, YARP | Nền tảng chạy, truy cập dữ liệu, proxy ngược |
| Message bus | NATS Client (.NET) | Hàng đợi sự kiện JetStream |
| Cơ sở dữ liệu | Npgsql (PostgreSQL), Microsoft.Data.Sqlite, SQLite | Truy cập cơ sở dữ liệu |
| Frontend | Blazor, Bootstrap (giao diện chuẩn), JavaScript (PWA, Service Worker) | Giao diện người dùng |
| Kiểm thử | xUnit, Playwright | Kiểm thử đơn vị và đầu cuối |

> Ghi chú: danh sách này là danh mục gợi ý; trước khi nộp hồ sơ, chủ sở hữu phải xuất danh sách đầy đủ từ công cụ quản lý phụ thuộc của dự án (NuGet `packages.lock.json`, npm `package-lock.json`) và bổ sung đường dẫn nguồn cho từng gói.

---

# PHỤ LỤC C — BẢN MÔ TẢ SỬ DỤNG AI VÀ CAM ĐOAN TRỰC TIẾP SÁNG TẠO

*(Mẫu khai báo theo quy định mới của Mẫu số 03 TT 08/2026/TT-BVHTTDL — chủ sở hữu điền và ký)*

1. **Tên hệ thống trí tuệ nhân tạo đã sử dụng trong quá trình sáng tạo tác phẩm:** *(điền tên công cụ, ví dụ: trợ lý lập trình AI; mô hình ngôn ngữ lớn dùng để viết/hỗ trợ viết mã nguồn)*.
2. **Mô tả việc sử dụng:** Công cụ AI được sử dụng như trợ lý hỗ trợ lập trình — gợi ý, hoàn thiện và rà soát mã nguồn theo định hướng, thiết kế và quyết định kiến trúc do tác giả/chủ sở hữu đưa ra. Toàn bộ quyết định thiết kế hệ thống (kiến trúc, mô hình dữ liệu, quy tắc nghiệp vụ kế toán, chuẩn mực) do con người quyết định và chịu trách nhiệm. Tác giả cam đoan kiểm soát, định hướng và trực tiếp tham gia sáng tạo toàn bộ nội dung chương trình máy tính nêu trên.
3. **Cam đoan của tác giả:** Tôi cam đoan nội dung chương trình máy tính (mã code, giao diện) do tôi/tổ chức trực tiếp sáng tạo, không sao chép từ tác phẩm của người khác, không vi phạm quy định của pháp luật Việt Nam; việc sử dụng công cụ AI chỉ là phương tiện hỗ trợ kỹ thuật, không thay thế vai trò sáng tạo của tác giả.

**Ngày ...... tháng ...... năm ......**
**Tác giả / Chủ sở hữu quyền tác giả**
*(ký, ghi rõ họ tên)*

---

*— Hết Bản mô tả kỹ thuật phần mềm —*
*Tài liệu được biên soạn tự động từ kho mã nguồn ShopERP (repo `Gemini_Windsurf`) — phục vụ Hồ sơ đăng ký quyền tác giả. Tài liệu này không phải là ý kiến tư vấn pháp lý; chủ sở hữu nên rà soát với luật sư/tổ chức dịch vụ đăng ký bản quyền trước khi nộp.*