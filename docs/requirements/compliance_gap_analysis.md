# Accounting Compliance Gap Analysis

**Baseline SRS:** `SRS_Accounting_Software_Legal_Compliance_v1.0.md`  
**Gap source:** `docs/Accounting_Doc/gap_register_ketoan_phap_ly_2026-11-09.md`  
**Ngày phân tích:** 2026-11-09  
**Phạm vi:** ShopERP Accounting Engine theo SRS v1.0; tài liệu này không thay thế ý kiến pháp lý hoặc chứng nhận tuân thủ.  
**Trạng thái SRS:** Phase 0 đang được thực hiện. Không kết luận `DONE` khi thiếu implementation end-to-end, test evidence và reviewer độc lập.

---

## 1. Phương pháp và cách đọc trạng thái

Phân tích đối chiếu từng mục trong Gap Register với FR/NFR và test case của SRS, sau đó kiểm tra bằng chứng hiện có trong codebase và kết quả test thực thi.

### Trạng thái gap

| Trạng thái | Ý nghĩa |
|---|---|
| `VERIFIED_COMPLIANT` | Có implementation và evidence đủ cho acceptance criterion cụ thể; không bao hàm cam kết pháp lý toàn diện |
| `PARTIAL` | Có một phần control, nhưng còn thiếu requirement hoặc evidence |
| `MISSING` | Không tìm thấy implementation phù hợp trong phạm vi đã kiểm tra |
| `BLOCKED` | Không thể kết luận do thiếu cấu hình, dữ liệu, owner hoặc văn bản pháp lý |
| `NOT_APPLICABLE` | Chưa thuộc phạm vi release v1.0 hoặc tenant profile cụ thể |
| `UNVERIFIED` | Chưa đủ bằng chứng code/test/RV; không được xem là compliant |

### Quy tắc evidence

- Mã nguồn chỉ chứng minh **implementation hiện có**, không tự động chứng minh runtime production.
- Test pass chỉ chứng minh các scenario đã được test, không chứng minh mọi FR/NFR.
- Một feature tồn tại nhưng chưa có effective-date, RBAC hoặc audit đầy đủ vẫn là `PARTIAL`.
- Mọi `MISSING`, `BLOCKED`, `UNVERIFIED` phải được xử lý trước release gate nếu requirement tương ứng là MUST.

---

## 2. Current-state architecture map

| Capability hiện có | Bằng chứng hiện tại | Đánh giá sơ bộ |
|---|---|---|
| Accounting standard theo tenant | `1_Shared/Domain.cs` định nghĩa `AccountingStandard`; `1_Shared/Domain/AccountingStandardRules.cs` resolve theo `TenantType` và override | `PARTIAL`: có regime selection nhưng chưa có effective-date theo accounting period |
| Period close/open | `3_CoreHub/Services/PeriodClosingService.cs`; write paths kiểm tra `PeriodClosingStatus.Closed` | `PARTIAL`: có lock và reopen controls trong các write path đã đọc; cần kiểm tra toàn bộ producer và database-level boundary |
| Append-only / reversal | `1_Shared/Domain/Audit/AuditLog.cs`; `AccountingEntry` và reversal services được sử dụng trong accounting flows | `PARTIAL`: domain/service có append-only pattern; chưa đủ evidence database constraint/trigger |
| Audit before/after | `AuditLog` có `OldValues`, `NewValues`, `IpAddress`, `UserAgent`, `Reason`, `CorrelationId` | `PARTIAL`: model có trường; cần kiểm tra coverage thực tế ở mọi nghiệp vụ quan trọng và tính toàn vẹn chuỗi audit |
| Reconciliation | `3_CoreHub/Services/Reconcile/AccountingReconcileService.cs`; R1–R13; `ReconciliationItemService` phát sinh exception item | `PARTIAL`: có read-only checks và exception items; chưa đủ evidence scheduled execution, alerting, SLA xử lý và fail-closed policy |
| Business Event / Rule Engine | Có `VoucherTypeRegistry`, `SmartEntry` parser và các accounting posting services; không tìm thấy đầy đủ `ComplianceProfile`, `AccountingRuleSet`, `BusinessEvent` theo SRS | `PARTIAL`: có rule-like registry và posting strategies nhưng chưa có versioned compliance profile/effective-date framework |
| Ledger/report output | Có BCTC services, HKD books, trial balance, journal/ledger/reconciliation UI và một số export | `UNVERIFIED`: cần evidence từng mẫu báo cáo, mã chỉ tiêu, effective date, PDF/CSV/XLSX và drill-down end-to-end |
| Integration readiness | Có e-invoice providers và accounting integration surfaces | `PARTIAL`: cần API contract/versioning, external reference/state/error handling và evidence cho chữ ký số |
| Security / tenant isolation | Có RBAC, tenant context và architecture tests | `UNVERIFIED`: cần ma trận quyền, tenant isolation tests và security evidence theo từng release |

