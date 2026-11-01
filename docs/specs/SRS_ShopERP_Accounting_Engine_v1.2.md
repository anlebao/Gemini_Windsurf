# SRS -- SHOPERP ACCOUNTING ENGINE

## Hệ thống chứng từ, hạch toán và đối chiếu kế toán cho doanh nghiệp thương mại

  -----------------------------------------------------------------------
  Thuộc tính                          Giá trị
  ----------------------------------- -----------------------------------
  Tên tài liệu                        Software Requirements Specification
                                      -- ShopERP Accounting Engine

  Phiên bản                           1.2 -- UX-Preserving Backend-First SRS

  Ngày                                30/10/2026

  Phạm vi                             Doanh nghiệp thương mại tại Việt
                                      Nam

  Đối tượng sử dụng                   Product Owner, BA, kế toán trưởng,
                                      kế toán viên, kiến trúc sư, đội
                                      phát triển và QA

  Trạng thái                          Đặc tả yêu cầu; chưa phải xác nhận
                                      tuân thủ pháp luật hoặc thiết kế
                                      triển khai cuối cùng
  -----------------------------------------------------------------------

------------------------------------------------------------------------

# 1. Mục đích tài liệu

Tài liệu này xác định yêu cầu nghiệp vụ và yêu cầu phần mềm cho
Accounting Engine của ShopERP. Mục tiêu là tạo một lõi kế toán thống
nhất, có khả năng tiếp nhận sự kiện từ các phân hệ bán hàng, mua hàng,
kho, ngân hàng, tiền mặt và các nghiệp vụ quản trị khác; áp dụng quy tắc
hạch toán; tạo bút toán; cập nhật sổ chi tiết và sổ cái; đồng thời hỗ
trợ đối chiếu và truy vết.

Tài liệu được dùng để:

1.  Rà soát mức độ đáp ứng của base code hiện tại.
2.  Xác định chức năng đã có, còn thiếu, làm chưa đúng hoặc chưa thể xác
    minh.
3.  Làm căn cứ thiết kế, phát triển, kiểm thử và nghiệm thu.
4.  Hạn chế viết lại những module đang hoạt động đúng.
5.  Bảo đảm các phân hệ không tự ghi nhận trùng một nghiệp vụ kinh tế.

**Nguyên tắc cốt lõi:** Một nghiệp vụ kinh tế chỉ được ghi nhận kế toán
theo đúng bản chất và đúng thời điểm; các chứng từ thu, chi, mua, bán,
kho và ngân hàng phải liên kết được với nhau nhưng không được đồng nhất
thành một sự kiện duy nhất.

# 2. Phạm vi sản phẩm

## 2.1. Trong phạm vi

Accounting Engine phải hỗ trợ các nhóm nghiệp vụ sau:

-   Thu tiền mặt và thu qua ngân hàng.
-   Chi tiền mặt và chi qua ngân hàng.
-   Chuyển tiền nội bộ giữa quỹ và tài khoản ngân hàng.
-   Bán hàng, doanh thu, thuế liên quan và công nợ phải thu.
-   Mua hàng, hàng tồn kho, thuế liên quan và công nợ phải trả.
-   Nhập kho, xuất kho, trả hàng và giá vốn.
-   Tạm ứng, hoàn ứng và các khoản phải thu/phải trả khác.
-   Vốn góp, khoản vay, ký quỹ/ký cược và các khoản thu/chi có bản chất
    tương ứng.
-   Bút toán điều chỉnh, phân bổ, khấu hao, dự phòng và khóa sổ.
-   Sổ quỹ, sổ ngân hàng, sổ công nợ, sổ kho, sổ cái và báo cáo tài
    chính.
-   Đối chiếu giữa sổ chi tiết và sổ cái.
-   Nhật ký kiểm soát, phân quyền và truy vết.

## 2.2. Ngoài phạm vi của phiên bản nền tảng

Các nội dung dưới đây chỉ được tích hợp qua giao diện hoặc sự kiện,
không mặc nhiên thuộc phạm vi triển khai đầy đủ của phiên bản đầu:

-   Kê khai và nộp hồ sơ thuế điện tử trực tiếp.
-   Kết nối trực tiếp mọi ngân hàng hoặc nhà cung cấp dịch vụ thanh
    toán.
-   Hóa đơn điện tử và truyền nhận dữ liệu với nhà cung cấp hóa đơn, trừ
    khi có SRS riêng.
-   Kế toán hợp nhất nhiều pháp nhân.
-   Kế toán quản trị chuyên sâu theo ngân sách, dự án hoặc giá thành sản
    xuất.
-   Tính lương và quản trị nhân sự toàn diện.
-   Kế toán sản xuất, xây dựng, chủ đầu tư và các ngành đặc thù.

Các module ngoài phạm vi vẫn phải có khả năng tích hợp mà không làm sai
lệch dữ liệu kế toán.

# 3. Cơ sở và nguyên tắc kế toán

## 3.1. Cơ sở áp dụng

Hệ thống hướng tới doanh nghiệp Việt Nam và phải cho phép cấu hình theo
chế độ kế toán mà doanh nghiệp đủ điều kiện áp dụng. Đối với doanh
nghiệp áp dụng chế độ kế toán theo Thông tư 99/2025/TT-BTC, hệ thống
phải hỗ trợ các yêu cầu tương ứng có hiệu lực từ kỳ kế toán thuộc phạm
vi áp dụng.

Việc xác định chế độ kế toán, hệ thống tài khoản, biểu mẫu chứng từ,
thuế và chính sách kế toán cụ thể phải được kế toán trưởng hoặc chuyên
gia có thẩm quyền xác nhận trước khi đưa vào vận hành. Không được coi
các bút toán minh họa trong tài liệu này là quy tắc pháp lý bất biến.

## 3.2. Nguyên tắc thiết kế

1.  **Bản chất kinh tế:** Hạch toán theo bản chất giao dịch, không chỉ
    dựa trên tên phiếu.
2.  **Tiền không đồng nghĩa doanh thu:** Nhận tiền ứng trước hoặc thu
    hồi công nợ không tự động tạo doanh thu.
3.  **Một nguồn -- một lần ghi sổ:** Một sự kiện nguồn không được tạo
    bút toán trùng.
4.  **Tách chứng từ và bút toán:** Chứng từ nghiệp vụ là căn cứ; Journal
    Entry là kết quả hạch toán.
5.  **Tách kế toán và thuế:** Không suy diễn nghĩa vụ thuế chỉ từ tài
    khoản kế toán hoặc ngược lại.
6.  **Có thể truy vết:** Mọi bút toán phải truy ngược được tới chứng từ
    và sự kiện nguồn.
7.  **Không sửa âm thầm:** Chứng từ đã ghi sổ phải được điều chỉnh/đảo
    theo quy trình, giữ nguyên lịch sử.
8.  **Cấu hình thay vì hard-code:** Tài khoản và quy tắc hạch toán phải
    có khả năng cấu hình theo doanh nghiệp và thời gian hiệu lực.
9.  **Đối chiếu được:** Sổ chi tiết tiền, kho, công nợ phải đối chiếu
    được với sổ cái.
10. **Phù hợp quy mô nhiều tenant:** Dữ liệu, cấu hình và phân quyền
    phải cô lập theo từng doanh nghiệp.

# 4. Thuật ngữ và định nghĩa

  -----------------------------------------------------------------------
  Thuật ngữ                           Định nghĩa
  ----------------------------------- -----------------------------------
  Business Event                      Sự kiện kinh tế phát sinh từ một
                                      phân hệ hoặc tích hợp bên ngoài

  Source Document                     Chứng từ nguồn ghi nhận nghiệp vụ
                                      ban đầu

  Voucher                             Chứng từ nghiệp vụ được quản lý
                                      trong hệ thống

  Voucher Type                        Mã/nhóm loại chứng từ, ví dụ thu
                                      tiền, chi tiền, nhập hàng

  Accounting Rule                     Quy tắc sinh bút toán từ sự
                                      kiện/chứng từ

  Journal Entry (JE)                  Bút toán kế toán được ghi nhận vào
                                      nhật ký chung/sổ cái

  Journal Entry Line                  Một dòng Nợ hoặc Có trong bút toán

  Subledger                           Sổ chi tiết như tiền, công nợ, hàng
                                      tồn kho

  Posting                             Thao tác ghi sổ chính thức

  Reversal                            Bút toán đảo để xử lý chứng từ đã
                                      ghi sổ

  Reconciliation                      Đối chiếu số liệu giữa các phân hệ
                                      hoặc giữa sổ chi tiết và sổ cái

  Tenant                              Một doanh nghiệp/pháp nhân sử dụng
                                      ShopERP

  Idempotency                         Cơ chế bảo đảm xử lý lặp cùng một
                                      yêu cầu/sự kiện không tạo kết quả
                                      trùng
  -----------------------------------------------------------------------

# 5. Vai trò người dùng và phân quyền

  -----------------------------------------------------------------------
  Vai trò                             Quyền điển hình
  ----------------------------------- -----------------------------------
  Chủ doanh nghiệp/Quản trị tenant    Xem báo cáo, cấu hình trong phạm vi
                                      được cấp, quản lý người dùng

  Kế toán trưởng                      Quản lý chính sách kế toán, duyệt
                                      chứng từ, mở/khóa kỳ theo thẩm
                                      quyền

  Kế toán tổng hợp                    Kiểm tra, ghi sổ, điều chỉnh, đối
                                      chiếu và lập báo cáo

  Kế toán thanh toán                  Lập phiếu thu/chi, thanh toán, đối
                                      chiếu tiền

  Kế toán bán hàng/mua hàng           Xử lý chứng từ bán/mua và công nợ
                                      trong phạm vi

  Thủ quỹ                             Ghi nhận thu/chi tiền mặt, xem sổ
                                      quỹ được phân quyền

  Thủ kho                             Ghi nhận nhập/xuất kho; không tự ý
                                      sửa bút toán sổ cái

  Nhân viên bán hàng                  Tạo đơn hàng và xem thông tin được
                                      cấp; không có quyền ghi sổ kế toán

  Kiểm toán/Người xem                 Chỉ xem dữ liệu và nhật ký được cấp
  -----------------------------------------------------------------------

Yêu cầu phân quyền:

-   Hỗ trợ phân quyền theo tenant, chi nhánh, kho, quỹ, tài khoản ngân
    hàng, chức năng và thao tác.
-   Có thể tách người lập, người duyệt và người ghi sổ.
-   Người dùng không được tự duyệt chứng từ của mình nếu chính sách kiểm
    soát nội bộ yêu cầu tách nhiệm vụ.
-   Quyền mở kỳ, sửa cấu hình tài khoản và sửa Accounting Rule phải là
    quyền đặc biệt.
-   Mọi thay đổi phân quyền phải được ghi Audit Log.

# 6. Kiến trúc nghiệp vụ tổng thể

## 6.1. Luồng chuẩn

1.  Phân hệ nghiệp vụ phát sinh Business Event.
2.  Hệ thống tạo hoặc cập nhật chứng từ nguồn.
3.  Hệ thống kiểm tra dữ liệu, trạng thái, kỳ kế toán, quyền và liên
    kết.
4.  Accounting Engine xác định Accounting Rule có hiệu lực.
5.  Hệ thống tạo Journal Entry ở trạng thái nháp/chờ duyệt hoặc ghi sổ
    theo chính sách.
6.  Khi đủ điều kiện, người có quyền thực hiện Posting.
7.  Hệ thống cập nhật sổ cái và sổ chi tiết trong cùng một giao dịch dữ
    liệu hoặc cơ chế bảo đảm nhất quán tương đương.
8.  Hệ thống phát sự kiện đã ghi sổ để các module báo cáo/đối chiếu cập
    nhật.
9.  Người dùng có thể truy ngược từ báo cáo đến JE, chứng từ và sự kiện
    nguồn.

