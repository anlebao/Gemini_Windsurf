# VẠN AN HTX-IN-A-BOX
# BUSINESS OPERATING MODEL v1.1 — PROOF-GATED (bản chờ kiểm chứng pilot)

**Phiên bản:** 1.1 — **Ngày:** 2026-10-27
**Nền:** `BOM_HTX_in_a_Box_v1.0.md` (khung kiến trúc giữ nguyên)
**Pháp lý:** Luật HTX số 17/2023/QH15 (hiệu lực 01/7/2024) · Nghị định 113/2024/NĐ-CP (hiệu lực 01/11/2024)
**Quyết định phê duyệt:** KHÔNG mở cổng nhân bản diện rộng khi chưa kiểm chứng tại 01 HTX thực tế đạt đồng thời 3 trụ chứng minh P1/P2/P3. BOM v1.1 là khung đo lường + gating cho pilot đó.

---

## 1. Core Pivot — Một câu

Vạn An **không bán phần mềm cho HTX**; Vạn An **kiến tạo cộng đồng kinh tế địa phương**, trong đó HTX là tổ chức kinh tế **độc lập của thành viên**, và phần mềm là hạ tầng tạo ra giao dịch có lợi ích thực tế. Bản v1.1 bổ sung điều kiện: **mọi tuyên bố lợi ích phải đo được, mọi cổng mở rộng phải có ngưỡng chứng minh**.

## 2. Operating Model at a Glance (3 lớp — giữ v1.0)

| Lớp | Chủ thể | Ranh giới |
|---|---|---|
| 1 – Cộng đồng | Tenant, khách, CTV, shipper, nhà cung cấp | Không mặc định là thành viên HTX |
| 2 – HTX | Thành viên (≥ 05 thành viên chính thức – Điều 4.7) | Quản trị độc lập; tài sản chung được bảo vệ |
| 3 – Vạn An | Vạn An Ecommerce / Vạn An Tech | Hạ tầng + kiến tạo theo hợp đồng; không điều hành thay HTX |

Nấc trung gian: **tổ hợp tác** (≥ 02 thành viên, Điều 4.21) → **liên hiệp HTX** (≥ 03 HTX, Điều 4.8).

## 3. Quan hệ kinh tế (giữ v1.0 + chi tiết giao dịch nội bộ theo Điều 20 ND 113/2024)

| Mã | Quan hệ | Nội dung |
|---|---|---|
| R1 | Tenant ↔ Khách | Bán hàng, đặt lịch, giao nhận (đã có) |
| R2 | Tenant ↔ Tenant | Bán chéo, giới thiệu, mua chung |
| R3 | HTX ↔ Thành viên | **Giao dịch nội bộ** (Điều 4.1 Luật; Điều 20 ND 113) |
| R4 | Vạn An ↔ HTX/Tenant | Phần mềm + dịch vụ kiến tạo theo hợp đồng |
| R5 | HTX ↔ HTX | Hỗ trợ, nguồn cung, đào tạo; chính thức hóa bằng liên hiệp HTX |

**5 hoạt động giao dịch nội bộ (Điều 20 ND 113/2024)** — ánh xạ vào các chương trình HTX-in-a-Box:

| # | Hoạt động (Điều 20) | Chương trình tương ứng |
|---|---|---|
| a | Sản xuất/mua ngoài sản phẩm, vật tư đầu vào cung ứng cho thành viên | Mua chung, gom nhu cầu |
| b | Trực tiếp cung cấp hoặc thuê bên khác cung cấp dịch vụ phục vụ sản xuất/đời sống | Dịch vụ chung: kế toán, marketing, công nghệ |
| c | Lưu trữ, phân loại, bảo quản, chế biến, tiêu thụ sản phẩm của thành viên | Gom sản phẩm, tiêu thụ tập trung |
| d | Bảo trợ hợp đồng giữa thành viên và đơn vị cung cấp/thu mua (được trả phí bảo trợ) | Kết nối nhà cung cấp, bán buôn |
| e | Cho vay nội bộ (chỉ khi đủ điều kiện Điều 19 ND 113) | Không triển khai ở giai đoạn pilot |