---

## 3. Gap analysis theo Gap Register

### 3.1 Gap pháp lý và compliance (A-G1 đến A-G18)

| Gap ID | Yêu cầu / gap | FR/NFR/TC liên quan | Trạng thái / classification | Evidence hiện có | Gap và hành động cần thiết | Ưu tiên |
|---|---|---|---|---|---|---|
| A-G1 | TK mới TT 99/2025: TK 215, chi tiết 82112, tên/kết cấu TK | FR-001, FR-003, FR-040, TC-023 | `UNVERIFIED` | `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` có chart seed; `6_Tests/VanAn.Core.Tests/Seed/AccountChartSeederTests.cs` tồn tại | Đối chiếu chart với text TT 99/2025 hiện hành; ghi source legal reference, version và effective date; thêm test cho TK 215/82112 và chart theo regime | P0 |
| A-G2 | Nhất quán tối thiểu một kỳ khi đổi chế độ; trình bày lại số liệu so sánh | FR-001, FR-002, FR-016, NFR-007, TC-015 | `MISSING` / `PARTIAL` | `AccountingStandard` và `AccountingStandardRules` resolve theo tenant/override; chưa tìm thấy effective-date theo `AccountingPeriod` | Tạo versioned compliance profile/rule set có `EffectiveFrom`, `EffectiveTo`, source legal reference; resolve rule theo period, không chỉ tenant hoặc thời điểm post | P0 |
| A-G3 | Hóa đơn máy tính tiền kết nối/chuyển cơ quan thuế theo NĐ 254/2026 và TT 91/2026 | FR-070, FR-071, FR-072, NFR-008, OI-006 | `PARTIAL` / `UNVERIFIED` | Có `3_CoreHub/Services/Orchestration/EInvoiceOrchestrator.cs`, `Services/Providers/EInvoice/`, UI EInvoice; master plan P0.4 ghi nhận cần rà soát chuẩn dữ liệu | Xác nhận tenant profile và provider contract; chứng minh luồng hóa đơn máy tính tiền, trạng thái truyền, lỗi, retry và external reference; không suy ra compliance chỉ từ provider integration | P0 |
| A-G4 | Mã số BCTC mới PL4 TT 99: bổ sung 124/125/126, sửa 134 | FR-040, FR-045, TC-023 | `PARTIAL` / `UNVERIFIED` | Có BCTC services và TT133/TT58 templates được ghi nhận trong `docs/AI/project_state.md`; chưa có evidence mapping đầy đủ mã số TT99 mới | Đối chiếu từng chỉ tiêu với phụ lục TT99 hiện hành; thêm golden tests cho mã số, tổng chỉ tiêu và version report template | P0 |
| A-G5 | Đối chiếu/cảnh báo hóa đơn đầu vào trên cổng thuế; trạng thái NCC bỏ trốn/ngừng HĐ | FR-072, FR-034, TC-020, TC-025 | `MISSING` / `PARTIAL` | Master plan P1.4 mô tả module hóa đơn đầu vào; không tìm thấy đủ implementation/evidence trong phạm vi phân tích | Xác định nguồn dữ liệu chính thức/API hoặc quy trình thủ công có owner; lưu external reference, trạng thái, thời điểm kiểm tra, cảnh báo và audit | P1 |
| A-G6 | Quy trình hóa đơn điều chỉnh/thay thế; không xóa/gạch; kê khai đúng kỳ | FR-021, FR-026, FR-070, FR-072, TC-009 | `PARTIAL` / `UNVERIFIED` | `AuditLog` có factory cho E-invoice correction; `AccountingReconcileService` không phải workflow hóa đơn; master plan P0.3 ghi nhận adjust/replace | Chứng minh provider-specific API, UI workflow, reason, liên kết hóa đơn gốc/mới, trạng thái đồng bộ và audit end-to-end | P1 |
| A-G7 | Gộp hóa đơn dưới 20 triệu cùng NCC cùng ngày từ 20 triệu trở lên; thanh toán không tiền mặt | FR-013, FR-022, FR-034, FR-044, TC-024 | `PARTIAL` / `UNVERIFIED` | Master plan P0.1 ghi nhận cảnh báo 20 triệu và báo cáo rà soát; `PaymentComplianceServiceTests` tồn tại | Chạy/đọc test chứng minh aggregation theo NCC + ngày + tenant + kỳ, payment method, reason và audit; bổ sung test biên và concurrent transactions | P0 |
| A-G8 | Chữ ký số ký BCTC/sổ/hóa đơn | FR-070, FR-071, NFR-008, OI-006 | `MISSING` / `UNVERIFIED` | Không tìm thấy digital-signature integration đủ trong phạm vi kiểm tra | Chốt provider/adapter, loại tài liệu được ký, certificate lifecycle, timestamp, audit và fallback; chỉ đánh dấu hoàn thành khi có end-to-end evidence | P2 |
| A-G9 | Export chuẩn thanh tra: XML định dạng cơ quan thuế và sổ theo mẫu phụ lục | FR-040, FR-041, FR-044, FR-056, TC-013, TC-020 | `PARTIAL` / `UNVERIFIED` | Có một số export/report services theo project state; chưa có evidence XML chuẩn TCT | Tạo report catalogue có mã mẫu/version/effective date; test schema, encoding, checksum/metadata và access control | P1 |
| A-G10 | eTax: tờ khai/nộp thuế điện tử, đối chiếu sổ với tờ khai | FR-072, FR-073, NFR-008, OI-006 | `NOT_APPLICABLE` / `BLOCKED` cho v1.0 trừ khi owner mở rộng phạm vi | Không có đủ evidence integration eTax | Nếu đưa vào release: tạo legal/profile decision, API contract, reconciliation và evidence; nếu không, ghi rõ out-of-scope và rủi ro sử dụng | P2 |
| A-G11 | Âm quỹ/âm kho tại thời điểm xuất (TK 111/152/156) | FR-013, FR-022, FR-034, NFR-001 | `PARTIAL` / `UNVERIFIED` | `IBalanceAlertService` được đăng ký trong test infrastructure; master plan P1.3 mô tả cảnh báo âm quỹ/âm kho | Chứng minh calculation theo thời điểm nghiệp vụ, concurrency, tenant isolation, warning/override policy và test các giao dịch đồng thời | P1 |
| A-G12 | Giao dịch liên kết và hồ sơ/tờ khai | LEG-06, FR-004, FR-044, OI-008 | `NOT_APPLICABLE` / `BLOCKED` cho tenant profile v1.0 | Master plan P2.3 ghi nhận chỉ thực hiện khi có nhu cầu | Xác định tenant có phát sinh giao dịch liên kết; nếu có, lập compliance profile và requirement riêng trước khi code | P2 |
| A-G13 | IFRS theo TT 118/2026 hiệu lực 01/01/2027 | LEG-03 boundary, FR-001, FR-040, FR-073 | `NOT_APPLICABLE` cho release hiện tại nếu chưa có tenant thuộc đối tượng | Master plan P2.4 ghi nhận khảo sát/roadmap | Theo dõi effective date; trước 2027 xác nhận tenant applicability; sau đó tạo change request và regression plan | P2 |
| A-G14 | Ngoại tệ quy đổi VND theo Điều 6 TT 99 | FR-022, FR-024, FR-040, NFR-001 | `UNVERIFIED` / `NOT_APPLICABLE` nếu tenant chỉ dùng VND | Chưa có evidence đầy đủ multi-currency trong phạm vi kiểm tra | Xác nhận currency policy; nếu hỗ trợ ngoại tệ, thêm rate source, effective date, rounding, historical immutability và tests | P1/P2 theo tenant |
| A-G15 | Audit giá trị trước/sau chi tiết | FR-050, FR-051, NFR-002, TC-010, TC-022 | `PARTIAL` | `AuditLog` có `OldValues`/`NewValues`; project state ghi nhận Q3 chỉ chọn IP/UserAgent và defer hash-chain | Kiểm tra các nghiệp vụ trọng yếu có snapshot đủ để reviewer xác định thay đổi; quyết định risk acceptance bằng văn bản nếu chưa đáp ứng MUST | P0 |
| A-G16 | Phân nhiệm quản trị IT và dữ liệu kế toán; ngăn can thiệp có chủ ý | FR-060, FR-061, FR-065, NFR-009, TC-001, TC-021 | `PARTIAL` / `UNVERIFIED` | Có role/RBAC và SystemAdmin boundaries trong code/tests; chưa có evidence break-glass và DB direct-access controls | Tạo ma trận quyền; kiểm thử admin không có quyền kế toán mặc định; ghi nhận break-glass, approval, audit và database access policy | P1 |
| A-G17 | Backup ít nhất hai nơi độc lập và restore drill định kỳ | FR-055, NFR-006, TC-019 | `UNVERIFIED` | `docs/AI/plans/accounting-legal-compliance-master-plan.md` ghi nhận backup infra; chưa có evidence restore drill trong phân tích này | Lưu backup schedule, storage locations, encryption, retention, restore test date, checksum/record count và phê duyệt RPO/RTO | P1 |
| A-G18 | BCTC được ký bởi kế toán trưởng và đại diện pháp luật; quy trình phê duyệt | FR-023, FR-050, Section 6, Section 14 | `MISSING` / `UNVERIFIED` | Không tìm thấy quy trình ký số/phê duyệt BCTC đầy đủ | Tách approval workflow khỏi digital signature; lưu actor, role, timestamp, version report và reason; ký số là adapter riêng | P1 |