## 6.2. Phân định trách nhiệm phân hệ

  -----------------------------------------------------------------------
  Phân hệ                 Trách nhiệm             Không được tự ý
  ----------------------- ----------------------- -----------------------
  Sales                   Ghi nhận đơn hàng, giao Tự tạo bút toán trùng
                          hàng, trả hàng, sự kiện với Accounting Engine
                          bán                     

  Purchase                Ghi nhận đơn mua, nhận  Tự ghi sổ cái ngoài cơ
                          hàng, hóa đơn mua, trả  chế chung
                          hàng                    

  Inventory               Ghi nhận biến động số   Ghi nhận doanh thu hoặc
                          lượng và giá trị kho    thu tiền
                          theo chính sách         

  Cash/Bank               Ghi nhận dòng tiền thực Tự suy diễn doanh thu
                          tế, sao kê, đối soát    từ mọi khoản tiền vào

  Accounting Engine       Áp dụng quy tắc, tạo    Tạo sự kiện kinh doanh
                          JE, ghi sổ và kiểm soát giả để hợp thức hóa bút
                                                  toán

  Reporting               Đọc dữ liệu đã ghi sổ   Sửa trực tiếp dữ liệu
                          và dữ liệu chi tiết     nguồn hoặc JE đã ghi sổ
  -----------------------------------------------------------------------

# 7. Yêu cầu chức năng

Mức ưu tiên: - **P0:** Bắt buộc để bảo đảm tính đúng đắn của kế toán. -
**P1:** Cần có trong phiên bản triển khai nghiệp vụ thương mại đầu
tiên. - **P2:** Có thể triển khai ở giai đoạn mở rộng.

## 7.1. Quản lý hệ thống tài khoản

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-COA-001              Hệ thống phải cung cấp  P0
                          danh mục tài khoản kế   
                          toán có thể cấu hình    
                          theo chế độ kế toán     
                          doanh nghiệp áp dụng.   

  FR-COA-002              Mỗi tenant phải có bộ   P0
                          tài khoản độc lập hoặc  
                          cấu hình độc lập, không 
                          dùng chung dữ liệu phát 
                          sinh giữa tenant.       

  FR-COA-003              Tài khoản phải có mã,   P0
                          tên, loại, tính chất dư 
                          Nợ/Có, trạng thái và    
                          thuộc tính theo dõi chi 
                          tiết khi cần.           

  FR-COA-004              Hỗ trợ tài khoản tổng   P1
                          hợp và tài khoản chi    
                          tiết theo cấu hình hợp  
                          lệ.                     

  FR-COA-005              Không cho phép xóa tài  P0
                          khoản đã phát sinh; chỉ 
                          cho phép ngừng sử dụng  
                          hoặc thay thế theo quy  
                          trình.                  

  FR-COA-006              Hỗ trợ ngày hiệu lực và P1
                          lịch sử thay đổi cấu    
                          hình tài khoản.         
  -----------------------------------------------------------------------

## 7.2. Quản lý loại chứng từ

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-VCH-001              Hệ thống phải quản lý   P0
                          Voucher Type bằng mã    
                          nghiệp vụ có thể cấu    
                          hình.                   

  FR-VCH-002              Một biểu mẫu/chức năng  P0
                          chứng từ có thể phục vụ 
                          nhiều nghiệp vụ khác    
                          nhau thông qua mã       
                          nghiệp vụ và Accounting 
                          Rule.                   

  FR-VCH-003              Phân biệt loại nghiệp   P0
                          vụ, phương thức thanh   
                          toán, đối tượng, chứng  
                          từ nguồn và tài khoản   
                          tiền.                   

  FR-VCH-004              Hỗ trợ số chứng từ duy  P1
                          nhất theo tenant, loại  
                          chứng từ và quy tắc     
                          đánh số.                

  FR-VCH-005              Chứng từ phải có trạng  P0
                          thái: Nháp, Chờ duyệt,  
                          Đã duyệt, Đã ghi sổ, Đã 
                          đảo/Hủy theo quy trình. 

  FR-VCH-006              Chứng từ phải lưu người P0
                          tạo, người duyệt, người 
                          ghi sổ và thời điểm     
                          tương ứng.              

  FR-VCH-007              Cho phép đính kèm tài   P1
                          liệu/chứng từ hỗ trợ và 
                          liên kết nhiều chứng từ 
                          nguồn khi nghiệp vụ yêu 
                          cầu.                    
  -----------------------------------------------------------------------

## 7.3. Nghiệp vụ thu tiền

ShopERP hiện có biểu mẫu Phiếu thu. **Giữ nguyên biểu mẫu và luồng thao tác hiện tại.**
Không mặc định thêm dropdown bắt buộc "Loại nghiệp vụ thu", thêm màn hình hoặc bắt
người dùng nhập lại thông tin đã có ở đơn hàng/chứng từ nguồn. Accounting Engine
phải ưu tiên suy luận loại nghiệp vụ từ nguồn phát sinh, liên kết, đối tượng, phương
thức thanh toán và dữ liệu đã lưu. Chỉ yêu cầu người dùng phân loại khi không thể
suy luận an toàn; mọi thay đổi UX phải được Product Owner phê duyệt trước.

Một Phiếu thu có thể tạo các phân bổ và bút toán phù hợp với bản chất của từng khoản.
Không đồng nhất việc nhận tiền với ghi nhận doanh thu.

  -----------------------------------------------------------------------
  Mã nghiệp vụ            Mô tả                   Định khoản tham khảo,
                                                  không hard-code
  ----------------------- ----------------------- -----------------------
  THU01                   Thu tiền bán hàng       Nợ 111/112; Có doanh
                                                  thu và thuế phải nộp
                                                  khi đủ điều kiện ghi
                                                  nhận

  THU02                   Thu công nợ khách hàng  Nợ 111/112; Có 131

  THU03                   Thu tiền khách hàng ứng Nợ 111/112; Có 131 theo
                          trước                   dõi chi tiết khách hàng

  THU04                   Thu lãi tiền gửi/thu    Nợ 112/111; Có tài
                          tài chính               khoản thu nhập tài
                                                  chính phù hợp

  THU05                   Thu nhập khác/bồi       Nợ 111/112; Có tài
                          thường                  khoản thu nhập khác phù
                                                  hợp

  THU06                   Nhân viên hoàn ứng      Nợ 111/112; Có 141 hoặc
                                                  tài khoản liên quan

  THU07                   Thu hồi khoản phải thu  Nợ 111/112; Có tài
                          khác                    khoản phải thu tương
                                                  ứng

  THU08                   Nhận vốn góp            Nợ 111/112; Có tài
                                                  khoản vốn chủ sở hữu
                                                  phù hợp

  THU09                   Nhận tiền vay           Nợ 111/112; Có tài
                                                  khoản vay/nợ phù hợp

  THU10                   Nhận ký quỹ/ký cược     Nợ 111/112; Có tài
                                                  khoản phải trả ký
                                                  quỹ/ký cược phù hợp

  THU11                   Nhận tiền chưa xác định Ghi nhận theo quy trình
                          nội dung                chờ xác minh; không tự
                                                  động coi là doanh thu

  THU12                   Thu tiền thanh lý tài   Hạch toán khoản thu và
                          sản                     thuế liên quan; đồng
                                                  thời liên kết quy trình
                                                  ghi giảm tài sản
  -----------------------------------------------------------------------

Yêu cầu chức năng:

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-REC-001              Cho phép lập chứng từ   P0
                          thu tiền mặt hoặc thu   
                          qua ngân hàng từ một    
                          giao diện nghiệp vụ     
                          thống nhất.             

  FR-REC-002              Engine phải xác định    P0
                          loại nghiệp vụ từ dữ liệu
                          nguồn hiện có; chỉ yêu
                          cầu người dùng bổ sung
                          khi không thể suy luận
                          an toàn và đã được duyệt UX.                    

  FR-REC-003              Phương thức thu gồm     P1
                          tiền mặt, chuyển khoản, 
                          QR/thẻ hoặc phương thức 
                          khác theo cấu hình.     

  FR-REC-004              Thu công nợ phải hỗ trợ P0
                          chọn một hoặc nhiều hóa 
                          đơn/khoản phải thu và   
                          nhập số tiền phân bổ.   

  FR-REC-005              Hỗ trợ thu một phần,    P0
                          thu nhiều lần, thu gộp  
                          nhiều hóa đơn và thu    
                          thừa.                   

  FR-REC-006              Thu ứng trước phải được P0
                          theo dõi theo khách     
                          hàng/hợp đồng/đơn hàng  
                          nếu có; không tự ghi    
                          nhận doanh thu.         

  FR-REC-007              Thu tiền bán hàng phải  P0
                          liên kết với đơn        
                          hàng/hóa đơn bán và     
                          ngăn ghi nhận doanh thu 
                          lần thứ hai.            

  FR-REC-008              Thu chưa rõ nội dung    P1
                          phải có trạng thái chờ  
                          phân loại, người chịu   
                          trách nhiệm và thời hạn 
                          xử lý.                  

  FR-REC-009              Hệ thống phải kiểm tra  P0
                          tài khoản tiền, ngày    
                          thu, số tiền, đối tượng 
                          và kỳ kế toán.          

  FR-REC-010              Chứng từ thu đã ghi sổ  P0
                          không được sửa/xóa trực 
                          tiếp.                   
  -----------------------------------------------------------------------

## 7.4. Nghiệp vụ chi tiền

ShopERP hiện có biểu mẫu Phiếu chi. Giữ nguyên biểu mẫu và trải nghiệm hiện tại.
Engine ưu tiên suy luận bản chất khoản chi từ chứng từ nguồn, nhà cung cấp, đơn mua,
chi phí, tài sản, khoản phải trả và dữ liệu đã lưu. Không thêm trường bắt buộc hoặc
luồng nhập liệu mới nếu backend có thể xác định đáng tin cậy. Nếu thiếu dữ liệu thiết
yếu, hệ thống phải báo rõ lý do và chỉ yêu cầu bổ sung tối thiểu.

  -----------------------------------------------------------------------
  Mã nghiệp vụ            Mô tả                   Định khoản tham khảo
  ----------------------- ----------------------- -----------------------
  CHI01                   Chi mua hàng thanh toán Nợ hàng tồn kho/chi
                          ngay                    phí/tài sản và thuế đầu
                                                  vào đủ điều kiện; Có
                                                  111/112

  CHI02                   Thanh toán công nợ nhà  Nợ 331; Có 111/112
                          cung cấp                

  CHI03                   Tạm ứng nhân viên       Nợ 141; Có 111/112

  CHI04                   Thanh toán chi phí quản Nợ chi phí quản lý và
                          lý                      thuế liên quan nếu đủ
                                                  điều kiện; Có 111/112

  CHI05                   Thanh toán chi phí bán  Nợ chi phí bán hàng và
                          hàng                    thuế liên quan nếu đủ
                                                  điều kiện; Có 111/112

  CHI06                   Thanh toán lương        Nợ 334; Có 111/112

  CHI07                   Nộp thuế                Nợ tài khoản thuế phải
                                                  nộp tương ứng; Có
                                                  111/112

  CHI08                   Trả lãi vay             Nợ chi phí tài chính
                                                  hoặc tài khoản phù hợp;
                                                  Có 111/112

  CHI09                   Trả gốc vay             Nợ tài khoản vay/nợ; Có
                                                  111/112

  CHI10                   Hoàn tiền khách hàng    Nợ 131 hoặc tài khoản
                                                  phải trả phù hợp; Có
                                                  111/112

  CHI11                   Chi ký quỹ/ký cược      Nợ tài khoản ký quỹ/ký
                                                  cược phù hợp; Có
                                                  111/112

  CHI12                   Chi mua tài sản cố định Nợ tài sản và thuế liên
                                                  quan nếu đủ điều kiện;
                                                  Có 111/112

  CHI13                   Chi khác                Theo bản chất kinh tế
                                                  và chính sách kế toán
                                                  được duyệt
  -----------------------------------------------------------------------

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-PAY-001              Cho phép lập chứng từ   P0
                          chi tiền mặt hoặc chi   
                          qua ngân hàng trên một  
                          luồng nghiệp vụ thống   
                          nhất.                   

  FR-PAY-002              Hỗ trợ thanh toán một   P0
                          phần hoặc toàn bộ một   
                          hay nhiều khoản phải    
                          trả.                    

  FR-PAY-003              Hỗ trợ ứng trước nhà    P0
                          cung cấp và theo dõi số 
                          dư ứng trước theo đối   
                          tượng.                  

  FR-PAY-004              Chi hoàn tiền phải liên P1
                          kết giao dịch bán/thu   
                          ban đầu nếu có.         

  FR-PAY-005              Không ghi nhận chi phí  P0
                          chỉ vì có dòng tiền chi 
                          ra; phải căn cứ bản     
                          chất và điều kiện ghi   
                          nhận.                   

  FR-PAY-006              Kiểm tra số dư quỹ/tài  P1
                          khoản tiền theo chính   
                          sách; cảnh báo hoặc     
                          chặn chi vượt số dư     
                          theo cấu hình.          

  FR-PAY-007              Chứng từ chi đã ghi sổ  P0
                          phải được điều chỉnh    
                          bằng quy trình đảo/điều 
                          chỉnh.                  
  -----------------------------------------------------------------------