**Thu nhập giao dịch nội bộ** = doanh thu giao dịch nội bộ − chi phí trực tiếp liên quan (Điều 20.2) → căn cứ ưu đãi thuế (Điều 22, 86 Luật HTX).

## 4. Vòng đời tham gia (giữ v1.0)

```
Lead → Profile Created → Activated → Transacting → Collaborating
      → [Tổ hợp tác] → HTX Member → [Liên hiệp HTX] → Inactive
```

## 5. Money Flow (giữ v1.0)

```
Khách ──tiền hàng──▶ Tenant          (doanh thu thành viên)
Tenant/khách ──phí dịch vụ──▶ HTX     (doanh thu HTX — giao dịch HTX thực hiện hợp pháp)
HTX/Tenant ──phí phần mềm──▶ Vạn An   (doanh thu Vạn An)
```

Bất biến: không "ví trung gian" · không thu phí dưới tên HTX khi chủ thể thực tế là doanh nghiệp khác · GMV ≠ doanh thu của bất kỳ pháp nhân nào.

---

# PHẦN MỚI v1.1 — BA TRỤ CHỨNG MINH (PROOF PILLARS)

Mỗi trụ có 5 thành phần: **Định nghĩa** · **Cơ chế đo** · **Ngưỡng chứng minh** (đề xuất, chốt trong pilot protocol) · **Công cụ hệ thống** · **Dữ liệu chứng minh**.

## 6. Trụ P1 — Thành viên nhận được giá trị thực

**Định nghĩa.** Giá trị ròng của thành viên (VN/T) = (lợi ích tăng thêm đo được) − (phí thành viên + chi phí tham gia). Lợi ích tăng thêm gồm 4 kênh:

1. **Giao dịch nội bộ** (Điều 20 ND 113): tiết kiệm giá đầu vào (mua chung), phí dịch vụ thấp hơn thị trường, giá trị tiêu thụ sản phẩm — cộng hưởng ưu đãi thuế của HTX (Điều 22, 86 Luật HTX).
2. **Bán chéo (R2)**: doanh thu tăng thêm có nguồn giới thiệu được xác minh.
3. **Marketing/dịch vụ chung**: chi phí marketing, kế toán, công nghệ giảm so với mua riêng.
4. **Chính sách Nhà nước (Điều 5–17 ND 113)**: hỗ trợ đào tạo, thông tin, tư vấn, KHCN, thị trường, hạ tầng mà thành viên tiếp cận được qua HTX.

**Cơ chế đo.**
- Đo baseline T0 (trước khi tham gia chương trình) cho từng thành viên: giá vốn đầu vào, doanh thu, chi phí marketing/kế toán, chi phí thuê ngoài.
- Đo lại tại T+3, T+6, T+12 tháng.
- Phát hành **Member Value Statement (MVS)** mỗi quý cho từng thành viên: giá trị từng kênh, tổng giá trị ròng, so với phí đã đóng.

**Ngưỡng chứng minh P1 (đề xuất — chốt trong pilot protocol).**
- ≥ 70% thành viên có giá trị ròng dương trong 2 kỳ đo liên tiếp.
- Giá trị ròng trung bình ≥ 1,5 × phí thành viên/năm.
- Mỗi kênh lợi ích đều có dữ liệu đối soát, không chỉ báo cáo định tính.

**Công cụ hệ thống.** Phân hệ đối soát: mã nguồn khách (R2), sổ giao dịch nội bộ (R3), hóa đơn mua chung, MVS tự sinh.

**Dữ liệu chứng minh.** MVS đã ký bởi thành viên; biên bản xác nhận lợi ích; 2 kỳ đối soát liên tiếp.

## 7. Trụ P2 — HTX tự trang trải được chi phí

**Định nghĩa.** HTX vận hành bền vững khi thu nhập hợp pháp của HTX (không tính hỗ trợ Nhà nước) đủ trang trải chi phí vận hành thực tế trong thời gian dài; hỗ trợ Nhà nước chỉ là bổ sung, không phải nguồn chính.