### 3.2 Đánh giá năm yêu cầu kỹ thuật (B-1 đến B-5)

| Gap ID | Yêu cầu | FR/NFR/TC | Trạng thái | Evidence | Gap còn lại và acceptance cần đạt |
|---|---|---|---|---|---|
| B-1 | Immutable Posted Entries | FR-025, FR-026, FR-030, NFR-001, TC-007, TC-008, TC-018 | `PARTIAL` | `AuditLog` immutable; accounting services sử dụng reversal; `AccountingReconcileService` ghi rõ read-only | Cần test API/UI từ chối update/delete; chứng minh transaction atomic; xác nhận DB constraint/trigger hoặc defense-in-depth; kiểm tra mọi write path kể cả import/sync/manual |
| B-2 | Audit trail: ai, khi, trước/sau | FR-050, FR-051, FR-052, FR-036, NFR-002, TC-010, TC-022 | `PARTIAL` | `AuditLog` có actor fields, `OldValues`, `NewValues`, reason, IP/UserAgent, correlation; `AccountingReconcileService` audit kết quả reconcile | Cần coverage matrix theo action/entity; kiểm tra payload có đủ before/after; bảo vệ audit khỏi user thường; test export audit package |
| B-3 | Rule Engine không hard-code; có cấu hình và version | FR-002, FR-011, FR-012, FR-016, NFR-007, TC-015, TC-016 | `PARTIAL` | Có voucher registry và strategies; `AccountingStandardRules` tập trung regime; chart/template còn có hardcode theo gap register | Tách chart, account mapping, closing prefixes, tax accounts và report templates thành versioned data/rule definitions; thêm effective date và regression test khi đổi rule |
| B-4 | Reconciliation Orders → phiếu → sổ → BCTC | FR-033, FR-034, FR-035, FR-045, NFR-002, TC-013, TC-014 | `PARTIAL` | `AccountingReconcileService` R1–R13; R11/R13 provider fail-open; `ReconciliationItemService` tạo exception items | Cần định nghĩa SLA, retry, alert, owner; quyết định fail-open hay fail-closed theo risk; thêm scheduled/on-close execution và evidence production |
| B-5 | Period lock, kiểm soát mở lại và lịch sử | FR-035, FR-036, TC-011, TC-012 | `VERIFIED_COMPLIANT` cho các write paths đã kiểm tra; `PARTIAL` cho toàn hệ thống | `PeriodClosingService` có status/reopen; `AccountingEntryService`, `ManualJournalVoucherService`, `CongNoService`, `OpeningBalanceService`, `ImportService` kiểm tra `Closed`; test suite `PeriodClosingServiceTests` pass trong lần chạy 23/23 | Tiếp tục kiểm tra mọi producer mới, offline sync, direct repository và DB-level boundary; không mở rộng verdict toàn hệ thống chỉ từ các path đã đọc |