## 7.5. Chuyển tiền nội bộ

  -----------------------------------------------------------------------
  Mã                      Nghiệp vụ               Định khoản tham khảo
  ----------------------- ----------------------- -----------------------
  CK01                    Rút tiền ngân hàng về   Nợ 111 / Có 112
                          quỹ                     

  CK02                    Nộp tiền mặt vào ngân   Nợ 112 / Có 111
                          hàng                    

  CK03                    Chuyển giữa hai tài     Nợ 112 tài khoản nhận /
                          khoản ngân hàng         Có 112 tài khoản chuyển

  CK04                    Tiền đang chuyển        Nợ 113 / Có tài khoản
                                                  tiền chuyển đi, khi đáp
                                                  ứng điều kiện

  CK05                    Ngân hàng xác nhận tiền Nợ 112 / Có 113
                          đang chuyển đến         
  -----------------------------------------------------------------------

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-TRF-001              Chuyển tiền nội bộ phải P0
                          là chức năng riêng,     
                          không bị ghi nhận như   
                          doanh thu hoặc chi phí. 

  FR-TRF-002              Hỗ trợ tài khoản nguồn, P1
                          tài khoản đích, ngày    
                          chuyển, ngày nhận, phí  
                          và tham chiếu ngân      
                          hàng.                   

  FR-TRF-003              Cho phép trạng thái     P1
                          Đang chuyển, Đã nhận,   
                          Chênh lệch/Cần xử lý.   

  FR-TRF-004              Phí ngân hàng phải được P1
                          ghi nhận riêng theo bản 
                          chất, không gộp vào số  
                          tiền chuyển nội bộ.     

  FR-TRF-005              Tổng tài sản tiền không P0
                          được tăng/giảm do bản   
                          thân giao dịch điều     
                          chuyển, ngoại trừ phí   
                          hoặc chênh lệch thực    
                          tế.                     
  -----------------------------------------------------------------------

## 7.6. Mua hàng và công nợ phải trả

  -----------------------------------------------------------------------
  Mã                      Nghiệp vụ               Định khoản tham khảo
  ----------------------- ----------------------- -----------------------
  MH01                    Nhập hàng mua chịu      Nợ hàng tồn kho/hàng
                                                  mua phù hợp; Có 331

  MH02                    Mua hàng thanh toán     Nợ hàng tồn kho và thuế
                          ngay                    đầu vào đủ điều kiện;
                                                  Có 111/112

  MH03                    Hóa đơn nhận sau khi    Ghi nhận/điều chỉnh
                          hàng đã nhập            phần thuế và công nợ
                                                  theo hồ sơ, chính sách
                                                  phù hợp

  MH04                    Trả lại hàng mua        Điều chỉnh công nợ,
                                                  hàng tồn kho và thuế
                                                  liên quan

  MH05                    Giảm giá hàng mua       Điều chỉnh giá trị hàng
                                                  mua/công nợ theo bản
                                                  chất

  MH06                    Chiết khấu thương mại   Phân bổ/điều chỉnh theo
                          từ nhà cung cấp         chính sách kế toán và
                                                  hàng hóa liên quan

  MH07                    Ứng trước nhà cung cấp  Nợ 331 chi tiết nhà
                                                  cung cấp; Có 111/112

  MH08                    Hàng mua đang đi đường  Theo dõi hàng mua đang
                                                  đi đường khi đáp ứng
                                                  điều kiện

  MH09                    Hàng đi đường về kho    Kết chuyển từ hàng mua
                                                  đi đường sang hàng tồn
                                                  kho

  MH10                    Chi phí mua hàng/vận    Phân bổ vào giá trị
                          chuyển                  hàng tồn kho hoặc ghi
                                                  nhận phù hợp theo bản
                                                  chất
  -----------------------------------------------------------------------

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-PUR-001              Tách đơn mua, nhận      P0
                          hàng, hóa đơn mua và    
                          thanh toán thành các sự 
                          kiện/chứng từ có liên   
                          kết.                    

  FR-PUR-002              Hỗ trợ mua hàng trả     P0
                          ngay và mua hàng công   
                          nợ.                     

  FR-PUR-003              Hỗ trợ nhận hàng nhiều  P1
                          lần cho một đơn mua và  
                          hóa đơn cho nhiều lần   
                          nhận theo chính sách.   

  FR-PUR-004              Hỗ trợ hóa đơn đến      P1
                          trước hoặc sau hàng hóa 
                          theo quy trình được cấu 
                          hình.                   

  FR-PUR-005              Hỗ trợ trả hàng, giảm   P1
                          giá, chiết khấu và điều 
                          chỉnh công nợ.          

  FR-PUR-006              Chi phí mua hàng có thể P1
                          được phân bổ theo tiêu  
                          thức cấu hình và truy   
                          vết được.               

  FR-PUR-007              Ngăn ghi nhận trùng     P0
                          hàng tồn kho, thuế đầu  
                          vào hoặc công nợ khi    
                          một chứng từ được đồng  
                          bộ nhiều lần.           
  -----------------------------------------------------------------------

## 7.7. Bán hàng và công nợ phải thu

  -----------------------------------------------------------------------
  Mã                      Nghiệp vụ               Định khoản tham khảo
  ----------------------- ----------------------- -----------------------
  BH01                    Bán hàng thu tiền ngay  Ghi nhận doanh thu/thuế
                                                  khi đủ điều kiện và ghi
                                                  nhận tiền

  BH02                    Bán hàng chưa thu tiền  Ghi nhận phải thu khách
                                                  hàng và doanh thu khi
                                                  đủ điều kiện

  BH03                    Xuất kho ghi nhận giá   Nợ giá vốn / Có hàng
                          vốn                     tồn kho theo phương
                                                  pháp tính giá

  BH04                    Khách thanh toán một    Nợ 111/112 / Có 131
                          phần                    

  BH05                    Khách ứng trước         Nợ 111/112 / Có 131 chi
                                                  tiết khách hàng

  BH06                    Khách trả hàng          Điều chỉnh doanh thu,
                                                  thuế, công nợ/tiền và
                                                  hàng tồn kho/giá vốn
                                                  phù hợp

  BH07                    Giảm giá hàng bán       Điều chỉnh doanh thu và
                                                  nghĩa vụ thuế liên quan

  BH08                    Chiết khấu thương mại   Điều chỉnh doanh thu
                          cho khách               theo chính sách và quy
                                                  định

  BH09                    Xử lý nợ phải thu khó   Theo quy trình phê
                          đòi/xóa nợ              duyệt và quy định áp
                                                  dụng

  BH10                    Khách thanh toán thừa   Theo dõi số dư khách
                                                  hàng hoặc khoản phải
                                                  trả phù hợp
  -----------------------------------------------------------------------

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-SAL-001              Một giao dịch bán hàng  P0
                          phải có thể thanh toán  
                          ngay, bán chịu hoặc kết 
                          hợp nhiều phương thức   
                          thanh toán.             

  FR-SAL-002              Ghi nhận doanh thu phải P0
                          dựa trên điều kiện ghi  
                          nhận và trạng thái giao 
                          hàng/cung cấp dịch vụ   
                          phù hợp, không chỉ dựa  
                          vào thu tiền.           

  FR-SAL-003              Giá vốn phải được ghi   P0
                          nhận theo chính sách    
                          tính giá xuất kho được  
                          tenant cấu hình.        

  FR-SAL-004              Hỗ trợ khách thanh toán P0
                          một phần, nhiều lần,    
                          thanh toán nhiều hóa    
                          đơn và thanh toán thừa. 

  FR-SAL-005              Hỗ trợ trả hàng, giảm   P1
                          giá, chiết khấu và hoàn 
                          tiền có liên kết giao   
                          dịch gốc.               

  FR-SAL-006              Một đơn hàng có thể     P1
                          liên kết nhiều lần giao 
                          hàng, hóa đơn và thanh  
                          toán theo chính sách.   

  FR-SAL-007              Thanh toán công nợ      P0
                          không được ghi nhận     
                          doanh thu lần thứ hai.  

  FR-SAL-008              Hỗ trợ đối chiếu doanh  P1
                          thu, tiền thu, công nợ  
                          và giá vốn theo đơn     
                          hàng.                   
  -----------------------------------------------------------------------

## 7.8. Điều chỉnh kế toán và cuối kỳ

  -----------------------------------------------------------------------
  Mã                      Nghiệp vụ               Ví dụ định khoản
  ----------------------- ----------------------- -----------------------
  KT01                    Điều chỉnh sai sót      Theo nghiệp vụ gốc và
                                                  hồ sơ điều chỉnh

  KT02                    Phân bổ chi phí trả     Nợ chi phí phù hợp / Có
                          trước                   chi phí trả trước

  KT03                    Trích khấu hao TSCĐ     Nợ chi phí phù hợp / Có
                                                  hao mòn lũy kế

  KT04                    Trích lập dự phòng phải Theo chính sách và quy
                          thu                     định áp dụng

  KT05                    Điều chỉnh/đánh giá     Theo bản chất và quy
                          hàng tồn kho            định áp dụng

  KT06                    Đánh giá chênh lệch tỷ  Theo chính sách ngoại
                          giá                     tệ áp dụng

  KT07                    Kết chuyển doanh thu    Kết chuyển tài khoản
                                                  doanh thu theo chế độ
                                                  áp dụng

  KT08                    Kết chuyển chi phí      Kết chuyển tài khoản
                                                  chi phí theo chế độ áp
                                                  dụng

  KT09                    Xác định kết quả kinh   Kết chuyển kết quả sang
                          doanh                   tài khoản phù hợp

  KT10                    Khóa sổ kỳ kế toán      Kiểm tra điều kiện và
                                                  khóa kỳ
  -----------------------------------------------------------------------

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-CLS-001              Chỉ người có quyền mới  P0
                          được tạo/duyệt bút toán 
                          điều chỉnh và cuối kỳ.  

  FR-CLS-002              Hỗ trợ lịch kỳ kế toán, P0
                          ngày bắt đầu/kết thúc   
                          và trạng thái kỳ.       

  FR-CLS-003              Có checklist kiểm tra   P1
                          trước khi khóa kỳ.      

  FR-CLS-004              Không cho ghi sổ vào kỳ P0
                          đã khóa, trừ quy trình  
                          mở kỳ được cấp quyền và 
                          lưu lý do.              

  FR-CLS-005              Hỗ trợ kết chuyển và    P1
                          lập bút toán cuối kỳ    
                          theo quy tắc đã được    
                          phê duyệt.              

  FR-CLS-006              Mở lại kỳ phải lưu      P0
                          người thực hiện, thời   
                          gian, lý do và phạm vi  
                          ảnh hưởng.              
  -----------------------------------------------------------------------

## 7.9. Accounting Rule Engine

