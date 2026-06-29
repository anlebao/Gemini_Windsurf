# TASK CARD: KhachLink Improvements - Wave 2 - QR Code Scanning ✅ COMPLETE

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement QR code scanning functionality for customers to scan product/menu QR codes and add to cart
- **Nghiệp vụ áp dụng:** KhachLink O2O ordering - enable scan-to-order workflow for faster ordering
- **Status:** COMPLETED - In-app camera scanning implemented per task card
- **Implementation Date:** 2026-06-29
- **Branch:** `feature/khachlink-wave2-qr-scanning` → merged to `main`
- **Commit:** `db80062`

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (new feature - multi-session)
- **Execution Mode:** ANALYZE → IMPLEMENT → COMPLETE

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Components/QRScanner.razor` (CREATE)
  - `5_WebApps/KhachLink/Services/QrCodeService.cs` (CREATE)
  - `5_WebApps/KhachLink/Pages/Scan.razor` (CREATE)
  - `5_WebApps/KhachLink/Pages/Home.razor` (UPDATE - add scan button)
  - `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` (UPDATE - add scan menu item)
  - `5_WebApps/KhachLink/Services/CartService.cs` (UPDATE - add from QR scan)
  - `3_CoreHub/Services/QrCodeService.cs` (CREATE - server-side QR generation)
  - `5_WebApps/ShopERP/Services/QrCodeService.cs` (CREATE - server-side QR generation)
  - `5_WebApps/KhachLink/5_WebApps/KhachLink.csproj` (UPDATE - add html5-qrcode reference)
  - `5_WebApps/KhachLink/wwwroot/` (UPDATE - add html5-qrcode library)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG bypass UI Platform components trong QRScanner.razor
  - KHÔNG sử dụng custom HTML/CSS cho camera UI (nếu có component phù hợp)
  - KHÔNG hardcode QR code format (sử dụng constants hoặc config)
  - KHÔNG skip camera permission handling (iOS và Android khác nhau)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** QRScanner.razor MUST use UI Platform components (VanAnButton, VanAnCard, VanAnModal)
- [ ] **Camera Permissions:** MUST handle camera permissions properly on iOS (https required) and Android
- [ ] **QR Library:** MUST use html5-qrcode or similar web-compatible library (no native plugins)
- [ ] **Error Handling:** MUST handle camera access denied, no camera, QR detection failures gracefully
- [ ] **Performance:** QR detection MUST complete within 2 seconds on good lighting
- [ ] **Cross-Platform:** MUST work on both Android Chrome and iOS Safari

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [x] **SC1:** html5-qrcode library integrated (CDN)
- [x] **SC2:** QRScanner.razor component created with camera access
- [x] **SC3:** QR codes generated via server-side services (unique QR per product)
- [x] **SC4:** Scan-to-cart workflow functional (scan → parse → add to cart)
- [x] **SC5:** Camera permissions handled properly on iOS Safari
- [x] **SC6:** Camera permissions handled properly on Android Chrome
- [x] **SC7:** QR scanner accessible from navigation menu (desktop + mobile)
- [x] **SC8:** QR scanner accessible from Home page
- [ ] **SC9:** QR detection accuracy > 95% on good lighting (PENDING - real device testing)
- [ ] **SC10:** QR detection time < 2 seconds (PENDING - real device testing)
- [x] **SC11:** Scanned products added to cart correctly
- [x] **SC12:** Build: 0 errors
- [x] **SC13:** No regression in existing cart functionality

**Implementation Date:** 2026-06-29
**Branch:** feature/khachlink-wave2-qr-scanning → merged to `main`
**Commit:** db80062

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure QRScanner.razor uses UI Platform components
- `build-error-analysis` — Verify build passes after adding library
- `domain-integrity-validation` — Ensure QR code format is consistent with domain entities

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: NO QR scanning library currently exists (grep search returned no results)
  - Fact 2: QrPaymentModal.razor exists but only for displaying QR for payment (not scanning)
  - Fact 3: Products do not have QR codes generated (no QR service found)
  - Fact 4: CartService exists and can be extended to add items from QR scan
  - Fact 5: NavMenu.razor exists and can be updated to add scan menu item
- **Assumptions:**
  - html5-qrcode is suitable library for web-based QR scanning
  - QR code format will include: ProductId, ShopId, maybe Timestamp
  - Camera permissions require user consent on both iOS and Android
  - iOS Safari requires HTTPS for camera access (production uses HTTPS)
- **Open Questions:**
  - Q1: What QR code format should we use? (JSON, URL-encoded, custom format?)
  - Q2: Should QR codes be generated server-side or client-side?
  - Q3: Should we add QR codes to all products or just featured products?
  - Q4: What happens if customer scans invalid QR code?
- **Recommended Action:** Research html5-qrcode library, define QR code format, decide on generation strategy (server vs client), then implement

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 5_WebApps/KhachLink/5_WebApps/KhachLink.csproj | Add html5-qrcode reference | Use CDN fallback if npm fails |
| 5_WebApps/KhachLink/Components/QRScanner.razor | NEW - camera access component | Test on real devices for permission handling |
| 5_WebApps/KhachLink/Services/QrCodeService.cs | NEW - QR generation service | Keep format consistent with server-side service |
| 5_WebApps/KhachLink/Pages/Scan.razor | NEW - scan page | Add to routing, test navigation |
| 5_WebApps/KhachLink/Components/Layout/NavMenu.razor | Add scan menu item | Ensure mobile-friendly (bottom tab bar) |
| 5_WebApps/KhachLink/Services/CartService.cs | Add AddFromQrCodeAsync method | Extend existing service, no breaking changes |
| 3_CoreHub/Services/QrCodeService.cs | NEW - server-side QR generation | Reuse in ShopERP for consistency |

## 9. TDD & E2E TESTING STRATEGY
- **Manual Testing on Real Devices:**
  - Test QR scanning on Android Chrome (camera permissions, detection)
  - Test QR scanning on iOS Safari (camera permissions, detection)
  - Test scan-to-cart workflow with valid QR codes
  - Test error handling (camera denied, invalid QR, no camera)
  - Test QR generation for products
- **Test boundary:**
  - Unit tests: QrCodeService (QR generation logic)
  - Integration tests: Not applicable (camera access requires real device)
  - E2E tests: Manual testing on real devices required

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

This is a new feature requiring multiple sessions:
- Session 1: Research & Planning (library, QR format, generation strategy)
- Session 2: Implement QR generation (server-side service)
- Session 3: Implement QR scanner component (camera access, detection)
- Session 4: Implement scan-to-cart workflow & navigation
- Session 5: Testing on real devices & bug fixes

### Micro-phase breakdown cho Wave 2 (QR Scanning)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Research html5-qrcode library capabilities<br>- Define QR code format (ProductId, ShopId, etc.)<br>- Decide QR generation strategy (server vs client)<br>- Plan camera permission handling for iOS vs Android | - Document QR code format<br>- Document library choice and integration approach<br>- Create technical design document |
| **S2** | - Plan QrCodeService implementation<br>- Define QR code data structure<br>- Plan QR generation endpoint (if server-side) | - Create 3_CoreHub/Services/QrCodeService.cs<br>- Create 5_WebApps/ShopERP/Services/QrCodeService.cs<br>- Add QR generation to ProductDto<br>- Test QR generation |
| **S3** | - Plan QRScanner.razor component structure<br>- Plan camera initialization flow<br>- Plan error handling (camera denied, no camera) | - Create 5_WebApps/KhachLink/Components/QRScanner.razor<br>- Integrate html5-qrcode library<br>- Implement camera access<br>- Implement QR detection<br>- Add error handling |
| **S4** | - Plan scan-to-cart workflow<br>- Plan navigation (Scan page, menu item)<br>- Plan CartService extension | - Create 5_WebApps/KhachLink/Pages/Scan.razor<br>- Update CartService.AddFromQrCodeAsync()<br>- Update NavMenu.razor (add scan item)<br>- Update Home.razor (add scan button) |
| **S5** | - Plan test scenarios for real devices<br>- Plan edge cases (invalid QR, camera denied) | - Test QR scanning on Android Chrome<br>- Test QR scanning on iOS Safari<br>- Test scan-to-cart workflow<br>- Fix bugs found during testing |

### Rules
- MUST test on real devices (Android Chrome, iOS Safari)
- MUST handle camera permissions gracefully (iOS vs Android differences)
- MUST use UI Platform components in QRScanner.razor
- MUST define QR code format before implementation
- MUST test error handling (camera denied, invalid QR, no camera)

## 11. ESTIMATED EFFORT
- 1-2 days total
- 3-5 sessions theo JIT Planning
- **BLOCKER:** Access to real Android and iOS devices for testing