### 3.3 Khả năng thích ứng luật mới (C-1 đến C-4)

| Gap ID | Gap | FR/NFR/TC | Trạng thái | Evidence | Hành động |
|---|---|---|---|---|---|
| C-1 | Chart of accounts nằm trong code/dictionary hoặc seed, chưa có cấu hình versioned đầy đủ | FR-001, FR-003, FR-005, NFR-007 | `PARTIAL` | `AccountChartSeeder.cs` và `AccountChartSeederTests.cs`; `AccountingStandardRules.cs` | Chuyển chart thành tenant/regime-scoped, versioned, effective-dated; giữ audit trước/sau và migration plan |
| C-2 | Hardcode closing prefixes và tax accounts | FR-012, FR-016, NFR-007 | `PARTIAL` / `UNVERIFIED` | Gap register ghi nhận prefixes `5xx/6xx/911/421`, reseller `3331/1331`; chưa kiểm chứng toàn bộ call-sites | Tạo account-role/rule mapping có version; thêm tests phát hiện account không tồn tại hoặc không effective trong period |
| C-3 | BCTC services theo regime chưa có registry/strategy thống nhất | FR-002, FR-016, FR-040, FR-073, NFR-007, TC-023 | `PARTIAL` | Có TT133/TT58 templates và regime-aware report hub theo project state; chưa có evidence universal registry | Chuẩn hóa report template registry: form code, circular, effective date, account mapping, calculation version và test golden output |
| C-4 | Không có effective-date cho regime; resolve theo tenant | FR-001, FR-002, FR-016, NFR-007, TC-015 | `MISSING` / `PARTIAL` | `AccountingStandard` lưu trên tenant; `AccountingStandardRules` không có period effective-date trong phạm vi đã đọc | Đây là P0: thêm `ComplianceProfile`/`AccountingRuleSet` có effective range; migration phải giữ nguyên kết quả historical postings |

