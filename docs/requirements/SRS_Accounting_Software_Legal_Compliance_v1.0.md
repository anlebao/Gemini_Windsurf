# SRS -- Accounting Software Legal Compliance v1.0

**Product:** ShopERP Accounting Engine -- Vạn An Ecosystem\
**Document type:** Software Requirements Specification (SRS)\
**Version:** 1.0\
**Baseline date:** 09/11/2026\
**Status:** Approved for baseline / implementation planning\
**Primary language:** Vietnamese\
**Audience:** Product Owner, Accounting Domain Lead, Software Architect,
Devin/Development Team, QA, Security, Implementation & Compliance

------------------------------------------------------------------------

## 1. Purpose

Tài liệu này xác định các yêu cầu phần mềm cần có để ShopERP Accounting
Engine hỗ trợ doanh nghiệp thực hiện công tác kế toán phù hợp với pháp
luật Việt Nam, đồng thời thiết lập tiêu chí kiểm thử và hồ sơ chứng minh
tuân thủ.

SRS này chuyển các yêu cầu pháp lý thành yêu cầu có thể thiết kế, triển
khai và kiểm chứng. Tài liệu **không phải** ý kiến tư vấn pháp lý, chứng
nhận sản phẩm hoặc cam kết rằng phần mềm tự động làm cho mọi khách hàng
tuân thủ pháp luật. Việc tuân thủ còn phụ thuộc chế độ kế toán được
chọn, cấu hình, dữ liệu đầu vào, quy trình nội bộ và trách nhiệm của đơn
vị sử dụng.

## 2. Product context and design constraints

### 2.1 Context

ShopERP hiện có UX nhập liệu kế toán tối giản với: - Phiếu thu. - Phiếu
chi. - Entry point từ Orders.

Thiết kế đã thống nhất là **bảo toàn UX hiện có**: không tự ý tạo thêm
hàng loạt form chứng từ. Hệ thống ưu tiên Business Event Type/Sub-type,
Accounting Rule Engine và Event Handler ở tầng nghiệp vụ/backend để phân
loại sự kiện, sinh đề xuất hạch toán và kiểm soát quy trình.

### 2.2 Design principles

1.  **Preserve existing UX:** Không tạo form mới nếu nghiệp vụ có thể xử
    lý qua Business Event và cấu hình.
2.  **No silent posting:** Không tự động ghi sổ một nghiệp vụ chưa qua
    các kiểm soát/phê duyệt được cấu hình.
3.  **Posted data traceability:** Không sửa/xóa trực tiếp dữ liệu đã ghi
    sổ; mọi sửa sai phải có dấu vết và nghiệp vụ điều chỉnh phù hợp.
4.  **Versioned accounting rules:** Quy tắc hạch toán, biểu mẫu, tài
    khoản và báo cáo phải có phiên bản, ngày hiệu lực và lịch sử thay
    đổi.
5.  **Tenant isolation:** Dữ liệu kế toán của mỗi pháp nhân/đơn vị kế
    toán phải được cô lập.
6.  **Offline-aware, controlled sync:** Nếu Edge ShopERP hoạt động
    offline, dữ liệu đồng bộ phải chống trùng, giữ nguồn gốc, thứ tự
    nghiệp vụ và phát hiện xung đột; không để sync ghi đè sổ đã khóa.
7.  **Human accountability:** Phần mềm hỗ trợ kiểm soát và cung cấp
    thông tin; không thay thế trách nhiệm của người quản lý, kế toán
    trưởng/phụ trách kế toán và người liên quan.
8.  **Configurable, not hard-coded:** Không hard-code chế độ kế toán
    hoặc quy tắc thuế vào UI hay một luồng nghiệp vụ duy nhất.

## 3. Scope

### 3.1 In scope

-   Hỗ trợ cấu hình chế độ kế toán doanh nghiệp theo văn bản áp dụng.
-   Quản lý kỳ kế toán, danh mục tài khoản, đối tượng và chiều phân
    tích.
-   Tiếp nhận Business Events từ Orders và các module ShopERP.
-   Lập, kiểm tra, phê duyệt, ghi sổ và điều chỉnh chứng từ/bút toán.
-   Sổ kế toán, đối chiếu, khóa kỳ và báo cáo đầu ra.
-   Nhật ký kiểm toán, phân quyền, bảo mật, sao lưu/khôi phục.
-   Kết nối hoặc chuẩn bị giao diện tích hợp với hóa đơn điện tử, chữ ký
    số, ngân hàng và hệ thống liên quan.
-   Quản lý phiên bản quy tắc và cập nhật theo thay đổi pháp luật.
-   Kiểm thử tuân thủ, xuất hồ sơ kiểm chứng.

### 3.2 Out of scope for v1.0

-   Tự động kết luận nghĩa vụ thuế của mọi loại hình doanh nghiệp.
-   Thay thế dịch vụ hóa đơn điện tử, chữ ký số, ngân hàng hoặc cổng nộp
    thuế.
-   Tự động nộp tờ khai hoặc ký số nếu chưa có tích hợp, ủy quyền và
    kiểm soát riêng.
-   Tư vấn kế toán/thuế thay cho chuyên gia có trách nhiệm.
-   Triển khai đầy đủ chế độ kế toán hộ kinh doanh/cá nhân kinh doanh
    hoặc HTX trong cùng baseline này.
-   Định giá tài sản, kiểm toán độc lập hoặc xác nhận tính hợp pháp của
    chứng từ nguồn.

### 3.3 Multi-regime boundary

V1.0 lấy **doanh nghiệp** làm phạm vi tuân thủ chính, với Thông tư
99/2025/TT-BTC là chế độ kế toán doanh nghiệp được ban hành và có hiệu
lực từ 01/01/2026 (áp dụng cho năm tài chính bắt đầu từ hoặc sau ngày
này).

Hộ kinh doanh/cá nhân kinh doanh và HTX phải được coi là các compliance
profile riêng, có SRS/phụ lục pháp lý riêng và được xác nhận văn bản
hiệu lực trước khi phát hành. Không áp dụng máy móc hệ thống tài khoản,
sổ và báo cáo doanh nghiệp cho các đối tượng này.

## 4. Legal and standards baseline