**Cơ cấu chi phí HTX tối thiểu (kế hoạch trước khi thành lập):** thù lao quản trị · kế toán/thuế · phí phần mềm Vạn An · chi phí vận hành chương trình · chi phí thuê địa điểm/pháp lý · dự phòng quỹ chung không chia.

**Nguồn thu HTX:** phí dịch vụ thành viên · thu nhập giao dịch nội bộ (Điều 20) · phí bảo trợ hợp đồng (Điều 20d) · doanh thu giao dịch HTX thực hiện hợp pháp · chính sách Nhà nước (Điều 6–17 ND 113 — xem là nguồn bổ sung).

**Cơ chế đo.**
- **Tỷ lệ tự trang trải (self-sufficiency) = Thu HTX (không gồm hỗ trợ Nhà nước) / Chi phí vận hành thực tế** — chỉ số chính.
- **Tỷ lệ bền vững = Thu HTX (gồm hỗ trợ Nhà nước) / Chi phí vận hành** — chỉ số phụ.
- Đo theo quý, báo cáo tại Đại hội thành viên.

**Ngưỡng chứng minh P2 (đề xuất).**
- Tự trang trải ≥ 70% tại tháng 12 kể từ thành lập.
- Tự trang trải ≥ 90% tại tháng 24.
- Đạt hòa vốn (≥ 100%) trong 36 tháng — điều kiện để mở cổng nhân bản.
- Nếu 2 quý liên tiếp tự trang trải < 50% → kích hoạt quy trình tái cấu trúc, không mở rộng thêm chương trình.

**Công cụ hệ thống.** Mô hình chi phí HTX (bảng kế toán riêng cho HTX, tách khỏi sổ của thành viên và của Vạn An); dashboard tỷ lệ tự trang trải; cảnh báo ngưỡng.

**Dữ liệu chứng minh.** Báo cáo tài chính HTX quý; biên bản Đại hội thành viên phê duyệt chi phí; xác nhận mức hỗ trợ Nhà nước thực nhận (Điều 17 ND 113 — quy trình hỗ trợ).

## 8. Trụ P3 — Tài sản chung được bảo vệ bằng kiểm soát có thể thực thi

**Định nghĩa.** Tài sản chung gồm: **quỹ chung không chia**, **tài sản chung không chia** (Điều 4.13–4.15 Luật HTX), vốn điều lệ và tài sản khác của HTX. Bảo vệ tài sản chung = ngăn chặn chi sai, chi không phép, sử dụng cá nhân hóa; và xử lý đúng pháp luật khi HTX chấm dứt.

**Kiểm soát 3 lớp.**

| Lớp | Cơ chế | Yêu cầu thực thi |
|---|---|---|
| **Pháp lý** | Điều lệ + nghị quyết Đại hội thành viên | Tỷ lệ trích lập quỹ chung không chia do Đại hội quyết định; mọi khoản chi quỹ phải có nghị quyết/ủy quyền; quỹ chung không chia **không được chia** trong quá trình hoạt động |
| **Tài chính** | Sổ sách, 2 người ký, ngân sách, hạn mức ủy quyền, kiểm toán | Kế toán độc lập; kiểm toán độc lập định kỳ (HTX siêu nhỏ/nhỏ được hỗ trợ chi phí — Điều 15 ND 113); đối chiếu ngân hàng định kỳ; phương án xử lý tài sản khi giải thể/phá sản phải rà soát toàn bộ vốn, quỹ, tài sản gồm quỹ chung không chia (Điều 21 ND 113) |
| **Hệ thống** | Phân quyền theo vai trò, audit log, chống xóa/sửa, cảnh báo | Không cá nhân nào có toàn quyền; mọi giao dịch quỹ có nhật ký + người phê duyệt; phát hiện chi bất thường → cảnh báo → dừng |