### 3.4 Digital Twin / Enterprise Operating Model gaps (D-1 đến D-7)

Các mục này không phải trực tiếp FR kế toán pháp lý trong SRS v1.0. Chúng được giữ trong gap analysis để tránh mất backlog, nhưng không được dùng để kết luận compliance accounting.

| Gap ID | Mô tả | Mapping SRS | Trạng thái | Evidence | Hành động |
|---|---|---|---|---|---|
| D-1 | `processingSteps` thiếu trên toàn bộ registry | Không map trực tiếp; liên quan documentation/operational evidence nếu dùng cho compliance | `UNVERIFIED` | Gap register là nguồn duy nhất trong phân tích này | Nếu registry được dùng làm evidence release, bổ sung processing steps và kiểm tra schema; nếu không, đánh dấu `NOT_APPLICABLE` cho SRS accounting v1.0 |
| D-2 | `ruleRefs`, `kpiRefs`, `approvalRequired:false` chưa backfill | FR-002/FR-073 chỉ gián tiếp nếu registry là rule source | `UNVERIFIED` | Gap register | Không dùng registry chưa hoàn chỉnh làm bằng chứng versioned rule set; hoàn thiện hoặc tách accounting rule registry riêng |
| D-3 | Narrative process-details thiếu “Decisions & rationale” | Section 14 evidence package | `PARTIAL` | SRS yêu cầu evidence; gap register ghi nhận narrative mỏng | Bổ sung decision log cho các thay đổi kế toán quan trọng, owner và legal reference |
| D-4 | Thiếu sequenceDiagram cho NATS/reconcile handoff | NFR-009, Section 11.2 | `PARTIAL` / `UNVERIFIED` | Có NATS và reconcile services; chưa có evidence diagram/runtime trace đầy đủ | Vẽ sequence diagram và correlation ID xuyên suốt event → voucher → journal → ledger → report |
| D-5 | Lane title chưa đúng convention SRS Digital Twin | Không map trực tiếp SRS accounting v1.0 | `NOT_APPLICABLE` / `UNVERIFIED` | Gap register | Xử lý trong Digital Twin backlog riêng; không chặn accounting release trừ khi artifact được dùng làm evidence |
| D-6 | Start/end events và decision diamonds chưa đầy đủ | Section 11/12 gián tiếp | `PARTIAL` / `UNVERIFIED` | Gap register | Bổ sung workflow diagrams cho voucher state, period close/reopen và reconciliation exception |
| D-7 | Drift report residual và mandatory NATS findings | FR-054, NFR-009, Section 11.2 | `PARTIAL` / `UNVERIFIED` | Gap register ghi nhận 4 conditional findings và 23 mandatory NATS findings | Phân loại false positive/true positive; tạo remediation hoặc risk acceptance; không đóng reconcile/sync gap nếu còn mandatory finding chưa xử lý |

---

## 4. Cross-cutting SRS requirements chưa có evidence đầy đủ

### 4.1 Business Event và Rule Engine

SRS yêu cầu `BusinessEvent` có source system, source ID, tenant, event type, occurred-at và correlation ID; idempotency; manual review khi không xác định rule; effective-date testing; không tự động post chỉ vì rule match.