**Ràng buộc UX:** Rule Engine là năng lực backend. Không bắt buộc người dùng chọn
mã nghiệp vụ trên Phiếu thu/Phiếu chi nếu có thể suy ra từ nguồn hoặc ngữ cảnh.
Mọi mã nghiệp vụ nội bộ có thể được tạo/ánh xạ tự động và phải truy vết được.

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-RULE-001             Hệ thống phải chọn quy  P0
                          tắc hạch toán theo loại 
                          sự kiện/nghiệp vụ và    
                          điều kiện áp dụng.      

  FR-RULE-002             Quy tắc phải cấu hình   P0
                          được tài khoản Nợ/Có,   
                          điều kiện, công thức số 
                          tiền và chiều hạch      
                          toán.                   

  FR-RULE-003             Quy tắc hỗ trợ ngày     P0
                          hiệu lực; thay đổi quy  
                          tắc không được âm thầm  
                          thay đổi bút toán đã    
                          ghi sổ.                 

  FR-RULE-004             Hỗ trợ điều kiện theo   P1
                          tenant, chi nhánh, loại 
                          hàng, đối tượng, phương 
                          thức thanh toán và      
                          thuộc tính thuế khi phù 
                          hợp.                    

  FR-RULE-005             Có cơ chế kiểm tra quy  P0
                          tắc thiếu tài khoản,    
                          trùng điều kiện hoặc    
                          xung đột ưu tiên.       

  FR-RULE-006             Cho phép xem trước bút  P1
                          toán trước khi ghi sổ.  

  FR-RULE-007             Mọi thay đổi quy tắc    P0
                          phải có phiên bản,      
                          người thay đổi, thời    
                          điểm và phê duyệt theo  
                          phân quyền.             

  FR-RULE-008             Không cho phép cấu hình P0
                          tạo bút toán mất cân    
                          đối.                    
  -----------------------------------------------------------------------

## 7.10. Journal Entry và Posting Engine

**Ràng buộc kiến trúc:** Không bắt buộc triển khai một event bus hoặc nền tảng
message broker mới. Có thể dùng service/domain event, transaction nội bộ hoặc outbox
hiện có nếu đáp ứng được tính nguyên tử, retry, quan sát lỗi và idempotency. Chỉ bổ
sung hạ tầng khi audit chứng minh cần thiết.

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-JE-001               Mỗi JE phải có mã duy   P0
                          nhất, ngày hạch toán,   
                          kỳ kế toán, nguồn, diễn 
                          giải và trạng thái.     

  FR-JE-002               Mỗi JE có một hoặc      P0
                          nhiều dòng; tổng phát   
                          sinh Nợ phải bằng tổng  
                          phát sinh Có theo đơn   
                          vị tiền tệ và quy tắc   
                          làm tròn.               

  FR-JE-003               Mỗi dòng phải lưu tài   P0
                          khoản, số tiền, chiều   
                          Nợ/Có và các chiều phân 
                          tích cần thiết.         

  FR-JE-004               JE phải tham chiếu được P0
                          tới chứng từ và         
                          Business Event nguồn.   

  FR-JE-005               Posting phải có tính    P0
                          nguyên tử: không được   
                          cập nhật một phần sổ    
                          cái nếu phần còn lại    
                          thất bại.               

  FR-JE-006               Hỗ trợ chống xử lý lặp  P0
                          bằng idempotency key    
                          hoặc cơ chế tương       
                          đương.                  

  FR-JE-007               JE đã ghi sổ không được P0
                          sửa hoặc xóa trực tiếp. 

  FR-JE-008               Hỗ trợ bút toán đảo     P0
                          toàn phần hoặc một phần 
                          theo quy trình được cấp 
                          quyền.                  

  FR-JE-009               Cho phép ghi nhận diễn  P1
                          giải, tài liệu đính kèm 
                          và liên kết giao dịch   
                          liên quan.              

  FR-JE-010               Hỗ trợ đa tiền tệ nếu   P2
                          tenant bật chức năng;   
                          lưu nguyên tệ, tỷ giá   
                          và số tiền quy đổi.     
  -----------------------------------------------------------------------

## 7.11. Sổ chi tiết và đối chiếu

  -----------------------------------------------------------------------
  ID                      Yêu cầu                 Ưu tiên
  ----------------------- ----------------------- -----------------------
  FR-SUB-001              Hệ thống phải cung cấp  P0
                          sổ chi tiết tiền mặt và 
                          tiền gửi ngân hàng.     

  FR-SUB-002              Hệ thống phải theo dõi  P0
                          công nợ phải thu/phải   
                          trả theo đối tượng và   
                          chứng từ.               

  FR-SUB-003              Hệ thống phải theo dõi  P0
                          số lượng và giá trị     
                          hàng tồn kho theo       
                          kho/hàng hóa theo chính 
                          sách.                   

  FR-SUB-004              Số dư sổ chi tiết phải  P0
                          đối chiếu được với tài  
                          khoản tổng hợp tương    
                          ứng.                    

  FR-RECNC-001            Hỗ trợ đối chiếu sao kê P1
                          ngân hàng với giao dịch 
                          đã ghi nhận.            

  FR-RECNC-002            Hỗ trợ phát hiện giao   P1
                          dịch trùng hoặc chênh   
                          lệch số tiền/ngày/đối   
                          tượng.                  

  FR-RECNC-003            Hỗ trợ đối chiếu công   P0
                          nợ theo hóa đơn, thanh  
                          toán, bù trừ và số dư   
                          đầu kỳ.                 

  FR-RECNC-004            Hỗ trợ đối chiếu giá    P0
                          trị tồn kho với sổ cái. 

  FR-RECNC-005            Chênh lệch đối chiếu    P1
                          phải có trạng thái,     
                          người xử lý, nguyên     
                          nhân và kết quả.        
  -----------------------------------------------------------------------

## 7.12. Báo cáo

  -----------------------------------------------------------------------
  ID                      Báo cáo                 Yêu cầu
  ----------------------- ----------------------- -----------------------
  FR-RPT-001              Nhật ký chung           Lọc theo kỳ, tài khoản,
                                                  nguồn, chứng từ và
                                                  trạng thái

  FR-RPT-002              Sổ cái                  Hiển thị số dư đầu kỳ,
                                                  phát sinh Nợ/Có, số dư
                                                  cuối kỳ

  FR-RPT-003              Sổ quỹ tiền mặt         Theo dõi thu, chi và
                                                  tồn quỹ

  FR-RPT-004              Sổ tiền gửi ngân hàng   Theo dõi phát sinh và
                                                  số dư theo từng tài
                                                  khoản

  FR-RPT-005              Công nợ phải thu        Theo dõi theo khách
                                                  hàng, hóa đơn và tuổi
                                                  nợ

  FR-RPT-006              Công nợ phải trả        Theo dõi theo nhà cung
                                                  cấp, hóa đơn và hạn
                                                  thanh toán

  FR-RPT-007              Báo cáo nhập -- xuất -- Theo dõi số lượng và
                          tồn                     giá trị theo kho/hàng
                                                  hóa

  FR-RPT-008              Báo cáo doanh thu và    Phân tích theo thời
                          giá vốn                 gian, hàng hóa, khách
                                                  hàng, chi nhánh

  FR-RPT-009              Bảng cân đối phát sinh  Tổng hợp số dư và phát
                                                  sinh theo tài khoản

  FR-RPT-010              Báo cáo tài chính       Lập theo biểu mẫu/chế
                                                  độ kế toán tenant áp
                                                  dụng

  FR-RPT-011              Báo cáo lưu chuyển tiền Tổng hợp theo phương
                          tệ                      pháp và quy định áp
                                                  dụng

  FR-RPT-012              Báo cáo thuế/đối chiếu  Hỗ trợ dữ liệu và đối
                          thuế                    chiếu; không mặc nhiên
                                                  thay thế phần mềm kê
                                                  khai thuế
  -----------------------------------------------------------------------

# 8. Ma trận định khoản tham khảo

Các bút toán dưới đây chỉ là ví dụ định hướng. Tài khoản cụ thể, điều
kiện ghi nhận, thuế và thời điểm hạch toán phải được cấu hình theo chế
độ kế toán và chính sách được doanh nghiệp phê duyệt.

  Nhóm         Nghiệp vụ                            Nợ tham khảo                 Có tham khảo
  ------------ ------------------------------------ ---------------------------- --------------
  Thu          Thu bán hàng đủ điều kiện ghi nhận   111/112                      511, 3331
  Thu          Thu công nợ                          111/112                      131
  Thu          Khách ứng trước                      111/112                      131
  Thu          Thu lãi tiền gửi                     112                          515
  Thu          Nhân viên hoàn ứng                   111/112                      141
  Thu          Nhận tiền vay                        111/112                      341
  Chi          Trả nợ nhà cung cấp                  331                          111/112
  Chi          Tạm ứng nhân viên                    141                          111/112
  Chi          Trả lương                            334                          111/112
  Chi          Nộp thuế                             333                          111/112
  Chuyển       Rút tiền ngân hàng về quỹ            111                          112
  Chuyển       Nộp tiền mặt vào ngân hàng           112                          111
  Mua          Nhập hàng mua chịu                   156 hoặc tài khoản phù hợp   331
  Bán          Bán hàng công nợ                     131                          511, 3331
  Giá vốn      Xuất kho bán hàng                    632                          156
  Điều chỉnh   Phân bổ chi phí trả trước            641/642 hoặc phù hợp         242
  Điều chỉnh   Khấu hao tài sản                     641/642 hoặc phù hợp         214

**Không được dùng bảng này như một bộ định khoản cứng.** Đặc biệt, xử lý
thuế GTGT, hàng mua chưa có hóa đơn, hàng mua đang đi đường, hàng trả
lại, chiết khấu, chi phí mua hàng và ghi nhận doanh thu phải có điều
kiện nghiệp vụ riêng.

# 9. Mô hình dữ liệu logic đề xuất

Đây là mô hình khái niệm để Devin đối chiếu với database hiện tại. Không
bắt buộc mỗi khái niệm phải tương ứng một bảng riêng nếu kiến trúc hiện
tại có cách biểu diễn tương đương và bảo đảm các yêu cầu.

  -----------------------------------------------------------------------
  Entity/Concept                      Trách nhiệm và dữ liệu tối thiểu
  ----------------------------------- -----------------------------------
  TenantAccountingProfile             Tenant, chế độ kế toán, tiền tệ,
                                      năm tài chính, chính sách kế toán

  Account                             Tenant, mã tài khoản, tên, loại,
                                      trạng thái, tài khoản cha

  AccountingPeriod                    Tenant, ngày bắt đầu/kết thúc,
                                      trạng thái, thời điểm khóa

  BusinessEvent                       Event ID, tenant, loại sự kiện,
                                      source ID, thời điểm,
                                      payload/version

  Voucher                             Voucher ID, tenant, loại, số chứng
                                      từ, ngày chứng từ, ngày hạch toán,
                                      trạng thái

  VoucherLine                         Hàng hóa/khoản mục, số lượng, đơn
                                      giá, thuế, đối tượng, số tiền

  VoucherLink                         Liên kết chứng từ nguồn, chứng từ
                                      liên quan và quan hệ

  AccountingRule                      Mã, tenant/phạm vi, loại sự kiện,
                                      điều kiện, phiên bản, hiệu lực

  AccountingRuleLine                  Tài khoản Nợ/Có, biểu thức số tiền,
                                      điều kiện, chiều phân tích

  JournalEntry                        JE ID, tenant, số bút toán, ngày
                                      hạch toán, kỳ, source reference,
                                      trạng thái

  JournalEntryLine                    Tài khoản, Nợ/Có, số tiền, nguyên
                                      tệ, tỷ giá, đối tượng và chiều phân
                                      tích

  PaymentAllocation                   Giao dịch thanh toán, hóa đơn/khoản
                                      công nợ, số tiền phân bổ

  SubledgerTransaction                Giao dịch chi tiết tiền, công nợ,
                                      kho hoặc sổ phụ liên quan

  ReconciliationSession               Phạm vi đối chiếu, kỳ, trạng thái
                                      và người xử lý

  ReconciliationItem                  Dòng đối chiếu, giao dịch liên
                                      quan, chênh lệch và kết quả

  AuditLog                            Người thao tác, hành động, thời
                                      gian, giá trị trước/sau, lý do,
                                      correlation ID

  Attachment                          Tham chiếu tệp, loại tài liệu,
                                      chứng từ liên quan, metadata
  -----------------------------------------------------------------------