**Rủi ro đặc thù cần quy chế riêng:**
- **Cho vay nội bộ (Điều 19 ND 113)**: chỉ khi đủ điều kiện (nguồn vốn tối đa **30% vốn chủ sở hữu** năm trước, Đại hội thành viên thông qua, quy định trong Điều lệ). Pilot KHÔNG triển khai cho vay nội bộ.
- **Góp vốn bằng quyền sử dụng phần mềm (v1.0 §9.5)**: giữ nguyên cảnh báo — không chuyển IP trái chủ trương.

**Ngưỡng chứng minh P3 (đề xuất).**
- 100% giao dịch quỹ chung/chi tiêu HTX có nghị quyết hoặc ủy quyền hợp lệ + audit trail đầy đủ.
- Kiểm toán độc lập 2 năm liên tiếp không có ý kiến ngoại trừ trọng yếu.
- 0 vụ chi quỹ không phép, 0 khiếu nại mất tài sản có căn cứ trong 24 tháng.
- Danh mục tài sản chung (gồm quỹ chung không chia) được rà soát và công bố hằng năm theo Điều 21 ND 113.

**Công cụ hệ thống.** Module HTX Governance: sổ thành viên, sổ nghị quyết, phân quyền chức danh, nhật ký phê duyệt, đối chiếu tài sản, cảnh báo vượt hạn mức.

**Dữ liệu chứng minh.** Biên bản Đại hội thành viên; báo cáo kiểm toán; nhật ký phê duyệt hệ thống; biên bản rà soát tài sản hằng năm.

---

## 9. Gating nhân bản (cập nhật — Điểm khác biệt cốt lõi so với v1.0)

| Gate | Nội dung | Ngưỡng mới (v1.1) |
|---|---|---|
| G1 – Sẵn sàng địa bàn | Nhu cầu + ngành ưu tiên + kế hoạch thử nghiệm | Giữ v1.0 |
| G2 – Sẵn sàng vận hành | Onboarding, phân quyền, hỗ trợ hoạt động | Giữ v1.0 |
| G3 – Chứng minh giao dịch | Chương trình hợp tác thực tế + đối soát | **P1 đạt ngưỡng** trên mẫu ≥ 10 tenant có giao dịch |
| G4 – Sẵn sàng thành lập HTX | Nhóm sáng lập, phương án KD, hồ sơ pháp lý | **+ P2: kế hoạch chi phí 36 tháng với nguồn thu xác định**; quy chế tài chính + quỹ chung không chia hoàn thiện |
| G5 – Sẵn sàng chuyển giao | Hạt nhân kế nhiệm, vận hành tự chủ | **+ P2 ≥ 70% tự trang trải**; **P3 kiểm toán năm 1 không ý kiến ngoại trừ trọng yếu** |
| G6 – Sẵn sàng nhân bản | Mô hình kiểm chứng, tài liệu hóa | **CHỈ mở khi: pilot 01 HTX hoàn tất chu kỳ 24–36 tháng, đạt đồng thời P1 + P2 (≥ 90% hoặc hòa vốn) + P3; bộ tài liệu nhân bản được kiểm chứng bởi người không thuộc đội kiến tạo** |

Không vượt cổng chỉ vì đạt số lượng cửa hàng/member. **Quyết định phê duyệt (2026-10-27): chặn mọi nhân bản quy mô lớn trước khi G6 thỏa mãn.**

## 10. Chương trình kiểm chứng pilot — 01 HTX thực tế

| Hạng mục | Nội dung |
|---|---|
| Mục tiêu | Kiểm chứng P1/P2/P3 trên dữ liệu thật; hiệu chỉnh ngưỡng; tài liệu hóa quy trình tái sử dụng |
| Điều kiện chọn địa bàn | 1 xã/phường; 20–60 tenant tiềm năng; ≥ 1 nhóm ngành có nhu cầu bán chéo rõ; có 5–10 ứng viên thành viên chính thức |
| Thời gian | 24–36 tháng; điểm chốt tại T0, T+3, T+6, T+12, T+24, T+36 |
| Phạm vi KHÔNG triển khai ở pilot | Cho vay nội bộ (Điều 19); nhận góp vốn bằng quyền sử dụng phần mềm; giữ tiền hộ dưới mọi hình thức |
| Dữ liệu bắt buộc | Baseline từng thành viên (T0) · MVS quý · báo cáo tài chính HTX quý · tỷ lệ tự trang trải · nhật ký phê duyệt quỹ · kiểm toán năm |
| Người giám sát độc lập | Người không thuộc đội kiến tạo địa bàn, xác nhận từng ngưỡng trước khi chuyển Gate |
| Đầu ra | Báo cáo kiểm chứng 3 trụ; ngưỡng chốt cuối; bộ tài liệu nhân bản đã hiệu chỉnh |