**Trạng thái:** `PARTIAL`.  
**Evidence:** có các posting services và `VoucherTypeRegistry`, nhưng chưa tìm thấy đầy đủ lifecycle `Received → Validated → Classified → RuleMatched/ManualReview → VoucherDraft → Approval → Posted`.  
**Gap MUST:** FR-010 đến FR-016 và TC-002, TC-003, TC-006 cần implementation/evidence rõ ràng trước release gate.

### 4.2 Atomic posting và ledger integrity

SRS yêu cầu posting atomic, ledger tạo từ posted data, drill-down, reconciliation report và period lock.

**Trạng thái:** `PARTIAL`.  
**Evidence:** reconcile R1/R2/R6 kiểm tra cân đối; period lock có trong nhiều services; tests reconcile/period closing đã pass trong lần chạy targeted.  
**Gap MUST:** cần test fault injection, retry/idempotency, cross-store conflict, ledger/report drill-down và evidence rằng ledger không lấy Draft.

### 4.3 Audit và data integrity

SRS yêu cầu audit create/update/approve/post/adjust/reverse/close/reopen/config/security; before/after hoặc snapshot; audit không thể bị user nghiệp vụ sửa.

**Trạng thái:** `PARTIAL`.  
**Evidence:** `AuditLog` có các trường cần thiết và immutable domain pattern.  
**Gap MUST:** FR-050 đến FR-056 cần coverage matrix và test theo từng action; chưa đủ evidence cho toàn bộ write paths.

### 4.4 Security, RBAC và tenant isolation

SRS yêu cầu RBAC theo tenant/legal entity/function/action, least privilege, service credentials, TLS/encryption, security logs và break-glass controls.

**Trạng thái:** `UNVERIFIED`.  
**Evidence:** có architecture/security tests trong repository, nhưng phân tích này chưa chạy full security suite hoặc review deployment configuration.  
**Gap MUST:** cần test matrix, tenant isolation test, secret-management evidence, access review và break-glass procedure.

### 4.5 Integration và upgradeability

SRS yêu cầu versioned API/contract, adapter, external reference/status/error, migration có version/rollback/forward plan, legal change impact assessment.

**Trạng thái:** `PARTIAL`.  
**Evidence:** có e-invoice provider/orchestrator và một số integration clients.  
**Gap MUST:** cần contract tests, API version policy, migration evidence và legal-change register.

### 4.6 Output, reports và compliance evidence

SRS yêu cầu report template có mã/version/effective date, PDF và CSV/XLSX, drill-down, export theo quyền, kiểm tra cân đối trước phát hành, evidence package.

**Trạng thái:** `UNVERIFIED`.  
**Evidence:** có BCTC/report services và UI theo project state; chưa có đủ sample output và test evidence trong phân tích này.  
**Gap MUST:** FR-040 đến FR-045, FR-056 và Section 14 cần report catalogue, golden tests, export samples và reviewer sign-off.

### 4.7 Backup, restore và RPO/RTO

SRS yêu cầu backup schedule, restore test, RPO/RTO do Product Owner/Architect/Operations xác định.

**Trạng thái:** `UNVERIFIED`.  
**Evidence:** có tài liệu backup infra trong master plan nhưng chưa có restore-drill evidence.  
**Gap MUST:** NFR-006/FR-055/TC-019 cần biên bản restore test và phê duyệt RPO/RTO.

---

## 5. Traceability matrix — Gap Register đến SRS

| Gap Register | SRS primary mapping | Secondary mapping | Release risk |
|---|---|---|---|
| A-G1, A-G4, C-1, C-3 | FR-001, FR-003, FR-040 | FR-005, NFR-007 | Cao: sai chart/report theo regime |
| A-G2, C-4 | FR-001, FR-002, FR-016 | NFR-007, TC-015 | Cao: áp sai luật cho kỳ lịch sử |
| A-G3, A-G6, A-G9, A-G10 | FR-070–FR-072 | FR-044, NFR-008, TC-020 | Cao nếu tenant thuộc phạm vi tích hợp |
| A-G5, A-G7, A-G11 | FR-013, FR-022, FR-034 | FR-044, TC-024 | Cao: kiểm soát nghiệp vụ và đối soát |
| A-G8, A-G18 | FR-023, FR-070, FR-071 | Section 6, Section 14 | Trung bình/Cao theo loại chứng từ |
| A-G12, A-G13, A-G14 | FR-001, FR-004, FR-040 | OI-002, OI-008 | Phụ thuộc tenant applicability |
| A-G15–A-G17, B-2 | FR-050–FR-055, FR-060–FR-065 | FR-036, NFR-002, NFR-006, NFR-009, TC-001, TC-010, TC-019, TC-021–TC-022 | Cao: audit, tenant/security và recoverability MUST |
| B-1, B-5 | FR-025, FR-026, FR-030, FR-035 | NFR-001, TC-007–TC-012, TC-018 | Cao: posted integrity |
| B-3, B-4, C-2 | FR-010–FR-016, FR-033–FR-036 | FR-073, FR-074, NFR-007–NFR-009 | Cao: maintainability và reconciliation |
| D-1–D-7 | Không phải accounting FR trực tiếp | NFR-009, Section 14 nếu dùng làm evidence | Trung bình: quản lý backlog/documentation |