## 9.1. Yêu cầu dữ liệu và toàn vẹn

Mô hình dữ liệu trong mục 9 là mô hình logic đích, không phải yêu cầu tạo mới toàn bộ
bảng. Devin phải đối chiếu schema và module hiện có; tái sử dụng cấu trúc đã đáp ứng
đúng yêu cầu, chỉ bổ sung phần thiếu tối thiểu. Không tạo hai nguồn sự thật cho cùng
một số dư hoặc giao dịch.

-   Mọi bản ghi nghiệp vụ phải có tenant ID và được kiểm tra tenant ở
    tầng ứng dụng và tầng truy vấn.
-   Các khóa tham chiếu nguồn phải đủ để truy vết và chống ghi sổ trùng.
-   Không dùng số chứng từ hiển thị làm khóa duy nhất kỹ thuật.
-   Dùng decimal/fixed precision cho tiền; không dùng floating point cho
    tính toán tiền tệ.
-   Lưu rõ ngày chứng từ, ngày hạch toán, thời điểm tạo và thời điểm ghi
    sổ.
-   Quy tắc đã dùng để tạo JE phải được lưu dấu phiên bản hoặc snapshot
    cần thiết để tái hiện lịch sử.
-   Xóa mềm không được làm biến mất chứng từ đã ghi sổ hoặc phá vỡ quan
    hệ truy vết.
-   Thay đổi schema phải có migration có thể kiểm tra, backup/rollback
    plan và kiểm thử tương thích.

# 10. Trạng thái và vòng đời chứng từ

## 10.1. Trạng thái chuẩn

  Trạng thái        Ý nghĩa
  ----------------- -------------------------------------------------------
  Draft             Đang lập, chưa gửi duyệt
  PendingApproval   Đã gửi duyệt
  Approved          Đã được duyệt, chưa ghi sổ
  Posted            Đã ghi sổ chính thức
  Reversed          Đã được đảo toàn phần hoặc đã xử lý bằng bút toán đảo
  Cancelled         Hủy trước khi ghi sổ theo điều kiện cho phép

Không bắt buộc phải dùng đúng tên trạng thái trên nếu base code có state
machine tương đương. Tuy nhiên, phải phân biệt rõ chứng từ chưa ghi sổ
và chứng từ đã ghi sổ.

## 10.2. Quy tắc chuyển trạng thái

-   Draft có thể sửa hoặc xóa theo quyền.
-   PendingApproval có thể được duyệt hoặc trả lại.
-   Approved có thể được ghi sổ nếu vượt qua toàn bộ kiểm tra.
-   Posted không được sửa/xóa trực tiếp.
-   Posted muốn điều chỉnh phải tạo chứng từ điều chỉnh hoặc bút toán
    đảo có liên kết chứng từ gốc.
-   Cancelled chỉ áp dụng cho chứng từ chưa ghi sổ, trừ khi quy định và
    quy trình riêng xác định cách xử lý khác.
-   Mọi chuyển trạng thái phải ghi Audit Log.

# 11. Yêu cầu phi chức năng

  -----------------------------------------------------------------------
  ID                      Nhóm                    Yêu cầu
  ----------------------- ----------------------- -----------------------
  NFR-001                 Tính chính xác          Không ghi sổ JE mất cân
                                                  đối hoặc thiếu tài
                                                  khoản bắt buộc.

  NFR-002                 Tính nhất quán          Ghi sổ JE và cập nhật
                                                  sổ liên quan phải
                                                  nguyên tử hoặc có cơ
                                                  chế phục hồi nhất quán.

  NFR-003                 Chống trùng             Xử lý lặp cùng một
                                                  event không tạo thêm JE
                                                  hoặc giao dịch sổ phụ.

  NFR-004                 Bảo mật                 Cô lập dữ liệu theo
                                                  tenant; kiểm tra quyền
                                                  ở server-side, không
                                                  chỉ ẩn nút trên UI.

  NFR-005                 Auditability            Lưu lịch sử thao tác và
                                                  thay đổi quan trọng,
                                                  không cho người dùng
                                                  thông thường xóa log.

  NFR-006                 Hiệu năng               Phân trang, lọc và tổng
                                                  hợp báo cáo theo
                                                  kỳ/tenant; không tải
                                                  toàn bộ sổ vào bộ nhớ
                                                  client.

  NFR-007                 Khả năng mở rộng        Hỗ trợ nhiều tenant,
                                                  chi nhánh, kho, quỹ và
                                                  tài khoản ngân hàng.

  NFR-008                 Khả năng phục hồi       Có backup, phục hồi và
                                                  kiểm tra tính toàn vẹn
                                                  dữ liệu.

  NFR-009                 Khả năng kiểm thử       Accounting Rule,
                                                  Posting và
                                                  Reconciliation phải có
                                                  unit/integration tests.

  NFR-010                 Tương thích             Thay đổi phải bảo toàn
                                                  dữ liệu cũ và API đang
                                                  được sử dụng, trừ khi
                                                  có kế hoạch migration
                                                  được duyệt.

  NFR-011                 Quan sát hệ thống       Log có correlation ID
                                                  để truy vết từ Business
                                                  Event đến JE.

  NFR-012                 Tiền tệ                 Sử dụng decimal và quy
                                                  tắc làm tròn được cấu
                                                  hình, có kiểm thử biên.
  -----------------------------------------------------------------------

Các chỉ tiêu hiệu năng cụ thể (TPS, thời gian phản hồi, khối lượng dữ
liệu) phải được xác định sau khi đo base code và tải thực tế; không tự
đặt số liệu chưa có căn cứ.

# 12. Quy tắc kiểm soát và xử lý ngoại lệ

1.  Không cho ghi sổ nếu tổng Nợ khác tổng Có.
2.  Không cho ghi sổ nếu thiếu tài khoản hoặc đối tượng bắt buộc theo
    rule.
3.  Không cho ghi sổ vào kỳ khóa.
4.  Không cho ghi sổ trùng cùng source event và rule version trong cùng
    phạm vi nghiệp vụ.
5.  Không cho phép thanh toán phân bổ vượt số dư còn phải thu/phải trả,
    trừ trường hợp thu/chi thừa được hỗ trợ rõ ràng.
6.  Không cho phép một khoản thanh toán được phân bổ vượt tổng số tiền
    thanh toán.
7.  Không tự động ghi doanh thu khi nhận tiền ứng trước.
8.  Không tự động ghi chi phí chỉ vì phát sinh dòng tiền chi.
9.  Không tạo doanh thu/chi phí từ điều chuyển tiền nội bộ.
10. Không xóa lịch sử của chứng từ đã ghi sổ.
11. Không tự động thay đổi bút toán lịch sử khi Accounting Rule được cập
    nhật.
12. Khi xử lý lỗi giữa chừng, hệ thống phải rollback hoặc đánh dấu trạng
    thái cần phục hồi; không để số liệu nửa ghi sổ.

# 13. Kịch bản kiểm thử và tiêu chí nghiệm thu

## 13.1. Bộ test nghiệp vụ tối thiểu

  -----------------------------------------------------------------------
  Test ID                 Kịch bản                Kết quả mong đợi
  ----------------------- ----------------------- -----------------------
  AT-001                  Bán hàng thu tiền ngay  Doanh thu/thuế/tiền và
                                                  giá vốn được ghi nhận
                                                  đúng theo điều kiện;
                                                  không trùng

  AT-002                  Bán hàng công nợ        Ghi nhận phải thu; chưa
                                                  có dòng tiền

  AT-003                  Khách ứng trước         Tăng tiền và số dư ứng
                                                  trước; không tự ghi
                                                  doanh thu

  AT-004                  Khách trả một phần      Công nợ giảm đúng;
                                                  doanh thu không phát
                                                  sinh lại

  AT-005                  Một lần thu thanh toán  Phân bổ chính xác; tổng
                          nhiều hóa đơn           phân bổ không vượt tiền
                                                  thu

  AT-006                  Khách thanh toán thừa   Phần thừa được theo dõi
                                                  riêng theo khách hàng

  AT-007                  Nhập hàng mua chịu      Tăng kho/công nợ đúng
                                                  theo thời điểm và chứng
                                                  từ

  AT-008                  Trả hàng nhà cung cấp   Điều chỉnh kho, thuế và
                                                  công nợ theo quy tắc

  AT-009                  Khách trả hàng          Điều chỉnh doanh
                                                  thu/thuế/kho/giá vốn
                                                  đúng, liên kết giao
                                                  dịch gốc

  AT-010                  Hoàn tiền khách         Giảm tiền và xử lý
                                                  khoản phải trả/phải thu
                                                  đúng; không tạo chi phí
                                                  sai

  AT-011                  Chuyển tiền nội bộ      Không phát sinh doanh
                                                  thu/chi phí; số dư các
                                                  tài khoản tiền thay đổi
                                                  đúng

  AT-012                  Ngân hàng gửi giao dịch Chỉ một giao dịch được
                          trùng                   ghi nhận

  AT-013                  Ghi sổ chứng từ vào kỳ  Bị từ chối và có thông
                          khóa                    báo rõ

  AT-014                  Đảo chứng từ đã ghi sổ  Có JE đảo liên kết;
                                                  lịch sử gốc còn nguyên

  AT-015                  Rule thiếu tài khoản    Không cho ghi sổ; hiển
                                                  thị lỗi cấu hình

  AT-016                  JE nhiều dòng           Tổng Nợ bằng tổng Có;
                                                  số dư đúng

  AT-017                  Hai request ghi sổ đồng Không tạo hai JE cho
                          thời                    cùng source event

  AT-018                  Tenant A truy cập dữ    Bị từ chối ở
                          liệu Tenant B           server-side

  AT-019                  Đối chiếu công nợ       Tổng chi tiết khớp tài
                                                  khoản tổng hợp hoặc
                                                  hiển thị chênh lệch có
                                                  thể truy vết

  AT-020                  Đối chiếu tồn kho       Giá trị sổ kho khớp tài
                                                  khoản hàng tồn kho theo
                                                  cùng phạm vi và thời
                                                  điểm

  AT-021                  Thay đổi Accounting     JE đã ghi sổ không bị
                          Rule                    thay đổi

  AT-022                  Lỗi phát sinh giữa      Không có dữ liệu ghi sổ
                          Posting                 một phần

  AT-023                  Số tiền có nhiều chữ số Làm tròn theo chính
                          thập phân               sách; không sai lệch do
                                                  floating point

  AT-024                  Chứng từ bị hủy trước   Không tạo JE hoặc số dư
                          ghi sổ                  sổ phụ
  -----------------------------------------------------------------------

## 13.2. Tiêu chí nghiệm thu tổng thể

-   Tất cả test P0 phải đạt.
-   Không còn lỗi nghiêm trọng gây sai số dư, ghi sổ trùng, lộ dữ liệu
    tenant hoặc mất audit trail.
-   Số dư sổ chi tiết và sổ cái đối chiếu được trong phạm vi dữ liệu đã
    ghi sổ.
-   Có thể truy ngược một dòng báo cáo đến JE và chứng từ nguồn.
-   Migration có phương án backup, kiểm thử trên dữ liệu đại diện và kế
    hoạch rollback.
-   Các quy tắc định khoản đã được người có chuyên môn kế toán phê
    duyệt.
-   Có tài liệu hướng dẫn cấu hình, vận hành, xử lý lỗi và phục hồi.

# 14. Lộ trình triển khai

## Phase 0 -- Base-Code Audit (Read-only)

-   Kiểm tra kiến trúc, database, API, UI, service, background jobs và
    tests.
-   Lập ma trận Existing / Partial / Missing / Incorrect / Unknown.
-   Truy vết ít nhất các luồng bán hàng, nhập hàng, thu tiền, chi tiền
    và ghi sổ đang tồn tại.
-   Không sửa code hoặc database trong phase này.

**Đầu ra:** Audit Report, Gap Matrix, rủi ro dữ liệu và đề xuất
migration.