> **Quy tắc quản lý căn cứ pháp lý:** Trước mỗi release, Compliance
> Owner phải kiểm tra tình trạng hiệu lực, văn bản sửa đổi/thay thế,
> ngày áp dụng và phạm vi đối tượng. Nếu có xung đột giữa tài liệu thiết
> kế và văn bản pháp luật hiện hành, văn bản pháp luật được ưu tiên;
> phải lập change request và đánh giá ảnh hưởng.

  -----------------------------------------------------------------------
  ID                Văn bản           Vai trò trong SRS Ghi chú
  ----------------- ----------------- ----------------- -----------------
  LEG-01            Luật Kế toán số   Nguyên tắc, chứng Cần đối chiếu văn
                    88/2015/QH13 và   từ, sổ, báo cáo,  bản hợp
                    văn bản sửa đổi,  lưu trữ, trách    nhất/phiên bản
                    bổ sung còn hiệu  nhiệm             hiện hành khi
                    lực                                 phát hành

  LEG-02            Nghị định         Quy định chi tiết Kiểm tra hiệu lực
                    174/2016/NĐ-CP và thi hành Luật Kế  tại từng release
                    văn bản sửa       toán              
                    đổi/thay thế (nếu                   
                    có)                                 

  LEG-03            Thông tư          Chế độ kế toán    Hiệu lực
                    99/2025/TT-BTC    doanh nghiệp;     01/01/2026; áp
                                      Điều 28 quy định  dụng cho năm tài
                                      sử dụng phần mềm  chính bắt đầu từ
                                      kế toán           hoặc sau
                                                        01/01/2026

  LEG-04            Thông tư          Hướng dẫn tiêu    CSDL VBPL được
                    103/2005/TT-BTC   chuẩn và điều     tra cứu ghi nhận
                                      kiện của phần mềm văn bản hiện
                                      kế toán           thời; cần đọc
                                                        cùng luật và quy
                                                        định mới hơn,
                                                        không mặc định
                                                        các căn cứ cũ
                                                        trong văn bản vẫn
                                                        là căn cứ pháp lý
                                                        hiện hành

  LEG-05            Pháp luật về giao Tích hợp, xác     Phải xác định văn
                    dịch điện tử, hóa thực, bảo vệ và   bản cụ thể theo
                    đơn điện tử, chữ  trao đổi dữ liệu  từng tích hợp
                    ký điện tử/chữ ký                   
                    số và an toàn                       
                    thông tin có liên                   
                    quan                                

  LEG-06            Pháp luật thuế    Quy tắc thuế, dữ  Không coi chế độ
                    hiện hành áp dụng liệu hóa đơn và   kế toán là văn
                    cho từng nghiệp   báo cáo liên quan bản duy nhất
                    vụ                                  quyết định nghĩa
                                                        vụ thuế
  -----------------------------------------------------------------------

### 4.1 Key mandatory software requirements -- Article 28, Circular 99/2025/TT-BTC

Phần mềm kế toán doanh nghiệp được lựa chọn sử dụng phải đáp ứng tối
thiểu các nhóm yêu cầu:

-   **28.1(a) -- Legal/accounting correctness:** Quy trình và nghiệp vụ
    được thiết lập phải tuân thủ pháp luật kế toán, thuế và pháp luật
    liên quan; không làm thay đổi bản chất, nguyên tắc, phương pháp kế
    toán và thông tin trình bày trên sổ, báo cáo.
-   **28.1(b) -- Accuracy, consistency, non-duplication and
    traceability:** Xử lý chính xác, phù hợp, không trùng lặp; khi sửa
    chữa phải lưu dấu vết nội dung đã ghi sổ theo trình tự thời gian.
-   **28.1(c) -- Confidentiality, safety and intervention protection:**
    Dữ liệu phải bảo mật, an toàn; hệ thống có khả năng cảnh báo hoặc
    ngăn chặn can thiệp có chủ ý làm thay đổi thông tin, số liệu đã ghi
    sổ.
-   **28.1(d) -- Output availability:** Cung cấp đầy đủ, kịp thời dữ
    liệu đầu ra theo yêu cầu của cơ quan có thẩm quyền và bên sử dụng dữ
    liệu.
-   **28.1(dd) -- Connectivity/readiness:** Có khả năng kết nối hoặc sẵn
    sàng kết nối với phần mềm liên quan như hóa đơn điện tử, chữ ký số.
-   **28.1(e) -- Maintainability:** Có khả năng nâng cấp, sửa đổi, bổ
    sung phù hợp với thay đổi pháp luật kế toán, thuế và pháp luật liên
    quan.
-   **28.2 -- Accountability:** Người quản lý điều hành, kế toán
    trưởng/phụ trách kế toán và người liên quan chịu trách nhiệm về tính
    chính xác, trung thực của thông tin, số liệu kế toán được cung cấp
    từ phần mềm.

Các yêu cầu kỹ thuật chi tiết trong SRS dưới đây là cách triển khai/kiểm
chứng các nghĩa vụ trên; không phải mọi chi tiết kỹ thuật đều được Điều
28 quy định nguyên văn.

## 5. Definitions

  -----------------------------------------------------------------------
  Term                                Definition
  ----------------------------------- -----------------------------------
  Business Event                      Sự kiện kinh tế/nghiệp vụ phát sinh
                                      từ Orders hoặc module khác, có thể
                                      cần xử lý kế toán

  Accounting Event                    Business Event đã được phân loại và
                                      xác nhận thuộc phạm vi xử lý kế
                                      toán

  Voucher                             Chứng từ kế toán hoặc bản ghi chứng
                                      từ trong hệ thống

  Journal Entry                       Bút toán ghi Nợ/Có

  Draft                               Dữ liệu đang lập, chưa ghi sổ

  Approved                            Đã được người có thẩm quyền phê
                                      duyệt theo workflow

  Posted                              Đã ghi vào sổ kế toán chính thức

  Reversal/Adjustment                 Nghiệp vụ đảo/điều chỉnh có liên
                                      kết với nghiệp vụ gốc

  Accounting Period                   Kỳ kế toán

  Rule Set                            Tập hợp quy tắc hạch toán có phiên
                                      bản và ngày hiệu lực

  Audit Trail                         Dấu vết kiểm toán ghi lại hoạt động
                                      và thay đổi

  Tenant                              Phạm vi dữ liệu của khách hàng/pháp
                                      nhân

  Compliance Profile                  Cấu hình chế độ kế toán và quy định
                                      áp dụng cho một loại đơn vị
  -----------------------------------------------------------------------

## 6. User roles and responsibilities

  -----------------------------------------------------------------------
  Role                                Main permissions / responsibility
  ----------------------------------- -----------------------------------
  System Administrator                Quản trị hạ tầng; không mặc nhiên
                                      có quyền sửa bút toán

  Tenant Administrator                Quản trị người dùng/cấu hình trong
                                      tenant

  Chief Accountant / Accounting Lead  Quản lý chế độ kế toán, quy tắc,
                                      kỳ, phê duyệt và báo cáo theo phân
                                      quyền

  Accountant                          Lập chứng từ, xử lý nghiệp vụ, đối
                                      chiếu theo quyền

  Approver / Manager                  Phê duyệt nghiệp vụ theo hạn
                                      mức/quy chế

  Auditor / Read-only Reviewer        Xem dữ liệu, sổ, báo cáo và audit
                                      trail; không sửa

  Integration Service                 Gửi/nhận dữ liệu qua API; không có
                                      quyền vượt qua workflow

  Compliance Owner                    Theo dõi văn bản, phê duyệt cập
                                      nhật compliance profile và evidence
  -----------------------------------------------------------------------