---

## 6. Test evidence đã thực hiện trong phân tích này

### 6.1 Targeted automated tests

Lệnh:

```powershell
dotnet test "6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj" --no-restore --filter "FullyQualifiedName~AccountingReconcileServiceTests|FullyQualifiedName~PeriodClosingServiceTests"
```

Kết quả:

```text
Passed! - Failed: 0, Passed: 23, Skipped: 0, Total: 23
```

**Phạm vi chứng minh:** các test thuộc `AccountingReconcileServiceTests` và `PeriodClosingServiceTests` đã pass tại thời điểm chạy. Kết quả này không chứng minh toàn bộ FR/NFR, production runtime, tenant isolation hoặc legal compliance tổng thể.

### 6.2 Source evidence đã kiểm tra

| Evidence | File | Ý nghĩa |
|---|---|---|
| Accounting period lock | `3_CoreHub/Services/PeriodClosingService.cs` | Có status, close/reopen logic và optional reconcile integration |
| Write-path guard | `3_CoreHub/Services/AccountingEntryService.cs` | Các method tạo entry kiểm tra `PeriodClosingStatus.Closed` |
| Manual journal guard | `3_CoreHub/Services/Journal/ManualJournalVoucherService.cs` | Kiểm tra period status trước khi tạo journal voucher |
| Import guard | `3_CoreHub/Services/Import/ImportService.cs` | Import bị chặn khi kỳ đã đóng |
| Reconciliation R1–R13 | `3_CoreHub/Services/Reconcile/AccountingReconcileService.cs` | Có checks, fail-safe từng check, exception items; R11/R13 có provider fail-open |
| Reconcile models | `3_CoreHub/Services/Reconcile/ReconcileModels.cs` | Ghi rõ R11/R13 fail-open khi provider lỗi/không cấu hình |
| Audit model | `1_Shared/Domain/Audit/AuditLog.cs` | Có OldValues/NewValues, actor, reason, IP/UserAgent, correlation |
| Accounting regime rules | `1_Shared/Domain/AccountingStandardRules.cs` | Resolve theo tenant type/override; chưa thấy period effective-date |
| Legal master plan | `docs/AI/plans/accounting-legal-compliance-master-plan.md` | Nguồn backlog P0–P2 và trạng thái triển khai theo wave |

---

## 7. Existing / Modify / Missing / Incorrect / Not Applicable summary

| Classification | Các gap tiêu biểu | Ghi chú |
|---|---|---|
| `Existing` | Period lock write paths, R1–R13 reconciliation, audit model fields, e-invoice provider/orchestrator, voucher registry, TT133/TT58 templates | “Existing” không đồng nghĩa `DONE`; phải có test/RV/evidence |
| `Modify` | Accounting regime resolution, chart of accounts, account mapping, report templates, audit coverage, reconciliation exception policy | Cần version/effective-date và migration an toàn |
| `Missing` | Period-scoped effective-date rule sets, complete Business Event lifecycle, digital signature, eTax nếu thuộc scope, legal evidence package | Ưu tiên theo tenant applicability và release gate |
| `Incorrect` | Chưa có kết luận `Incorrect` chắc chắn trong phân tích này; các mục hardcode/fail-open cần verify thêm trước khi phân loại | Không sửa code trong Phase 0 |
| `Not Applicable` | A-G12/A-G13/A-G10 và một số D-items nếu tenant không thuộc đối tượng hoặc artifact không dùng làm compliance evidence | Phải có owner/legal/profile decision, không tự suy diễn |

---

## 8. Ưu tiên remediation

### P0 — phải xử lý hoặc có risk acceptance trước release gate