## Phase 1 -- Core Accounting (Backend-first, preserve UX)

-   Hoàn thiện/điều chỉnh danh mục tài khoản và kỳ kế toán nếu thiếu.
-   Tận dụng hai biểu mẫu Phiếu thu, Phiếu chi hiện có; không dựng lại UI.
-   Hoàn thiện Accounting Rule Engine phía backend.
-   Hoàn thiện Journal Entry và Posting Engine.
-   Kết nối điểm phát sinh kế toán hiện có từ Đơn hàng.
-   Hoàn thiện audit trail, reversal và kiểm soát kỳ.
-   Bổ sung sổ cái/sổ quỹ/sổ ngân hàng chỉ khi audit xác nhận còn thiếu.

**Điều kiện hoàn tất:** Các test P0 liên quan Core Accounting đạt.

## Phase 2 -- Sales, Purchase, Inventory & Subledger (reuse existing modules)

-   Liên kết bán hàng, mua hàng với kế toán.
-   Công nợ phải thu/phải trả.
-   Phân bổ thanh toán.
-   Nhập/xuất kho, trả hàng và giá vốn.
-   Đối chiếu sổ kho và công nợ.

**Điều kiện hoàn tất:** Không ghi nhận trùng; số liệu chi tiết và tổng
hợp đối chiếu được.

## Phase 3 -- Reconciliation & Controls

-   Đối chiếu sao kê ngân hàng.
-   Phát hiện giao dịch trùng.
-   Quy trình xử lý chênh lệch.
-   Checklist khóa sổ.
-   Kiểm soát phân quyền và audit.

## Phase 4 -- Financial Reporting

-   Nhật ký chung, sổ cái, bảng cân đối phát sinh.
-   Báo cáo tài chính theo chế độ áp dụng.
-   Báo cáo lưu chuyển tiền tệ.
-   Báo cáo thuế/đối chiếu dữ liệu thuế.
-   Bộ lọc và phân tích theo chi nhánh, kho, nhóm hàng và đối tượng.

# 15. Yêu cầu Devin thực hiện Base-Code Audit

**Nhiệm vụ:** Đối chiếu SRS này với codebase ShopERP hiện tại. Đây là
nhiệm vụ audit trước, không phải yêu cầu viết lại toàn bộ module.

**Thông tin hiện trạng do Product Owner cung cấp:** Base code hiện có 02 biểu mẫu
nhập liệu kế toán là Phiếu thu (Receipt), Phiếu chi (Payment), và 01 điểm khởi tạo
nghiệp vụ từ Đơn hàng (Orders). Đây là baseline cần xác minh trong code, không phải
lý do để Devin tạo thêm biểu mẫu. Nếu audit thấy khác, ghi nhận bằng chứng và hỏi
Product Owner; không tự suy diễn hoặc mở rộng UI.

## 15.1. Phương pháp kiểm tra

Devin phải kiểm tra thực tế:

-   Source code và dependency graph.
-   Database schema, migrations, indexes và constraints.
-   API endpoints, services, repositories và background jobs.
-   UI workflows và validation.
-   Luồng tạo chứng từ, duyệt, ghi sổ, đảo và đối chiếu.
-   Unit tests, integration tests, E2E tests và test coverage có liên
    quan.
-   Dữ liệu mẫu hoặc dữ liệu hiện có nếu được phép truy cập.

Không được kết luận một chức năng đã hoàn thành chỉ vì có màn hình,
endpoint, class hoặc bảng dữ liệu. Phải xác minh hành vi thực tế và
persistence.

## 15.2. Trạng thái đánh giá

  -----------------------------------------------------------------------
  Trạng thái                          Ý nghĩa
  ----------------------------------- -----------------------------------
  EXISTING                            Đã triển khai và có bằng chứng kiểm
                                      thử/hành vi

  PARTIAL                             Có triển khai nhưng chưa đáp ứng
                                      đầy đủ

  MISSING                             Chưa có

  INCORRECT                           Có triển khai nhưng sai nghiệp vụ,
                                      sai hạch toán hoặc sai kiểm soát

  UNKNOWN                             Chưa đủ bằng chứng để kết luận
  -----------------------------------------------------------------------

## 15.3. Deliverables bắt buộc

1.  **Architecture Assessment:** Mô tả kiến trúc kế toán hiện tại và các
    luồng dữ liệu.
2.  **Requirements Traceability Matrix:** Mỗi FR/NFR trong SRS có trạng
    thái, bằng chứng file/class/API/table/test và gap.
3.  **Accounting Flow Audit:** Truy vết luồng bán hàng, mua hàng, kho,
    thu, chi và chuyển tiền từ đầu đến cuối.
4.  **Data Integrity Assessment:** Đánh giá duplicate posting,
    transaction boundary, tenant isolation, decimal precision, audit
    trail và khả năng rollback.
5.  **Gap & Risk Register:** Danh sách vấn đề theo mức
    Critical/High/Medium/Low, ảnh hưởng và nguyên nhân.
6.  **Target Architecture Proposal:** Đề xuất tận dụng base code, chỉ rõ
    thành phần giữ lại, sửa, bổ sung hoặc loại bỏ.
7.  **Database Migration Plan:** Schema changes, data backfill,
    compatibility, backup và rollback.
8.  **Test Plan:** Unit, integration, regression và E2E tests cho các
    yêu cầu P0/P1.
9.  **Phased Implementation Plan:** Task cards, dependency, acceptance
    criteria và ước lượng.
10. **Decision Log:** Các giả định hoặc vấn đề cần Product Owner/kế toán
    trưởng quyết định.

## 15.4. Quy tắc thực thi

-   Chỉ audit read-only trong Phase 0.
-   Không sửa code, schema, dữ liệu hoặc cấu hình production khi chưa
    được phê duyệt.
-   Không viết lại module đang hoạt động đúng nếu chưa có bằng chứng về
    lỗi hoặc giới hạn kiến trúc.
-   Bảo toàn UX hiện tại: 02 biểu mẫu Phiếu thu/Phiếu chi và điểm phát sinh từ Orders.
-   Không thêm form, màn hình, dropdown bắt buộc, bước xác nhận hoặc trường nhập liệu
    mới theo mặc định. Mọi thay đổi UX phải có lý do nghiệp vụ không thể giải quyết
    bằng dữ liệu/logic backend hiện có và phải được Product Owner phê duyệt rõ ràng.
-   Không buộc người dùng nhập lại dữ liệu đã có trong Orders, khách hàng, nhà cung
    cấp, kho, sản phẩm, phương thức thanh toán hoặc chứng từ liên quan.
-   Phân biệt payment/receipt, revenue recognition, fulfillment, inventory movement,
    COGS, receivable/payable và refund/return; không gộp thành một event duy nhất.
-   Không tạo Accounting Engine thứ hai nếu đã có thành phần tương
    đương.
-   Không dùng stub, mock hoặc bypass validation làm giải pháp
    production.
-   Không bỏ qua business rules để làm test pass.
-   Không xóa hoặc ghi đè lịch sử kế toán.
-   Mọi đề xuất thay đổi phải nêu lý do, tác động tương thích ngược và
    cách kiểm thử.
-   Nếu SRS có điểm chưa đủ rõ hoặc có xung đột với quy định kế toán,
    phải ghi thành câu hỏi cần xác nhận; không tự suy đoán.

## 15.5. Format báo cáo gap

  --------------------------------------------------------------------------------------------------
  Requirement   Status     Evidence                      Current    Gap/Risk   Proposed   Test
  ID                       (file/class/API/table/test)   behavior              action     required
  ------------- ---------- ----------------------------- ---------- ---------- ---------- ----------

  --------------------------------------------------------------------------------------------------

## 15.6. Điều kiện kết thúc Phase 0

Phase 0 chỉ hoàn tất khi:

-   Tất cả yêu cầu trong SRS đã có trạng thái đánh giá.
-   Các luồng ghi sổ hiện tại đã được truy vết.
-   Rủi ro dữ liệu và khả năng ghi sổ trùng đã được đánh giá.
-   Có đề xuất migration và kế hoạch regression.
-   Các điểm chưa rõ được liệt kê thành câu hỏi cụ thể.
-   Không có thay đổi code/database ngoài phạm vi read-only audit.

**Devin phải dừng sau khi bàn giao báo cáo Phase 0 và chờ phê duyệt rõ
ràng trước khi triển khai.**

# 16. Các quyết định cần Product Owner và chuyên gia kế toán xác nhận

Các mục sau không được tự ý mặc định khi triển khai:

1.  Doanh nghiệp mục tiêu áp dụng chế độ kế toán nào và có thuộc đối
    tượng áp dụng Thông tư 99/2025/TT-BTC hay chế độ khác.
2.  Danh mục hệ thống tài khoản và mức độ chi tiết tài khoản.
3.  Phương pháp tính giá xuất kho và thời điểm ghi nhận giá vốn.
4.  Quy tắc ghi nhận doanh thu theo từng loại hàng hóa/dịch vụ và điều
    kiện giao hàng.
5.  Chính sách xử lý thuế GTGT, hóa đơn và chứng từ đầu vào/đầu ra.
6.  Cách xử lý hàng mua chưa có hóa đơn, hàng đi đường, chiết khấu và
    chi phí mua hàng.
7.  Quy trình duyệt chứng từ, hạn mức và phân tách nhiệm vụ.
8.  Chính sách kỳ kế toán, khóa sổ và mở lại kỳ.
9.  Có triển khai đa tiền tệ, đa chi nhánh và kế toán quản trị trong
    phiên bản đầu hay không.
10. Phạm vi kết nối ngân hàng, hóa đơn điện tử và hệ thống thuế.

------------------------------------------------------------------------

# 17. Kết luận và định hướng sản phẩm

ShopERP nên được xây dựng theo mô hình:

**Hai biểu mẫu nghiệp vụ hiện có (Phiếu thu, Phiếu chi) + điểm phát sinh từ Orders
-- Business Event/Transaction identity ở backend -- Accounting Rule Engine
-- Journal Entry/Posting Engine -- các sổ chi tiết được đối chiếu với sổ cái.**

Người dùng tiếp tục thao tác theo UX hiện tại. Hệ thống chịu trách nhiệm phân loại,
liên kết và hạch toán tự động tối đa; chỉ hỏi thêm khi dữ liệu hiện có không đủ để
đưa ra kết quả an toàn. Không coi việc có nhiều loại nghiệp vụ kế toán là lý do để
nhân bản biểu mẫu hoặc tăng thao tác người dùng.

Không thiết kế mỗi loại phiếu thành một bộ code riêng. Không để từng
phân hệ tự ghi sổ độc lập. Không coi dòng tiền vào/ra là doanh thu/chi
phí nếu bản chất kinh tế không phải như vậy.

Đây là nền tảng để ShopERP phục vụ nhiều doanh nghiệp thương mại và
nhiều tenant, đồng thời giữ được tính nhất quán, khả năng mở rộng, khả
năng kiểm toán và khả năng bảo trì lâu dài.


# 18. Bổ sung và điều chỉnh đặc tả v1.2 -- UX-preserving implementation

Mục 18 là phần điều chỉnh có tính ưu tiên cao của v1.2. Nếu nội dung ở các mục trước
được hiểu là yêu cầu thêm UI/trường nhập liệu hoặc tạo mới bảng/module, thì áp dụng
quy tắc tại mục 18 và mục 15.4 trước. Mục 18 không xóa các yêu cầu kế toán nghiệp vụ
của v1.0; nó điều chỉnh cách triển khai để phù hợp base code và nguyên tắc Option A.

## 18.1. Baseline UX và phạm vi thay đổi

### 18.1.1. Baseline cần bảo toàn

| Điểm thao tác | Hiện trạng theo Product Owner | Hướng xử lý v1.2 |
|---|---|---|
| Phiếu thu | Một biểu mẫu hiện có | Giữ nguyên; nâng cấp xử lý backend |
| Phiếu chi | Một biểu mẫu hiện có | Giữ nguyên; nâng cấp xử lý backend |
| Orders | Một điểm khởi tạo nghiệp vụ kế toán hiện có | Giữ luồng; hoàn thiện liên kết và posting |