Phải hỗ trợ nguyên tắc phân tách nhiệm vụ (segregation of duties) ở mức
cấu hình. Một người có thể kiêm nhiều vai trò nếu chính sách đơn vị cho
phép, nhưng hệ thống phải ghi nhận rõ người thực hiện từng bước và hỗ
trợ kiểm soát xung đột quyền.

## 7. Functional requirements

### 7.1 Compliance profile and accounting configuration

  -----------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- -----------------
  FR-001            Hệ thống phải gắn MUST              Không thể ghi sổ
                    mỗi legal entity                    nếu chưa có
                    với một                             profile hợp lệ.
                    Compliance                          
                    Profile xác định                    
                    loại đơn vị, chế                    
                    độ kế toán, năm                     
                    tài chính, đồng                     
                    tiền kế toán và                     
                    ngày hiệu lực.                      

  FR-002            Hệ thống phải     MUST              Có thể xác định
                    quản lý phiên bản                   rule set nào đã
                    Rule Set theo                       áp dụng cho một
                    ngày hiệu lực,                      bút toán tại thời
                    trạng thái và                       điểm ghi sổ.
                    nguồn pháp lý.                      

  FR-003            Hệ thống phải cho MUST              Tài khoản không
                    phép cấu hình hệ                    hợp lệ/không còn
                    thống tài khoản                     hiệu lực bị chặn
                    phù hợp chế độ kế                   hoặc cảnh báo
                    toán áp dụng.                       theo chính sách.

  FR-004            Hệ thống phải     SHOULD            Báo cáo chi tiết
                    quản lý danh mục                    truy ngược được
                    đối tượng và                        về đối tượng
                    chiều phân tích                     nguồn.
                    cần thiết (khách                    
                    hàng, nhà cung                      
                    cấp, nhân viên,                     
                    kho, bộ phận, hợp                   
                    đồng...).                           

  FR-005            Thay đổi cấu hình MUST              Có audit record
                    kế toán phải có                     không thể bị
                    người thực hiện,                    người dùng thông
                    thời điểm, lý do,                   thường sửa/xóa.
                    phiên bản                           
                    trước/sau và ngày                   
                    hiệu lực.                           
  -----------------------------------------------------------------------

### 7.2 Business Event and Rule Engine

  ------------------------------------------------------------------------
  ID                Requirement        Priority          Acceptance
                                                         criteria
  ----------------- ------------------ ----------------- -----------------
  FR-010            Hệ thống phải tiếp MUST              Mỗi event có
                    nhận Business                        source system,
                    Event từ Orders và                   source ID,
                    các module được                      tenant, event
                    tích hợp.                            type, occurred-at
                                                         và correlation
                                                         ID.

  FR-011            Hệ thống phải phân MUST              Có thể thêm event
                    loại event bằng                      type bằng cấu
                    Business Event                       hình/phiên bản
                    Type/Sub-type thay                   rule mà không
                    vì tạo form nhập                     phải thay đổi UI
                    liệu riêng cho mọi                   cơ bản.
                    loại nghiệp vụ.                      

  FR-012            Rule Engine phải   MUST              Kết quả có thể
                    ánh xạ event sang                    giải thích: rule
                    đề xuất chứng                        ID, version,
                    từ/bút toán dựa                      input và tài
                    trên Compliance                      khoản/chiều Nợ-Có
                    Profile, Rule Set                    dự kiến.
                    và ngày nghiệp vụ.                   

  FR-013            Hệ thống phải kiểm MUST              Thiếu trường bắt
                    tra điều kiện dữ                     buộc hoặc tài
                    liệu và tài khoản                    khoản không hợp
                    trước khi cho phép                   lệ thì không được
                    phê duyệt/ghi sổ.                    post.

  FR-014            Event gửi lặp lại  MUST              Gửi cùng một
                    phải được nhận                       event nhiều lần
                    diện bằng                            không tạo bút
                    idempotency key                      toán trùng.
                    hoặc khóa nghiệp                     
                    vụ tương đương.                      

  FR-015            Event không xác    MUST              Có hàng đợi lỗi,
                    định được rule                       lý do và khả năng
                    phải vào trạng                       xử lý lại có kiểm
                    thái                                 soát.
                    exception/manual                     
                    review, không tự                     
                    sinh bút toán suy                    
                    đoán.                                

  FR-016            Rule Engine phải   MUST              Không làm thay
                    hỗ trợ ngày hiệu                     đổi kết quả của
                    lực và kiểm thử                      bút toán lịch sử
                    trước khi kích                       đã ghi sổ.
                    hoạt phiên bản                       
                    mới.                                 
  ------------------------------------------------------------------------

### 7.3 Voucher and journal management

  -----------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- -----------------
  FR-020            V1.0 phải bảo     MUST              Không tạo thêm
                    toàn hai luồng                      form đại trà nếu
                    nhập liệu hiện                      không có phê
                    hữu: Phiếu thu và                   duyệt thay đổi
                    Phiếu chi; Orders                   UX.
                    tiếp tục là entry                   
                    point cho nghiệp                    
                    vụ phát sinh từ                     
                    đơn hàng.                           

  FR-021            Mỗi nghiệp vụ     MUST              Có thể truy ngược
                    kinh tế, tài                        từ
                    chính thuộc phạm                    voucher/journal
                    vi kế toán phải                     đến event hoặc
                    có dữ liệu chứng                    nguồn nhập tay.
                    từ phù hợp và                       
                    liên kết nguồn.                     

  FR-022            Hệ thống phải     MUST              Không cho ghi sổ
                    kiểm tra nội dung                   khi vi phạm kiểm
                    bắt buộc, ngày                      tra bắt buộc.
                    chứng từ, ngày                      
                    hạch toán, đơn vị                   
                    tiền tệ, đối                        
                    tượng, diễn giải                    
                    và tài liệu đính                    
                    kèm theo loại                       
                    nghiệp vụ.                          

  FR-023            Hệ thống phải hỗ  MUST              Mọi chuyển trạng
                    trợ trạng thái                      thái có actor,
                    Draft → Submitted                   timestamp và kết
                    → Approved →                        quả.
                    Posted hoặc                         
                    workflow tương                      
                    đương cấu hình                      
                    được.                               

  FR-024            Bút toán kép phải MUST              Tổng Nợ = tổng Có
                    cân bằng tổng Nợ                    theo độ chính xác
                    và tổng Có trước                    tiền tệ và quy
                    khi ghi sổ.                         tắc làm tròn đã
                                                        cấu hình.

  FR-025            Chứng từ/bút toán MUST              Thử sửa/xóa trực
                    đã Posted không                     tiếp qua API bị
                    được chỉnh sửa                      từ chối và ghi
                    hoặc xóa trực                       log bảo mật.
                    tiếp qua UI/API                     
                    thông thường.                       

  FR-026            Sai sót sau khi   MUST              Dữ liệu gốc vẫn
                    Posted phải xử lý                   truy cập được;
                    bằng nghiệp vụ                      phát sinh mới có
                    điều chỉnh/đảo                      dấu vết độc lập.
                    phù hợp, có liên                    
                    kết đến chứng từ                    
                    gốc và lý do.                       

  FR-027            Hệ thống phải hỗ  SHOULD            Không cấp trùng
                    trợ đánh số chứng                   số chứng từ trong
                    từ theo quy tắc                     cùng series/phạm
                    cấu hình, tránh                     vi.
                    trùng trong phạm                    
                    vi quy định.                        

  FR-028            Hệ thống phải hỗ  SHOULD            File/tài liệu có
                    trợ đính kèm và                     checksum hoặc
                    tham chiếu tài                      metadata để phát
                    liệu nguồn theo                     hiện thay đổi nếu
                    quyền truy cập.                     phù hợp.
  -----------------------------------------------------------------------