## 11. Nguồn thu (giữ v1.0 — tóm tắt)

**HTX:** phí dịch vụ thành viên · phí tổ chức hoạt động chung · doanh thu giao dịch hợp pháp · phí logistics/marketing/mua chung khi HTX thực sự cung cấp · quỹ chung không chia (trích theo quyết định Đại hội) · hỗ trợ Nhà nước (Điều 6–17 ND 113).
**Vạn An:** phí thuê bao · triển khai · chức năng bổ sung · hỗ trợ/đào tạo · cấp phép công nghệ. Không lấy nguồn từ quỹ chung không chia hoặc ưu đãi thuế của HTX.

## 12. Unit Economics (v1.0 + HTX break-even)

| Đơn vị | Chi phí | Doanh thu | Ngưỡng |
|---|---|---|---|
| 1 tenant hoạt động | Kiến tạo | Phí thuê bao | Chi phí tạo tenant giảm dần theo địa bàn |
| 1 chương trình bán chéo | Thử nghiệm | Lợi ích tăng thêm (công thức §19 tài liệu gốc) | Lợi ích dương sau đối soát |
| 1 HTX | Quản trị + kế toán + phần mềm + vận hành + quỹ chung không chia | Phí dịch vụ + giao dịch nội bộ + bảo trợ + hỗ trợ NN | Tự trang trải ≥ 70% (T12) → ≥ 90% (T24) → hòa vốn (T36) |

**Break-even HTX (công thức sử dụng ở G4):** Tổng phí thành viên + thu nhập giao dịch nội bộ ≥ Tổng chi phí vận hành hằng năm → xác định số thành viên tối thiểu = Chi phí vận hành năm / (Phí bình quân + Thu nhập nội bộ bình quân/1 thành viên). Nếu số thành viên tối thiểu > 70% số thành viên tiềm năng thực tế → chưa đủ điều kiện thành lập HTX.

## 13. Quyết định mẫu (Decision Records — bổ sung)

| Mã | Quyết định |
|---|---|
| BOM-HT-001 → 008 | Giữ nguyên v1.0 |
| **BOM-HT-101** | Nhân bản diện rộng bị chặn cho đến khi pilot 01 HTX đạt P1+P2+P3 (quyết định 2026-10-27) |
| **BOM-HT-102** | Pilot không triển khai cho vay nội bộ (Điều 19 ND 113); đánh giá lại sau pilot |
| **BOM-HT-103** | MVS quý là sản phẩm bắt buộc của hệ thống; thành viên xác nhận trước khi ghi nhận lợi ích |
| **BOM-HT-104** | Mọi chi quỹ chung không chia cần nghị quyết + 2 chữ ký + audit trail hệ thống; vi phạm = xử lý trách nhiệm cá nhân |
| **BOM-HT-105** | Giám sát độc lập (ngoài đội kiến tạo) xác nhận từng ngưỡng trước khi chuyển Gate |
| **BOM-HT-106** | Tự trang trải < 50% trong 2 quý liên tiếp → tái cấu trúc, dừng mở rộng chương trình |

## 14. Critical Invariants (v1.0 + bổ sung)