Devin phải xác minh baseline trên code thực tế (UI routes/components, API, service,
DB schema và tests). Nếu khác biệt, báo cáo sai khác; không tự tạo UX mới.

### 18.1.2. UX contract (bắt buộc)

- UX hiện tại là mặc định và là ràng buộc sản phẩm.
- Không thêm form Phiếu bán hàng/Phiếu mua hàng/phiếu kế toán phụ chỉ để phục vụ
  Accounting Engine nếu dữ liệu nguồn đã tồn tại ở module tương ứng.
- Không thêm dropdown Business Event Type bắt buộc vào Phiếu thu/Phiếu chi như một
  giải pháp mặc định.
- Không yêu cầu nhập lại khách hàng, nhà cung cấp, đơn hàng, số tiền, phương thức
  thanh toán, kho hoặc thông tin sản phẩm đã có trong nguồn.
- Backend được phép chuẩn hóa dữ liệu, ánh xạ mã nghiệp vụ, tạo posting plan và sinh
  JE tự động mà không làm phát sinh thao tác người dùng.
- Nếu cần dữ liệu bổ sung, hệ thống trước tiên tìm dữ liệu từ nguồn liên kết; kế đến
  dùng quy tắc mặc định đã được phê duyệt; cuối cùng mới yêu cầu người dùng bổ sung.
- Mọi thay đổi giao diện phải có UX Change Proposal, bao gồm: lý do, tình huống không
  thể xử lý ở backend, số thao tác tăng thêm, phương án ít tác động hơn, ảnh hưởng
  đào tạo và tiêu chí rollback. Chỉ thực hiện sau khi Product Owner phê duyệt.

### 18.1.3. Tiêu chí UX nghiệm thu

1. Các luồng thu/chi/Orders hiện tại vẫn sử dụng được sau triển khai.
2. Không tăng số trường bắt buộc hoặc số bước thao tác trong luồng chuẩn nếu chưa có
   phê duyệt.
3. Thông tin đã có từ nguồn không bị yêu cầu nhập lại.
4. Lỗi thiếu dữ liệu phải chỉ ra trường/nguồn còn thiếu và cách xử lý; không yêu cầu
   người dùng đoán mã tài khoản hoặc định khoản.
5. Các thay đổi backend không làm mất dữ liệu, lịch sử chứng từ hoặc liên kết cũ.

## 18.2. Business Event và phân biệt các mốc nghiệp vụ

Accounting Engine phải phân biệt tối thiểu các khái niệm sau. Chúng có thể liên quan
đến cùng một Order nhưng không được gộp thành một sự kiện kế toán duy nhất:

| Khái niệm | Ý nghĩa | Không được suy diễn thành |
|---|---|---|
| OrderCreated | Đơn hàng được tạo | Doanh thu đã phát sinh |
| PaymentReceived | Tiền thực tế nhận được | Doanh thu đã được ghi nhận |
| PaymentAllocated | Tiền được phân bổ cho khoản phải thu/đơn hàng | Một khoản thu mới |
| OrderFulfilled / GoodsDelivered | Hàng/dịch vụ đã được giao/cung cấp theo điều kiện áp dụng | Tiền đã thu đủ |
| RevenueRecognized | Đã đủ điều kiện ghi nhận doanh thu theo chính sách | Chỉ đơn giản là đã thu tiền |
| InventoryIssued | Hàng đã xuất kho theo nghiệp vụ | Mặc nhiên cùng thời điểm với thanh toán |
| COGSRecognized | Giá vốn được xác định theo chính sách | Doanh thu hoặc dòng tiền |
| RefundPaid | Tiền hoàn thực tế đã chi | Chỉ là sửa số tiền của phiếu thu cũ |
| SalesReturnAccepted | Hàng trả lại được chấp nhận/ghi nhận | Tự động đồng nhất với hoàn tiền |

Một event có thể sinh nhiều JE hoặc nhiều nhóm posting khi bản chất nghiệp vụ yêu
cầu; ngược lại, nhiều event liên quan có thể được đối chiếu về cùng một Order. Engine
phải lưu quan hệ này thay vì giả định 1 Order = 1 JE.

### 18.2.1. Quy tắc Orders

- OrderCreated không tự tạo doanh thu.
- PaymentReceived tạo ghi nhận tiền và phân loại đối ứng phù hợp (công nợ, tiền ứng
  trước, khoản chưa xác định hoặc loại khác), không tự động ghi doanh thu.
- Revenue recognition chỉ xảy ra khi điều kiện ghi nhận của hàng hóa/dịch vụ và chính
  sách kế toán tenant được đáp ứng; trạng thái giao hàng/hoàn thành dịch vụ là dữ liệu
  quan trọng nhưng không phải quy tắc duy nhất cho mọi ngành.
- Inventory issue và COGS phải theo phương pháp tính giá, thời điểm xuất kho và chính
  sách đã được chuyên gia kế toán phê duyệt.
- Refund, return, discount, cancellation và reversal phải liên kết giao dịch gốc,
  giữ lịch sử, và tạo điều chỉnh/đảo phù hợp; không sửa/xóa JE đã ghi sổ.
- Một đơn hàng có thể có nhiều lần thanh toán, thanh toán từng phần, nhiều phương
  thức, nhiều lần giao hàng, trả hàng một phần và nhiều lần hoàn tiền.

## 18.3. Idempotency và định danh sự kiện nguồn

Không dùng riêng OrderId làm idempotency key. OrderId chỉ định danh đơn hàng, không
định danh từng lần thanh toán/giao hàng/hoàn tiền.

Khóa chống trùng phải đại diện cho **một sự kiện nguồn cụ thể**, tối thiểu gồm:

- TenantId;
- SourceModule (Orders, Cash, Bank, Inventory, Purchase...);
- SourceEntityType;
- SourceEventId hoặc SourceTransactionId ổn định;
- EventType / EventVersion khi cần phân biệt semantics;
- Posting purpose hoặc sequence nếu một source event hợp lệ sinh nhiều posting set.

Định dạng khóa cụ thể tùy kiến trúc hiện có, nhưng phải có unique constraint hoặc cơ
chế concurrency-safe tương đương ở persistence layer. Không dựa duy nhất vào kiểm tra
ở UI hoặc in-memory cache.

### 18.3.1. Hành vi bắt buộc

1. Retry cùng một source event không tạo JE/subledger movement trùng.
2. Hai payment transaction khác nhau của cùng Order được xử lý độc lập.
3. Partial payment không bị bỏ qua vì Order đã có một JE trước đó.
4. Cùng event được gửi đồng thời từ hai worker/request vẫn chỉ được ghi sổ một lần.
5. Nếu xử lý thất bại giữa chừng, retry không để lại posting một phần.
6. Nếu payload của cùng event ID thay đổi bất thường, hệ thống phải phát hiện xung
   đột, không âm thầm coi là retry hợp lệ.
7. Reversal/refund là event riêng, tham chiếu event gốc; không tái sử dụng key của
   giao dịch gốc để chặn nghiệp vụ hoàn tiền hợp lệ.

### 18.3.2. Gợi ý unique identity

`(TenantId, SourceModule, SourceEventId, EventType, PostingPurpose)`

Đây là gợi ý logic, không phải chỉ thị tạo bảng mới. Devin phải xác định có thể mở
rộng các khóa hiện tại hay không trước khi thêm schema.

## 18.4. Accounting Rule Engine -- hành vi backend

Rule Engine nhận dữ liệu từ chứng từ/transaction hiện có, xác định bản chất nghiệp
vụ, chọn rule theo cấu hình và tạo posting plan. Engine không phải là một màn hình
mới cho kế toán viên.

### 18.4.1. Thứ tự xác định nghiệp vụ

1. Source document/event có mã nghiệp vụ rõ ràng và đáng tin cậy.
2. Quan hệ đến Order, invoice, purchase, payment, inventory movement, receivable hoặc
   payable đã tồn tại.
3. Trạng thái, loại đối tượng, phương thức thanh toán và thuộc tính giao dịch.
4. Rule mặc định đã được tenant/chuyên gia kế toán phê duyệt.
5. Nếu vẫn mơ hồ hoặc có nhiều rule cùng phù hợp: dừng ở trạng thái cần phân loại/
   xử lý, nêu rõ lý do; không tự chọn một tài khoản đối ứng tùy tiện.

Không được suy luận bản chất chỉ dựa trên tên hiển thị của phiếu hoặc một field không
đủ tin cậy.

### 18.4.2. Rule selection

- Có tenant scope và ngày hiệu lực.
- Có ưu tiên xác định, không cho phép hai rule xung đột cùng thắng.
- Có version và audit log; JE lưu rule version/snapshot đã áp dụng.
- Hỗ trợ điều kiện theo nguồn, event type, đối tượng, hàng hóa, thuế, phương thức
  thanh toán, chi nhánh và tài khoản tiền khi phù hợp.
- Có validation trước khi active: tài khoản hợp lệ, chiều Nợ/Có, công thức số tiền,
  currency/rounding, điều kiện không chồng lấn và khả năng cân bằng.
- Thay đổi rule không được hồi tố âm thầm vào JE đã ghi sổ.
- Thiếu rule hoặc rule mơ hồ phải tạo lỗi nghiệp vụ có thể quan sát và xử lý; tuyệt
  đối không dùng fallback tùy tiện để làm giao dịch có vẻ thành công.

### 18.4.3. Posting plan

Trước khi ghi sổ, engine có thể tạo posting plan nội bộ gồm nguồn, rule version, các
account lines, amount calculation, subledger effects, validation result và trace IDs.
Posting plan có thể được lưu hoặc tái tạo theo kiến trúc hiện có; không bắt buộc thêm
màn hình xem trước cho người dùng nếu chưa được duyệt.

## 18.5. Posting Engine và tính nhất quán giao dịch

- JE phải cân bằng theo từng currency và quy tắc làm tròn được cấu hình.
- Ghi JE, posting marker/idempotency record và các cập nhật sổ chi tiết liên quan phải
  nằm trong cùng database transaction khi có thể.
- Nếu không thể dùng một transaction do ranh giới module/database, phải có outbox/
  inbox hoặc cơ chế bù trừ/repair có thể kiểm toán; không được để trạng thái thành công
  giả khi subledger chưa cập nhật.
- Chỉ phát thông báo/event "posted" sau khi dữ liệu bền vững đã commit.
- Retry phải an toàn; lỗi phải có correlation ID, source reference và khả năng phục
  hồi có kiểm soát.
- Không dùng fire-and-forget cho posting quan trọng nếu không có durable queue và
  reconciliation/repair mechanism.
- Không để Sales, Orders, Cash/Bank và Inventory cùng tự ghi cùng một bút toán vào GL.
  Một thành phần chịu trách nhiệm posting GL; các module nguồn cung cấp event/dữ liệu
  và giữ subledger/domain state theo phân định đã được audit.

## 18.6. Tái sử dụng sổ chi tiết và nguồn dữ liệu hiện có

Trước khi tạo bảng hoặc module mới, Devin phải audit các cấu trúc đang có cho:

- Cash/Bank transactions và số dư quỹ/tài khoản ngân hàng;
- Accounts Receivable / Accounts Payable và payment allocation;
- Inventory ledger, stock movement, costing và COGS;
- Sales/Orders, purchase, invoice/receipt references;
- General Ledger / Journal Entry / posting history.

### 18.6.1. Quy tắc quyết định

- EXISTING và đúng: KEEP, không nhân bản.
- PARTIAL: FIX/EXTEND tại đúng nguồn dữ liệu hoặc adapter phù hợp.
- MISSING: ADD tối thiểu, có lý do và migration.
- INCORRECT: FIX có kế hoạch bảo toàn dữ liệu và regression.
- UNKNOWN: tiếp tục điều tra, không tạo bảng mới chỉ để né việc audit.

Mỗi loại số dư/giao dịch chỉ có một source of truth được chỉ định. Báo cáo có thể đọc
projection/read model nhưng không được biến thành nguồn dữ liệu ghi sổ thứ hai.