### 7.4 Posting, ledger and reconciliation

  -----------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- -----------------
  FR-030            Ghi sổ phải là    MUST              Fault injection
                    giao dịch nguyên                    không tạo trạng
                    tử: hoặc toàn bộ                    thái ghi sổ một
                    bút toán và liên                    phần.
                    kết sổ được ghi                     
                    nhận, hoặc không                    
                    có phần nào được                    
                    ghi nhận.                           

  FR-031            Sổ kế toán phải   MUST              Draft không xuất
                    được tạo từ dữ                      hiện trong sổ
                    liệu đã ghi sổ,                     chính thức.
                    không lấy trực                      
                    tiếp từ dữ liệu                     
                    Draft.                              

  FR-032            Hệ thống phải     MUST              Số liệu sổ đối
                    cung cấp sổ cái                     chiếu được với
                    và sổ chi tiết                      journal posted.
                    theo tài                            
                    khoản/đối tượng                     
                    phù hợp chế độ kế                   
                    toán.                               

  FR-033            Hệ thống phải hỗ  MUST              Người dùng có thể
                    trợ đối chiếu                       drill-down từ số
                    giữa nguồn nghiệp                   liệu báo cáo đến
                    vụ → chứng từ →                     chứng từ và
                    bút toán → sổ →                     nguồn.
                    báo cáo.                            

  FR-034            Hệ thống phải     MUST              Có reconciliation
                    phát hiện các                       report và
                    trường hợp lệch                     exception list.
                    số, thiếu liên                      
                    kết, event chưa                     
                    xử lý, trùng                        
                    nguồn hoặc sai                      
                    tổng hợp.                           

  FR-035            Hệ thống phải hỗ  MUST              Không thể post
                    trợ khóa kỳ kế                      vào kỳ khóa nếu
                    toán và kiểm soát                   không có quyền mở
                    quyền mở lại kỳ.                    lại và lý do; mọi
                                                        lần mở lại được
                                                        audit.

  FR-036            Hệ thống phải bảo MUST              Audit timeline
                    toàn thứ tự thời                    sắp xếp được theo
                    gian của dấu vết                    timestamp và
                    sửa chữa/điều                       sequence có kiểm
                    chỉnh.                              soát.
  -----------------------------------------------------------------------

### 7.5 Output, books and reports

  -----------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- -----------------
  FR-040            Hệ thống phải     MUST              Mẫu báo cáo có
                    xuất dữ liệu sổ                     mã, phiên bản,
                    và báo cáo theo                     ngày hiệu lực và
                    biểu mẫu/chế độ                     kiểm thử công
                    kế toán đã cấu                      thức.
                    hình.                               

  FR-041            Hệ thống phải hỗ  MUST              Có ít nhất định
                    trợ xuất dữ liệu                    dạng PDF và bảng
                    ở định dạng thông                   dữ liệu
                    dụng, có thể                        (CSV/XLSX) cho
                    đọc/đối chiếu và                    các đầu ra được
                    cung cấp theo yêu                   chọn.
                    cầu hợp pháp.                       

  FR-042            Báo cáo phải hiển MUST              Không thể nhầm
                    thị phạm vi pháp                    báo cáo Draft với
                    nhân, kỳ, đơn vị                    báo cáo đã chốt.
                    tiền tệ, thời                       
                    điểm lập và trạng                   
                    thái dữ liệu.                       

  FR-043            Hệ thống phải cho SHOULD            Drill-down trả về
                    phép truy ngược                     tập dữ liệu nguồn
                    chỉ tiêu báo cáo                    xác định được.
                    đến tài khoản,                      
                    bút toán và chứng                   
                    từ cấu thành.                       

  FR-044            Hệ thống phải     MUST              Có API/export có
                    cung cấp dữ liệu                    kiểm soát, ghi
                    đầy đủ, kịp thời                    nhận người yêu
                    theo quyền và                       cầu và thời điểm.
                    phạm vi được                        
                    phép.                               

  FR-045            Hệ thống phải     MUST              Lỗi kiểm tra được
                    kiểm tra cân đối                    hiển thị, không
                    và các quan hệ                      âm thầm phát hành
                    logic giữa báo                      báo cáo sai.
                    cáo trước khi                       
                    phát hành.                          
  -----------------------------------------------------------------------

### 7.6 Audit trail and data integrity

  ------------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- ------------------
  FR-050            Hệ thống phải ghi MUST              Mỗi record có
                    audit trail cho                     actor/service
                    tạo, sửa, gửi                       identity,
                    duyệt, duyệt,                       timestamp, action,
                    post, điều chỉnh,                   entity ID, tenant,
                    đảo, khóa/mở kỳ,                    outcome.
                    thay đổi quyền và                   
                    cấu hình.                           

  FR-051            Với thay đổi dữ   MUST              Reviewer xác định
                    liệu quan trọng,                    được nội dung đã
                    audit trail phải                    thay đổi.
                    lưu được giá trị                    
                    trước/sau hoặc                      
                    bản chụp thay đổi                   
                    đủ để truy vết.                     

  FR-052            Người dùng nghiệp MUST              API/UI từ chối;
                    vụ không được tự                    thao tác bị ghi
                    sửa/xóa audit                       nhận ở security
                    trail.                              log.

  FR-053            Hệ thống phải     MUST              Có kiểm soát
                    cảnh báo hoặc                       quyền, integrity
                    ngăn chặn can                       checks và cảnh báo
                    thiệp có chủ ý                      hành vi bất
                    làm thay đổi dữ                     thường.
                    liệu đã ghi sổ.                     

  FR-054            Hệ thống phải có  MUST              Kiểm thử
                    cơ chế phát hiện                    duplicate/replay
                    trùng lặp và toàn                   và orphan
                    vẹn tham chiếu                      reference đạt.
                    giữa event,                         
                    voucher, journal                    
                    và ledger.                          

  FR-055            Có chính sách     MUST              Có biên bản
                    backup, restore                     restore test, thời
                    và kiểm thử khôi                    điểm, phạm vi và
                    phục định kỳ.                       kết quả.

  FR-056            Có thể xuất audit SHOULD            Package chứa danh
                    package cho một                     sách chứng từ,
                    kỳ hoặc một tập                     journal, log, rule
                    giao dịch phục vụ                   version và file
                    kiểm tra.                           đính kèm theo
                                                        quyền.
  ------------------------------------------------------------------------