1. **C-4/A-G2:** Compliance profile và rule set theo `AccountingPeriod`, có effective date.
2. **A-G15/B-2:** Audit before/after coverage cho các nghiệp vụ trọng yếu; bảo vệ audit trail.
3. **A-G1/A-G4/C-1/C-3:** Đối chiếu chart of accounts, mã BCTC và report templates với văn bản TT99 hiện hành.
4. **A-G7:** Chứng minh kiểm soát thanh toán 20 triệu, đặc biệt aggregation cùng NCC/ngày.
5. **B-1/B-5:** Hoàn thiện evidence posted immutability, atomic posting và toàn bộ period-lock write paths.
6. **A-G16/A-G17:** Tenant isolation, least privilege, backup/restore evidence.

### P1 — cần hoàn thiện cho operational compliance

1. A-G3/A-G6/A-G9: hóa đơn máy tính tiền, điều chỉnh/thay thế, export chuẩn.
2. A-G5/A-G11: hóa đơn đầu vào và cảnh báo âm quỹ/âm kho.
3. B-3/B-4/C-2: versioned rule engine, reconciliation SLA và fail-closed policy cho check bắt buộc.
4. A-G18: approval workflow và evidence ký BCTC.
5. FR-010–FR-016: Business Event lifecycle, idempotency và manual review.

### P2 — theo tenant applicability hoặc roadmap

1. A-G8: chữ ký số.
2. A-G10: eTax.
3. A-G12: giao dịch liên kết.
4. A-G13: IFRS TT118/2026.
5. A-G14: ngoại tệ nếu có tenant sử dụng.
6. D-1–D-7: Digital Twin/EOM documentation backlog, tách khỏi accounting release gate trừ khi được dùng làm evidence.

---

## 9. Open issues cần owner decision

| ID | Issue | Owner đề xuất | Quyết định cần có |
|---|---|---|---|
| OA-001 | Tenant/legal entity và compliance profile áp dụng release đầu | Product Owner + Accounting Lead | Chốt loại hình, năm tài chính, chế độ kế toán, currency và effective date |
| OA-002 | TT99/TT133/TT58/TT71/TT152 applicability theo period | Compliance Owner + Accounting Lead | Chốt legal baseline và historical treatment |
| OA-003 | Report catalogue và mã chỉ tiêu bắt buộc | Accounting Lead | Chốt sổ/BCTC cần phát hành và mẫu pháp lý |
| OA-004 | Approval workflow, hạn mức và segregation of duties | Product Owner + Customer | Chốt actor/role/threshold |
| OA-005 | RPO/RTO, retention, backup locations | Architect + Operations + Legal | Chốt policy và restore drill |
| OA-006 | E-invoice, digital signature, bank/tax adapters | Product + Integration Lead | Chọn provider, API version và scope |
| OA-007 | HKD/cá nhân kinh doanh/HTX profiles | Product Owner + Legal + Accounting Lead | Xác nhận văn bản và SRS phụ lục riêng |
| OA-008 | Reconciliation fail-open/fail-closed | Accounting Lead + Architect | Phân loại check bắt buộc và exception SLA |
| OA-009 | Legal change register và review cadence | Compliance Owner | Chốt người kiểm tra hiệu lực trước mỗi release |

---

## 10. Phase 0 exit assessment

### Đã hoàn thành trong phân tích này

- Đối chiếu toàn bộ mục A-G1–A-G18, B-1–B-5, C-1–C-4 và D-1–D-7 với FR/NFR/TC.
- Xác định trạng thái `Existing / Modify / Missing / Not Applicable / Unverified`.
- Gắn evidence file-level cho các control đã kiểm tra.
- Thực hiện targeted test cho reconcile và period closing: 23/23 pass.
- Xác định P0/P1/P2 và các open issues cần owner.

### Chưa đủ để kết luận Phase 0 hoàn tất

- Chưa có legal baseline được Compliance Owner ký xác nhận cho từng tenant/profile.
- Chưa có full FR/NFR implementation matrix với test evidence cho mọi MUST.
- Chưa có production RV cho tất cả report, integration, security và recovery flows.
- Chưa có migration plan cho versioned compliance profile/rule set.
- Chưa có reviewer độc lập sign-off theo Definition of Done.

### Kết luận

Gap Register và SRS không mâu thuẫn; chúng đại diện cho hai lớp khác nhau:

- **Gap Register:** current-state diagnosis và backlog ưu tiên.
- **SRS:** target requirements, acceptance criteria và release evidence.

`compliance_gap_analysis.md` là cầu nối bắt buộc giữa hai lớp. Không item nào được đánh dấu `DONE` chỉ vì có tên class, UI hoặc test riêng lẻ; phải có end-to-end implementation, test evidence, audit/security evidence và reviewer sign-off tương ứng.