### 18.6.2. Đối chiếu

Engine phải hỗ trợ đối chiếu tổng hợp với chi tiết, tối thiểu:

- Cash/Bank subledger với tài khoản tiền tương ứng trong GL;
- AR/AP theo đối tượng/chứng từ với tài khoản tổng hợp;
- Inventory quantity/value/costing với tài khoản hàng tồn kho và COGS;
- Sales revenue/tax/receipts với Orders và chứng từ bán hàng;
- Payment allocation với số dư phải thu/phải trả.

Chênh lệch phải được liệt kê, truy vết, phân loại và xử lý bằng adjustment/reversal
được cấp quyền; không tự ghi bù để che lệch.

## 18.7. Thanh toán thừa, phân bổ và số dư chưa phân bổ

Không từ chối một khoản tiền thực tế đã nhận chỉ vì lớn hơn số công nợ còn phải thu.

Ví dụ: khách còn nợ 5.000.000 VND nhưng chuyển 6.000.000 VND:

- 5.000.000 VND được phân bổ vào khoản phải thu hợp lệ;
- 1.000.000 VND còn lại được ghi nhận là khoản khách trả trước/số dư chưa phân bổ
  theo chính sách kế toán và tài khoản phù hợp;
- không tự ghi 1.000.000 VND thành doanh thu;
- phải theo dõi theo khách hàng và có thể phân bổ cho giao dịch tương lai theo quyền.

Hệ thống phải ngăn phân bổ vượt quá số dư còn mở của một khoản phải thu cụ thể, nhưng
không được ngăn nhận tiền thực tế. Tương tự, cần xử lý khoản thu chưa xác định, khoản
chi trả thừa và số dư chưa phân bổ theo đúng bản chất, có trạng thái để xử lý.

## 18.8. Reversal, refund, return và sửa sai

- Không update/delete trực tiếp JE đã posted.
- Refund payment là dòng tiền chi; return/discount/revenue adjustment là nghiệp vụ
  điều chỉnh doanh thu/công nợ/thuế/kho theo bản chất. Chúng có thể phát sinh cùng
  nhau nhưng không mặc định là một event.
- Mọi reversal/adjustment phải tham chiếu source event và JE gốc, có actor, timestamp,
  reason, amount/scope và approval theo quyền.
- Partial refund/partial return phải được hỗ trợ; không đảo toàn bộ Order mặc định.
- Đảm bảo tổng tiền hoàn không vượt số tiền có thể hoàn theo chính sách, trừ trường
  hợp ngoại lệ có phê duyệt và audit.
- Sau điều chỉnh, phải chạy/đánh dấu reconciliation cần thiết.

## 18.9. Phân loại gap và quyết định phạm vi triển khai

Trong báo cáo Devin, mỗi đề xuất phải được gắn một trong các nhãn:

| Nhãn | Ý nghĩa | Quy tắc |
|---|---|---|
| KEEP | Đã có và đúng | Không viết lại |
| FIX | Có nhưng sai/lỗi | Sửa có regression |
| EXTEND BACKEND | Có nền tảng nhưng thiếu logic backend | Ưu tiên trước UI |
| ADD BACKEND | Chưa có năng lực backend cần thiết | Bổ sung tối thiểu |
| ADD UX | Thực sự cần thay đổi giao diện | Cần PO approval trước |
| DEFER | Chưa cần cho phạm vi hiện tại | Không triển khai trong phase này |
| UNKNOWN | Chưa đủ bằng chứng | Điều tra thêm, không đoán |

Một gap trong SRS không tự động đồng nghĩa với một màn hình mới hoặc một bảng mới.
Devin phải chỉ ra yêu cầu nghiệp vụ nào chưa đáp ứng, bằng chứng hiện trạng, cách xử
lý backend, và lý do nếu đề xuất thay đổi UX/schema.

## 18.10. Kế hoạch kiểm thử bổ sung P0

Các test sau bổ sung cho mục 13 và là điều kiện nghiệm thu trước production:

| ID | Tình huống | Kết quả bắt buộc |
|---|---|---|
| ACC12-001 | Tạo Order nhưng chưa thanh toán/chưa đủ điều kiện ghi nhận | Không tự ghi nhận doanh thu |
| ACC12-002 | Nhận tiền đặt cọc/ứng trước | Ghi nhận tiền và khoản ứng trước phù hợp; không ghi doanh thu mặc định |
| ACC12-003 | Một Order có 2 lần thanh toán riêng | Hai payment events được xử lý độc lập |
| ACC12-004 | Retry cùng payment event nhiều lần | Chỉ một lần posting |
| ACC12-005 | Hai worker xử lý đồng thời cùng event | Không duplicate posting |
| ACC12-006 | Khách trả nhiều hơn công nợ | Phần dư thành advance/unapplied balance, không bị từ chối hoặc ghi doanh thu |
| ACC12-007 | Payment cho nhiều hóa đơn | Phân bổ từng khoản chính xác, không vượt số dư từng khoản |
| ACC12-008 | Order fulfilled nhưng chưa thu tiền | Ghi nhận doanh thu/công nợ theo chính sách; không tạo dòng tiền giả |
| ACC12-009 | Thu tiền nhưng chưa fulfilled | Không ghi doanh thu nếu chưa đủ điều kiện |
| ACC12-010 | Hàng xuất kho | Stock movement và COGS theo chính sách; không phụ thuộc đơn thu tiền |
| ACC12-011 | Partial refund | Ghi nhận hoàn tiền/điều chỉnh tương ứng, giữ liên kết giao dịch gốc |
| ACC12-012 | Return nhưng chưa refund | Điều chỉnh hàng/doanh thu/công nợ phù hợp; không tạo dòng tiền chi giả |
| ACC12-013 | Thiếu hoặc xung đột Accounting Rule | Không tự chọn rule; báo lỗi có thể truy vết |
| ACC12-014 | Posting fail giữa chừng | Không có JE/subledger cập nhật một phần |
| ACC12-015 | Tenant A truy cập source/event Tenant B | Bị từ chối; không rò rỉ dữ liệu |
| ACC12-016 | Rule thay đổi sau khi JE posted | JE lịch sử giữ nguyên rule version/snapshot |
| ACC12-017 | Đối chiếu GL với AR/AP/Inventory/Cash | Sai lệch được phát hiện, không tự che lấp |
| ACC12-018 | Luồng thu/chi hiện hữu sau nâng cấp | Không tăng trường bắt buộc hoặc bước thao tác khi chưa được duyệt |

## 18.11. Migration, triển khai và rollback

- Phase 0 phải lập inventory schema và xác định dữ liệu nào là source of truth.
- Migration phải additive-first khi có thể; không drop/rename cột/bảng đang dùng trước
  khi chứng minh không còn consumer và có kế hoạch tương thích.
- Không chạy backfill trên production trước khi có backup, dry-run, số liệu đối soát
  trước/sau và phê duyệt.
- Không tự tạo lại JE lịch sử nếu không có migration strategy được chuyên gia kế toán
  phê duyệt; tránh duplicate posting khi backfill.
- Với dữ liệu cũ thiếu source event ID, cần chiến lược khóa tương thích/backfill có
  kiểm tra collision; không giả định OrderId đủ để chống trùng.
- Mỗi migration phải có forward plan, rollback/compensation plan, expected row counts,
  integrity checks và test trên bản sao dữ liệu đại diện.
- Triển khai theo feature flag/tenant pilot nếu kiến trúc cho phép; theo dõi duplicate,
  unbalanced JE, posting failures và reconciliation differences.
- Rollback code không được làm mất các bút toán đã ghi sổ; cần backward compatibility
  hoặc quy trình phục hồi dữ liệu có kiểm soát.

## 18.12. Phase gates cập nhật

1. **Gate 0 -- Read-only audit:** Hoàn thành ma trận traceability, sơ đồ luồng thực tế,
   inventory UI/schema/ledger, xác định nguồn sự thật và danh sách gap. Dừng chờ PO.
2. **Gate 1 -- Backend design approval:** PO/kế toán trưởng duyệt event semantics,
   mapping/rule, idempotency identity, transaction boundary và migration plan.
3. **Gate 2 -- Core engine:** Triển khai rule/posting backend và tests; chưa thay đổi UX.
4. **Gate 3 -- Orders integration:** Kết nối event payment/fulfillment/return/inventory
   hiện có; chứng minh không ghi nhận doanh thu hoặc tiền trùng/sai thời điểm.
5. **Gate 4 -- Subledger reconciliation:** Tái sử dụng sổ chi tiết hiện có; đối chiếu
   và sửa các chênh lệch đã xác định.
6. **Gate 5 -- Pilot and acceptance:** Chạy regression/E2E, migration rehearsal, pilot
   tenant, reconciliation và PO sign-off.

Không tự chuyển sang gate kế tiếp nếu gate hiện tại chưa có bằng chứng nghiệm thu và
phê duyệt theo phạm vi yêu cầu.

## 18.13. Prompt thực thi bắt buộc cho Devin

Devin phải tuân thủ chỉ thị sau khi bắt đầu công việc theo SRS v1.2:

> Preserve the existing ShopERP user experience. The current baseline reported by the
> Product Owner is exactly two accounting input forms (Receipt and Payment) plus the
> existing Orders accounting entry point. Verify this in the repository. Do not add
> forms, screens, mandatory Business Event Type dropdowns, or extra user input fields
> by default. First reuse existing source data and infer accounting semantics in the
> backend. Any UX change requires a written justification showing why backend
> inference cannot safely satisfy the requirement and explicit Product Owner approval.
>
> Start with a read-only audit. Do not modify code, schema, data, configuration, or
> production. Trace the real code path from Orders/Receipt/Payment through services,
> persistence, journal entries, cash/bank, receivables/payables, inventory and reports.
> Classify every requirement as KEEP, FIX, EXTEND BACKEND, ADD BACKEND, ADD UX, DEFER,
> or UNKNOWN, with concrete evidence (file, class, method, endpoint, table, migration,
> and test). Do not infer implementation from class/table names alone.
>
> Do not use OrderId alone as an idempotency key. Distinguish individual payment,
> fulfillment, inventory, refund and return events. Do not equate PaymentReceived with
> RevenueRecognized. Support partial payments, multiple tenders, overpayments,
> customer advances, unapplied balances, partial refunds and returns. Do not reject
> actual money received merely because it exceeds the open receivable; prevent only
> over-allocation to a specific receivable.
>
> Audit existing cash/bank, AR/AP, inventory/COGS and GL structures before proposing
> new tables or ledgers. Reuse the current source of truth wherever correct. Do not
> introduce a new message broker or distributed architecture unless evidence proves
> it necessary. No stubs, fake success, bypassed validation, destructive migrations,
> or rewriting working modules. Stop after Phase 0 and wait for explicit approval.

## 18.14. Những nội dung cần xác nhận chuyên môn trước production

Ngoài danh sách mục 16, cần xác nhận thêm:

1. Điều kiện ghi nhận doanh thu theo từng loại hàng hóa/dịch vụ và hợp đồng.
2. Thời điểm ghi nhận thuế và cách xử lý hóa đơn, điều chỉnh hóa đơn theo quy định.
3. Phương pháp giá xuất kho, thời điểm ghi nhận COGS và xử lý trả hàng.
4. Tài khoản/chi tiết theo dõi tiền khách ứng trước, trả thừa và khoản thu chưa xác định.
5. Cách xử lý giao dịch nhiều phương thức thanh toán, nhiều lần giao hàng và nhiều lần
   hoàn tiền.
6. Chính sách làm tròn, phân bổ chênh lệch và đa tiền tệ nếu bật.
7. Quyền duyệt, tự động posting và ngưỡng giao dịch cần kiểm soát thủ công.

Các ví dụ tài khoản trong tài liệu chỉ để minh họa logic. Không hard-code tài khoản
hoặc coi ví dụ là kết luận pháp lý. Cấu hình thực tế phải phù hợp chế độ kế toán,
chính sách thuế và hồ sơ doanh nghiệp được áp dụng.