1–8: giữ nguyên v1.0 (HTX độc lập; ≥ 05 thành viên chính thức; quỹ chung không chia không chia trong quá trình hoạt động; ranh giới dữ liệu; mã nguồn khách; doanh thu 3 lớp; không cam kết thời gian giao hàng chưa kiểm chứng; CTV ≠ thành viên/quan hệ lao động).
9. **Tài sản chung (quỹ + tài sản chung không chia) phải có danh mục, nghị quyết, 2 chữ ký, audit trail — không có ngoại lệ.**
10. **Chỉ số lợi ích thành viên phải có dữ liệu đối soát, không chấp nhận báo cáo định tính làm bằng chứng.**
11. **Hỗ trợ Nhà nước là nguồn bổ sung, không tính vào tỷ lệ tự trang trải.**
12. **Chu kỳ kiểm chứng 24–36 tháng phải hoàn tất trước G6; không rút ngắn bằng hồ sơ giấy.**

## 15. KPI & Gates

KPI: giữ Nhóm A–D v1.0 + bổ sung: **tỷ lệ tự trang trải** · **tỷ lệ thành viên giá trị ròng dương** · **giá trị ròng trung bình/phí** · **số vụ chi quỹ không phép (mục tiêu 0)** · **tỷ lệ giao dịch nội bộ** (bắt buộc ghi Điều lệ). Gates: xem §9.

## 16. Roadmap (cập nhật — gating theo chứng minh)

```
Pilot 01 HTX (24–36 tháng, chứng minh P1/P2/P3)          ← CHỐT: đang ở đây
   → Kiểm chứng 03 HTX (lặp lại quy trình, ngưỡng chốt)
   → Chuẩn hóa 10 HTX
   → Nhân bản có kiểm soát 30–100 HTX (chỉ khi G6 mở)
   → Mở rộng hệ thống 300–1.000 → Dài hạn 3.000
```

Mốc mục tiêu, không phải cam kết thời gian; mỗi nấc phải qua đủ Gate kèm ngưỡng.

## 17. Rủi ro pháp lý (v1.0 + ND 113)

| Rủi ro | Kiểm soát | Nguồn |
|---|---|---|
| Mất ưu đãi thuế do không tách giao dịch nội bộ | Ghi nhận riêng nội bộ/bên ngoài; tỷ lệ nội bộ trong Điều lệ | Điều 20, 86 |
| Cho vay nội bộ vượt giới hạn, rủi ro mất vốn | Không triển khai ở pilot; khi triển khai: ≤ 30% vốn CSH, Đại hội thông qua | Điều 19 ND 113 |
| Chi quỹ chung không chia trái quy định | Nghị quyết + 2 chữ ký + audit trail + kiểm toán | Điều 4.13; Điều 21 ND 113 |
| Xử lý tài sản khi giải thể/phá sản sai | Rà soát toàn bộ vốn/quỹ/tài sản theo quy định; công bố danh mục hằng năm | Điều 21 ND 113 |
| Tranh chấp hoa hồng/giới thiệu | Mã nguồn khách + nhật ký + đối soát chu kỳ | v1.0 |
| Vi phạm dữ liệu cá nhân | Đồng ý từng mục đích; ranh giới tenant; hợp đồng xử lý dữ liệu | Nghị định 13/2023 |
| Thỏa thuận giá chung trái luật cạnh tranh | Không ấn định giá bán của doanh nghiệp độc lập | v1.0 |
| Hỗ trợ Nhà nước bị từ chối do hồ sơ/quy mô | Xác định phân loại quy mô (Điều 5 ND 113) từ trước; theo đúng quy trình (Điều 17 ND 113) | ND 113 Chương III |

---

## 18. Câu hỏi mở cần chốt trong pilot protocol

1. Chốt ngưỡng số của P1 (≥ 70% thành viên dương; ≥ 1,5× phí) và P2 (70/90/100%) — đề xuất, cần duyệt.
2. Nguồn phí phần mềm Vạn An trả bởi HTX hay thành viên — ảnh hưởng trực tiếp tỷ lệ tự trang trải của HTX.
3. AI để hệ thống đánh giá: chính sách Nhà nước nào áp dụng cho ngành/địa bàn pilot (Điều 4–17 ND 113).
4. Người giám sát độc lập do ai chỉ định (Liên minh HTX? đơn vị tư vấn? Vạn An nhưng khác đội kiến tạo?).
5. Chu kỳ pilot 24 hay 36 tháng — phụ thuộc mùa vụ ngành nghề địa bàn.