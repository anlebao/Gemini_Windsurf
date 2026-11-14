# Task M1 — SÀN VIỆC (Job Market): `/jobs` page + ShowJobs activation

> **Status:** STUB — spec chi tiết khi bắt đầu module wave (user: "sẽ implement rất sớm")
> **Wave:** M (song song W1–W3) · **SRS:** §7.2.1
> **Phụ thuộc:** W1-1 (preset `Hoptacxa` — hiện `ShowJobs=false`, bật khi page ship).

## Mục tiêu giá trị

`ShowJobs` NavFlag (đã tồn tại trong DTO + admin preset + JobMarket profile) cuối cùng có page thật — portal hoptacxa có module sàn việc đầu tiên.

## Phạm vi dự kiến (chi tiết khi viết spec)

1. `/jobs` page trong KhachLink — danh sách việc theo cooperative/tenant (data từ W2-1 nếu có; nếu không, model `JobPost` riêng).
2. Domain: `JobPost` entity (tenant đăng việc — shipper/salesman recruitment fit community roles đã có?) — **cần spec:** liệu JobMarket = tuyển CTV/shipper trong hệ sinh thái, hay job board tổng quát.
3. `ProfileGuard RequiredFlag="ShowJobs"` + nav items (NavMenu cả desktop + mobile "Thêm").
4. Bật `ShowJobs=true` trong preset `Hoptacxa` (và JobMarket preset đã sẵn `true`).
5. Application flow tối thiểu: xem việc → apply/liên hệ (chat? reuse conversation).

## Open design questions (ghi khi spec)

- JobPost thuộc Tenant hay Cooperative? (member posting vs HTX posting)
- Apply → tạo Conversation/Order? hay chỉ contact info?
- Role gate: guest xem được? apply cần IdentityLevel nào?

## RV dự kiến

- [ ] `/jobs` 200 trên hoptacxa (ShowJobs ON).
- [ ] Nav "Sàn việc" hiện đúng profile, ẩn ở FullCommerce (ShowJobs=false).