### 7.7 Security, access control and privacy

  -----------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- -----------------
  FR-060            Hệ thống phải áp  MUST              Người dùng tenant
                    dụng RBAC theo                      A không đọc/ghi
                    tenant, legal                       dữ liệu tenant B.
                    entity, chức năng                   
                    và hành động.                       

  FR-061            Quyền xem, tạo,   MUST              Kiểm thử ma trận
                    sửa Draft, duyệt,                   quyền đạt.
                    post, điều chỉnh,                   
                    mở kỳ, xuất dữ                      
                    liệu và quản trị                    
                    phải tách biệt                      
                    cấu hình được.                      

  FR-062            Dịch vụ tích hợp  MUST              Token/service
                    phải dùng danh                      credential không
                    tính dịch vụ và                     thể thực hiện
                    scope tối thiểu.                    thao tác vượt
                                                        scope.

  FR-063            Dữ liệu nhạy cảm  MUST              TLS cho kết nối;
                    phải được bảo vệ                    mã hóa lưu
                    khi truyền và lưu                   trữ/backup theo
                    trữ theo chính                      threat model và
                    sách an toàn                        chính sách triển
                    thông tin áp                        khai.
                    dụng.                               

  FR-064            Hệ thống phải ghi MUST              Có thể truy vấn
                    log đăng nhập,                      theo user,
                    thất bại xác                        tenant, thời gian
                    thực, thay đổi                      và loại hành
                    quyền và hành                       động.
                    động nhạy cảm.                      

  FR-065            Không được để tài SHOULD            Tách quyền hạ
                    khoản quản trị hạ                   tầng và quyền kế
                    tầng mặc nhiên có                   toán; break-glass
                    quyền nghiệp vụ                     access có phê
                    để sửa bút toán                     duyệt và audit.
                    đã ghi sổ.                          
  -----------------------------------------------------------------------

### 7.8 Integration and upgradeability

  ------------------------------------------------------------------------
  ID                Requirement       Priority          Acceptance
                                                        criteria
  ----------------- ----------------- ----------------- ------------------
  FR-070            Hệ thống phải có  MUST              Có tài liệu API,
                    API/contract được                   version, auth,
                    version hóa để                      retry và error
                    kết nối phần mềm                    contract.
                    hóa đơn điện tử                     
                    và chữ ký số.                       

  FR-071            Hệ thống phải hỗ  SHOULD            Có thể thay
                    trợ tích hợp qua                    adapter mà không
                    adapter, không để                   thay đổi ledger
                    phụ thuộc một nhà                   contract.
                    cung cấp làm thay                   
                    đổi core                            
                    accounting model.                   

  FR-072            Hệ thống phải lưu MUST              Có thể đối chiếu
                    external                            chứng từ nội bộ
                    reference, trạng                    với chứng từ bên
                    thái đồng bộ và                     ngoài.
                    lỗi tích hợp.                       

  FR-073            Hệ thống phải hỗ  MUST              Migration có
                    trợ nâng cấp Rule                   version,
                    Set, report                         rollback/forward
                    template và                         plan và kiểm thử
                    schema có kiểm                      dữ liệu.
                    soát.                               

  FR-074            Mọi thay đổi do   MUST              Có trace từ văn
                    văn bản pháp luật                   bản → yêu cầu →
                    mới phải có                         rule/report → test
                    impact assessment                   case → release.
                    và regression                       
                    test.                               
  ------------------------------------------------------------------------

## 8. Non-functional requirements

  ------------------------------------------------------------------------------
  ID                Requirement             Priority          Acceptance
                                                              criteria
  ----------------- ----------------------- ----------------- ------------------
  NFR-001           **Reliability:** Ghi sổ MUST              Không mất hoặc
                    phải atomic, idempotent                   nhân đôi bút toán
                    và có thể phục hồi sau                    trong test
                    lỗi.                                      retry/failure.

  NFR-002           **Auditability:** Audit MUST              QA/Reviewer thực
                    trail phải có khả năng                    hiện được audit
                    truy vấn, xuất và kiểm                    scenario
                    tra tính liên tục.                        end-to-end.

  NFR-003           **Performance:** Tác vụ SHOULD            Benchmark được ghi
                    ghi sổ và tra cứu phải                    lại; không đặt
                    đáp ứng SLA được                          ngưỡng pháp lý giả
                    PO/Architect phê duyệt                    định trong SRS.
                    theo quy mô triển khai.                   

  NFR-004           **Scalability:** Hỗ trợ SHOULD            Load test và
                    mở rộng theo tenant và                    tenant isolation
                    khối lượng giao dịch mà                   test đạt ngưỡng
                    không phá vỡ tính cô                      release.
                    lập.                                      

  NFR-005           **Availability:** Có    SHOULD            Có dashboard và
                    health check,                             runbook vận hành.
                    monitoring, alerting và                   
                    quy trình xử lý sự cố.                    

  NFR-006           **Recoverability:**     MUST              Có backup
                    RPO/RTO phải được chủ                     schedule, restore
                    sản phẩm xác định theo                    test và phê duyệt
                    mô hình triển khai và                     RPO/RTO.
                    rủi ro.                                   

  NFR-007           **Maintainability:**    MUST              Có thể phát hành
                    Rule, biểu mẫu và                         rule version mà
                    mapping phải có cấu                       không sửa dữ liệu
                    hình/phiên bản, hạn chế                   lịch sử.
                    sửa code cho thay đổi                     
                    thông thường.                             

  NFR-008           **Interoperability:**   MUST              Có contract tests
                    API, export và data                       và version policy.
                    contract phải được tài                    
                    liệu hóa.                                 

  NFR-009           **Observability:** Log, SHOULD            Correlation ID
                    metric, trace phải hỗ                     xuyên suốt
                    trợ truy vết xuyên                        event-to-ledger.
                    module và tenant.                         

  NFR-010           **Usability:** Không    MUST              PO phê duyệt mọi
                    làm phức tạp UX nhập                      thay đổi
                    liệu hiện hữu nếu có                      form/entry point.
                    thể xử lý bằng                            
                    event/rule.                               
  ------------------------------------------------------------------------------

## 9. Data model -- minimum conceptual entities

Các entity dưới đây là khái niệm logic, không áp đặt tên bảng hoặc ORM
cụ thể:

-   `LegalEntity`
-   `ComplianceProfile`
-   `AccountingPeriod`
-   `Account`
-   `AccountingDimension`
-   `BusinessEvent`
-   `BusinessEventType`
-   `AccountingRuleSet`
-   `AccountingRule`
-   `Voucher`
-   `VoucherLine`
-   `JournalEntry`
-   `JournalLine`
-   `LedgerProjection` (nếu dùng projection; phải tái tạo/đối chiếu được
    từ posted journal)
-   `Approval`
-   `AuditEvent`
-   `IntegrationEndpoint`
-   `ExternalDocumentReference`
-   `ReconciliationRun`
-   `AttachmentMetadata`

### 9.1 Data integrity rules

1.  Mọi entity kế toán phải gắn `tenant_id` và `legal_entity_id` khi phù
    hợp.
2.  Mọi Business Event phải có định danh nguồn ổn định và idempotency
    key.
3.  Journal đã Posted phải bất biến đối với thao tác nghiệp vụ thông
    thường.
4.  Mọi điều chỉnh/đảo phải tham chiếu bản ghi gốc.
5.  Ledger/report là kết quả từ dữ liệu đã Posted; không tổng hợp Draft
    vào số liệu chính thức.
6.  Không xóa vật lý chứng từ/bút toán đã ghi sổ bằng thao tác ứng dụng
    thông thường.
7.  Thay đổi Rule Set không được tính lại âm thầm các giao dịch lịch sử.
8.  Tất cả timestamp phải lưu nhất quán; giao diện hiển thị theo
    timezone cấu hình của tenant.
9.  Số tiền phải dùng kiểu dữ liệu decimal phù hợp, không dùng floating
    point nhị phân cho tính toán kế toán.
10. Việc làm tròn phải có quy tắc được cấu hình và kiểm thử.

## 10. Core state machine

### 10.1 Voucher lifecycle

``` text
Draft
  -> Submitted
  -> Approved
  -> Posted

Draft/Submitted
  -> Rejected
  -> Draft (after correction)

Posted
  -> Adjustment / Reversal (new linked transaction)
```

-   Không có transition `Posted -> Draft`.
-   Không có transition `Posted -> Deleted`.
-   Nếu cần hủy theo quy định nghiệp vụ, phải dùng nghiệp vụ hủy/điều
    chỉnh có dấu vết và không xóa lịch sử.
-   Các trạng thái cụ thể có thể cấu hình theo workflow nhưng không được
    làm mất audit trail.

### 10.2 Business Event lifecycle

``` text
Received -> Validated -> Classified -> RuleMatched
                                  -> ManualReview
RuleMatched -> VoucherDraft -> Approval -> Posted
Any processing state -> Failed/Quarantined -> Retry/ManualResolution
```

Retry phải idempotent. Không tự động post chỉ vì event đã được nhận hoặc
rule đã match.

## 11. Security and operational controls

### 11.1 Minimum controls

-   Tenant isolation ở API, service, database query và background
    worker.
-   Least privilege; không dùng chung tài khoản người dùng.
-   Secret management; không lưu credentials tích hợp dạng plaintext.
-   TLS cho kết nối.
-   Backup được bảo vệ và kiểm tra khả năng restore.
-   Audit log được giới hạn quyền truy cập và bảo vệ khỏi sửa trái phép.
-   Có quy trình break-glass cho tình huống khẩn cấp; mọi lần sử dụng
    phải được phê duyệt/hậu kiểm.
-   Không cho phép thao tác trực tiếp trên database trở thành quy trình
    sửa số liệu kế toán bình thường.

### 11.2 Edge/offline synchronization

Với mô hình Edge ShopERP + CoreHub: - Mỗi event cần UUID/ID ổn định,
source device, tenant, sequence/version và occurred-at. - Đồng bộ theo
cơ chế at-least-once phải đi kèm idempotency ở CoreHub. - Không dùng
timestamp đơn lẻ để quyết định ghi đè dữ liệu kế toán. - Xung đột liên
quan đến dữ liệu đã Posted phải đưa vào exception queue; không tự
merge. - Kỳ đã khóa tại CoreHub phải được kiểm tra lại khi Edge gửi dữ
liệu. - Audit trail phải chỉ ra thao tác được tạo ở Edge hay CoreHub và
thời điểm đồng bộ.

## 12. Acceptance test suite

Các test case sau là baseline tối thiểu. QA có thể bổ sung test case
nhưng không được bỏ các MUST test khi chưa có phê duyệt.

  --------------------------------------------------------------------------------------
  Test ID           Scenario          Expected result                  Maps to
  ----------------- ----------------- -------------------------------- -----------------
  TC-001            Tenant A gọi API  Bị từ chối; có security log      FR-060
                    đọc dữ liệu                                        
                    Tenant B                                           

  TC-002            Gửi lặp cùng      Chỉ một nghiệp vụ kế toán được   FR-014
                    Business Event 3  tạo                              
                    lần                                                

  TC-003            Rule không match  Chuyển ManualReview; không tự    FR-015
                    event             post                             

  TC-004            Journal có tổng   Không cho post                   FR-024
                    Nợ khác tổng Có                                    

  TC-005            Post journal      Journal và ledger commit nguyên  FR-030
                    thành công        tử                               

  TC-006            Gửi lại request   Không tạo bút toán thứ hai       FR-014, NFR-001
                    post sau timeout                                   

  TC-007            Người dùng sửa    Bị từ chối; hướng dẫn điều chỉnh FR-025
                    journal đã Posted                                  
                    qua UI                                             

  TC-008            Người dùng gọi    Bị từ chối; ghi security log     FR-025, FR-052
                    API xóa journal                                    
                    đã Posted                                          

  TC-009            Tạo adjustment    Tạo record mới, liên kết bản     FR-026
                    cho journal đã    gốc, giữ nguyên lịch sử          
                    Posted                                             

  TC-010            Sửa dữ liệu Draft Audit theo chính sách; chưa ảnh  FR-050
                                      hưởng ledger                     

  TC-011            Mở lại kỳ đã khóa Chỉ role có quyền mới thực hiện; FR-035
                                      bắt buộc lý do; audit            

  TC-012            Post vào kỳ đang  Bị chặn                          FR-035
                    khóa                                               

  TC-013            Xuất sổ theo kỳ   Số liệu khớp journal Posted      FR-032, FR-041

  TC-014            Drill-down từ báo Truy được đến                    FR-033, FR-043
                    cáo               account/journal/voucher/source   

  TC-015            Rule version mới  Giao dịch lịch sử giữ nguyên     FR-002, FR-016
                    được kích hoạt    rule version cũ                  

  TC-016            Thay đổi          Có người thực hiện, lý do,       FR-005, FR-073
                    rule/template     version, effective date          

  TC-017            CoreHub nhận      Idempotent; không ghi đè journal FR-014, NFR-001
                    event offline gửi đã Posted                        
                    lại                                                

  TC-018            Lỗi xảy ra giữa   Rollback toàn bộ; không có       FR-030
                    transaction       ledger partial                   
                    posting                                            

  TC-019            Backup restore    Phục hồi dữ liệu và đối chiếu    FR-055
                    drill             được số lượng/tổng kiểm soát     

  TC-020            Yêu cầu export dữ Trả đủ dữ liệu theo scope, ghi   FR-044
                    liệu của người có nhận request                     
                    quyền                                              

  TC-021            API integration   Từ chối; không lộ dữ liệu; ghi   FR-062
                    thiếu credential  log                              
                    hợp lệ                                             

  TC-022            Thử thay đổi      Bị từ chối                       FR-052
                    audit record bằng                                  
                    user nghiệp vụ                                     

  TC-023            Phát hành report  Version mới có hiệu lực đúng     FR-040, FR-073
                    template mới      ngày; báo cáo cũ giữ phiên bản   

  TC-024            Thiếu trường bắt  Không được approve/post          FR-022
                    buộc của voucher                                   

  TC-025            Event từ nguồn có Không tạo chứng từ trùng         FR-014, FR-054
                    external                                           
                    reference đã xử                                    
                    lý                                                 
  --------------------------------------------------------------------------------------

### 12.1 Release gate

Release không được coi là đạt Legal Compliance v1.0 nếu: - Có bất kỳ
MUST requirement nào chưa triển khai hoặc chưa có test evidence. - Có
test MUST thất bại. - Có lỗ hổng tenant isolation hoặc khả năng sửa/xóa
journal đã Posted bằng đường nghiệp vụ thông thường. - Không xuất được
sổ/báo cáo thuộc phạm vi release. - Không truy vết được một giao dịch từ
nguồn đến báo cáo. - Chưa có người chịu trách nhiệm xác nhận chế độ kế
toán và rule set áp dụng.

Các ngoại lệ phải có risk acceptance bằng văn bản của Product Owner và
Compliance Owner; ngoại lệ không được dùng để miễn trừ nghĩa vụ pháp
luật.

## 13. Traceability matrix -- legal obligation to system control

  -----------------------------------------------------------------------
  Legal source      Obligation        System            Evidence
                                      requirement       
  ----------------- ----------------- ----------------- -----------------
  TT 99/2025, Art.  Nghiệp vụ tuân    FR-001--016,      Compliance
  28.1(a)           thủ luật; không   FR-040            profile, rule
                    thay đổi bản                        catalogue,
                    chất/phương pháp                    accounting test
                    kế toán                             cases

  TT 99/2025, Art.  Chính xác, phù    FR-014,           Duplicate tests,
  28.1(b)           hợp, không trùng; FR-024--036,      audit timeline,
                    sửa chữa có dấu   FR-050--054       reconciliation
                    vết theo thời                       reports
                    gian                                

  TT 99/2025, Art.  Bảo mật/an toàn;  FR-050--065,      RBAC tests,
  28.1(c)           cảnh báo hoặc     NFR-001/002       security logs,
                    ngăn chặn can                       integrity tests
                    thiệp có chủ ý                      

  TT 99/2025, Art.  Cung cấp dữ liệu  FR-040--045       Export samples,
  28.1(d)           đầu ra đầy đủ,                      report
                    kịp thời                            validation,
                                                        access logs

  TT 99/2025, Art.  Kết nối hoặc sẵn  FR-070--072       API
  28.1(dd)          sàng kết nối phần                   specifications,
                    mềm liên quan                       contract tests,
                                                        integration logs

  TT 99/2025, Art.  Có thể nâng       FR-002, FR-016,   Versioned rules,
  28.1(e)           cấp/sửa đổi/bổ    FR-073--074       migration tests,
                    sung khi pháp                       legal change
                    luật thay đổi                       register

  TT 99/2025, Art.  Trách nhiệm của   FR-050,           User/action
  28.2              người quản lý, kế FR-060--065;      records, approval
                    toán trưởng/phụ   Section 6         workflow,
                    trách và người                      responsibility
                    liên quan                           matrix

  TT                Tiêu chuẩn/điều   FR-020--045,      Functional test
  103/2005/TT-BTC   kiện phần mềm kế  FR-050--075       pack and product
                    toán, đọc cùng                      documentation
                    pháp luật hiện                      
                    hành                                
  -----------------------------------------------------------------------

## 14. Required compliance evidence package

Mỗi release cần lưu một Compliance Evidence Package tối thiểu gồm:

1.  Danh sách văn bản pháp lý và ngày kiểm tra hiệu lực.
2.  Compliance Profile và Rule Set được phát hành.
3.  Danh mục tài khoản, mẫu chứng từ, sổ và báo cáo áp dụng.
4.  Mapping từ điều khoản pháp lý đến yêu cầu phần mềm.
5.  Kết quả test case và bằng chứng chạy test.
6.  Mẫu output: chứng từ, sổ, báo cáo, dữ liệu export.
7.  Ma trận phân quyền và kết quả tenant isolation test.
8.  Mẫu audit trail cho tạo/sửa/duyệt/post/điều chỉnh/mở khóa kỳ.
9.  Backup policy và kết quả restore drill.
10. API/interface specification cho tích hợp.
11. Release notes, migration plan và rollback/forward plan.
12. Phê duyệt của Product Owner, Accounting Domain Lead và Compliance
    Owner.

## 15. Implementation plan

### Phase 0 -- Discovery and legal confirmation

-   Kiểm tra base code hiện tại.
-   Xác định chế độ kế toán và đối tượng áp dụng cho từng tenant.
-   Đối chiếu Điều 28 TT 99 và các yêu cầu liên quan trong TT 103.
-   Lập gap analysis: Existing / Modify / Missing / Incorrect / Remove.
-   Không viết lại module trước khi có kết quả đối chiếu.

**Exit criteria:** Approved legal baseline, current-state architecture
map, gap register.

### Phase 1 -- Integrity foundation

-   Tenant/legal entity boundary.
-   Business Event identity và idempotency.
-   Voucher/journal lifecycle.
-   Atomic posting.
-   Posted immutability và adjustment/reversal.
-   Audit trail.

**Exit criteria:** TC-001 đến TC-012, TC-018, TC-022 đạt.

### Phase 2 -- Accounting rules and books

-   Compliance Profile.
-   Versioned Rule Engine.
-   Account mapping.
-   General ledger/sub-ledger.
-   Reconciliation.
-   Period close/lock.

**Exit criteria:** TC-013 đến TC-017, TC-024, TC-025 đạt.

### Phase 3 -- Output and integration readiness

-   Sổ/báo cáo.
-   Export.
-   API contracts.
-   E-invoice/digital-signature adapters.
-   Upgrade/migration framework.

**Exit criteria:** TC-014, TC-020, TC-023 đạt; evidence package được
tạo.

### Phase 4 -- Release compliance review

-   Regression test.
-   Security review.
-   Restore drill.
-   Legal change review.
-   Accounting Domain Lead sign-off.
-   Product Owner sign-off.

**Exit criteria:** Không còn MUST gap chưa xử lý; có release evidence
đầy đủ.

## 16. Devin implementation instructions

Devin phải tuân thủ các chỉ dẫn sau khi làm việc với codebase:

1.  Đọc toàn bộ tài liệu này và kiểm tra codebase thực tế trước khi đề
    xuất sửa.
2.  Không giả định chức năng đã tồn tại chỉ vì có model/API/UI tương tự;
    phải truy vết end-to-end.
3.  Lập bảng đối chiếu từng FR/NFR: `Existing`, `Modify`, `Missing`,
    `Incorrect`, `Not Applicable`, kèm file/class/API/database/test
    evidence.
4.  Bảo toàn UX hiện tại gồm Phiếu thu, Phiếu chi và Orders entry point.
    Không tự tạo thêm form nhập chứng từ nếu chưa có quyết định thay đổi
    UX.
5.  Ưu tiên bổ sung Business Event Type/Sub-type, Rule Engine và Event
    Handler thay vì hard-code logic vào UI.
6.  Không dùng stub, mock, fake success, bypass validation hoặc chỉ tạo
    giao diện để đánh dấu hoàn thành.
7.  Không dùng `EnsureCreated` trong production; migration phải có
    version và được kiểm thử.
8.  Không sửa/xóa dữ liệu Posted để làm test pass. Dùng fixture/test
    database và nghiệp vụ điều chỉnh đúng mô hình.
9.  Mỗi MUST requirement phải có test tự động hoặc test thủ công có
    evidence rõ ràng.
10. Không tự diễn giải yêu cầu pháp luật vượt quá tài liệu; điểm chưa rõ
    phải ghi `LEGAL_CLARIFICATION_REQUIRED`.
11. Không mở rộng phạm vi sang hộ kinh doanh/HTX nếu chưa có compliance
    profile và legal baseline riêng.
12. Trước khi code, báo cáo kiến trúc hiện tại, gap analysis, rủi ro
    migration và kế hoạch theo phase; chỉ triển khai sau khi người phụ
    trách phê duyệt kế hoạch.

### Required Devin deliverables

-   `compliance_gap_analysis.md`
-   `accounting_compliance_traceability.md`
-   `accounting_test_plan.md`
-   `accounting_migration_plan.md` (nếu có thay đổi schema/data)
-   Code changes with tests
-   `compliance_evidence/` containing test outputs and sample exports
-   `implementation_report.md` with each FR/NFR status and evidence
    references

## 17. Open issues requiring owner decision

  ----------------------------------------------------------------------------
  ID                Issue              Owner                 Required decision
  ----------------- ------------------ --------------------- -----------------
  OI-001            Phạm vi doanh      Product Owner +       Chốt compliance
                    nghiệp nào được hỗ Accounting Lead       profile
                    trợ ở release đầu                        
                    (SME, siêu nhỏ,                          
                    lĩnh vực đặc thù)                        

  OI-002            Năm tài chính,     Customer/Accounting   Cấu hình theo
                    đồng tiền kế toán, Lead                  từng legal entity
                    phương pháp tính                         
                    giá và các chính                         
                    sách kế toán                             

  OI-003            Danh mục báo       Accounting Lead       Chốt report
                    cáo/sổ cần phát                          catalogue
                    hành trong release                       
                    đầu                                      

  OI-004            Phê duyệt chứng từ Product Owner +       Chốt workflow
                    và hạn mức         Customer              

  OI-005            RPO/RTO và         Architect +           Chốt theo rủi ro
                    retention/backup   Operations + Legal    và nghĩa vụ lưu
                    policy                                   trữ

  OI-006            Adapter hóa đơn    Product + Integration Chọn nhà cung cấp
                    điện tử/chữ ký số  Lead                  và API
                    cụ thể                                   

  OI-007            Phạm vi áp dụng TT Compliance            Lập legal
                    103/2005 sau các   Owner/Legal Counsel   interpretation
                    thay đổi pháp luật                       note

  OI-008            SRS riêng cho hộ   Product Owner +       Phê duyệt
                    kinh doanh và HTX  Legal + Accounting    baseline riêng
                                       Lead                  
  ----------------------------------------------------------------------------

## 18. Assumptions and limitations

-   SRS này là baseline yêu cầu sản phẩm, không phải chứng nhận phù hợp
    pháp luật.
-   Các yêu cầu MUST phản ánh nghĩa vụ pháp lý được xác định hoặc biện
    pháp kiểm soát cần thiết để chứng minh/triển khai nghĩa vụ đó; bảng
    traceability chỉ rõ nguồn.
-   Một số yêu cầu kỹ thuật như atomic transaction, idempotency,
    immutable posting, RBAC chi tiết là thiết kế kiểm soát được đề xuất
    để đáp ứng tính chính xác, không trùng lặp, an toàn và truy vết;
    không khẳng định từng thuật ngữ này được ghi nguyên văn trong luật.
-   Việc lựa chọn mẫu sổ, tài khoản, báo cáo, phương pháp kế toán và
    thời hạn lưu trữ phải được xác nhận theo loại đơn vị, lĩnh vực, năm
    tài chính và văn bản hiệu lực.
-   Cần rà soát pháp lý trước khi phát hành thương mại và mỗi khi có
    thay đổi quy định.

## 19. Official and reference sources

1.  **Công báo Chính phủ -- Thông tư 99/2025/TT-BTC:**\
    https://congbao.chinhphu.vn/van-ban/thong-tu-so-99-2025-tt-btc-46529/59631.htm
2.  **Bản PDF Công báo Chính phủ -- Thông tư 99/2025/TT-BTC:**\
    https://congbao.chinhphu.vn/tai-ve-van-ban-so-99-2025-tt-btc-46529-59631?format=pdf
3.  **Điều 28 Thông tư 99/2025/TT-BTC -- bản tra cứu:**\
    https://vanban.org/van-ban/thong-tu-99-2025-tt-btc-che-do-ke-toan-doanh-nghiep/dieu-28
4.  **CSDL quốc gia về VBPL -- Thông tư 103/2005/TT-BTC:**\
    https://vbpl.moj.gov.vn/bocongthuong/Pages/vbpq-luocdo.aspx?ItemID=17130
5.  **Bộ Công Thương -- thông tin văn bản Thông tư 103/2005/TT-BTC:**\
    https://moit.gov.vn/van-ban-phap-luat/van-ban-phap-quy/-thong-tu-cua-bo-tai-chinh-huong-dan-tieu-chuan-va-dieu-kien.html
6.  **Bộ Tài chính -- Hỏi đáp về thời điểm áp dụng TT 99/2025/TT-BTC:**\
    https://portal.mof.gov.vn/hoidapcstc/home/cthoidap/159555

------------------------------------------------------------------------

## Appendix A -- Requirement priority definition

-   **MUST:** Bắt buộc cho phạm vi release này; không đạt thì không được
    tuyên bố release đáp ứng SRS.
-   **SHOULD:** Rất nên có; có thể hoãn nếu có lý do, owner và kế hoạch
    xử lý.
-   **COULD:** Có thể cân nhắc; chưa được đưa vào baseline v1.0.

## Appendix B -- Definition of done

Một requirement chỉ được đánh dấu `DONE` khi đồng thời: 1. Có
implementation thực sự trong luồng end-to-end. 2. Có automated test hoặc
manual test evidence phù hợp. 3. Không có stub/bypass/fake success. 4.
Có logging/audit phù hợp. 5. Có tài liệu cấu hình/vận hành nếu cần. 6.
Có traceability đến FR/NFR và legal source. 7. Đã được reviewer độc lập
kiểm tra.
