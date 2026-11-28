# SRS -- VẠN AN HYBRID CLOUD & PRIVATE CLOUD INFRASTRUCTURE

**Phiên bản:** 1.1\
**Trạng thái:** Architecture Assessment / Proposal -- Chưa được phép
triển khai\
**Đối tượng thực hiện:** Devin / Engineering Team\
**Hệ thống:** Vạn An Ecosystem\
**Mục tiêu tài liệu:** Yêu cầu Devin kiểm tra codebase và hạ tầng Google
Cloud hiện tại, đánh giá hai phương án kiến trúc Private Cloud và Hybrid
Cloud, sau đó đề xuất kiến trúc phù hợp dựa trên bằng chứng thực tế.

**Thay đổi v1.1 (2026-11-27 — đồng bộ hiện trạng thực tế từ codebase):**
danh mục service đầy đủ 9 service (bỏ "Sync Worker" độc lập — sync chạy
in-process qua NATS, xem §2.1), bổ sung bảng hiện trạng đã xác minh
(§3.3), và mục kiểm tra PostgreSQL backup/PITR bắt buộc ưu tiên số 1
(§3.2.1).

------------------------------------------------------------------------

## 1. Bối cảnh và mục tiêu

Vạn An hiện đang vận hành một phần hạ tầng trên Google Cloud. Chủ dự án
muốn đánh giá khả năng xây dựng hạ tầng Private Cloud tự quản lý bằng
máy chủ vật lý, Proxmox VE, Ubuntu VM và Docker; đồng thời xem xét mô
hình Hybrid Cloud kết hợp Google Cloud với Private Cloud.

Tài liệu này không mặc định rằng tự host sẽ rẻ hơn, an toàn hơn hoặc
đáng tin cậy hơn Google Cloud. Devin phải kiểm tra hệ thống thực tế, đo
lường các ràng buộc kỹ thuật và vận hành, rồi mới đưa ra khuyến nghị.

### 1.1 Mục tiêu

1.  Kiểm kê codebase, dịch vụ, phụ thuộc và cách triển khai hiện tại.
2.  Kiểm tra hạ tầng Google Cloud đang sử dụng, đặc biệt là vị trí và
    tình trạng vận hành của PostgreSQL.
3.  Đánh giá hai phương án:
    -   **Phương án A -- Private Cloud:** Vạn An tự quản lý máy chủ vật
        lý, Proxmox VE, Ubuntu VM và Docker.
    -   **Phương án B -- Hybrid Cloud:** Google Cloud tiếp tục chạy các
        workload phù hợp; Private Cloud đảm nhiệm các workload được xác
        định rõ như development, staging, backup, disaster recovery hoặc
        dịch vụ nội bộ.
4.  Đề xuất vị trí triển khai phù hợp cho từng service, không ép buộc
    mọi service vào một mô hình duy nhất.
5.  Xây dựng phương án bảo mật, sao lưu, phục hồi thảm họa (DR), giám
    sát, mở rộng và kiểm soát chi phí.
6.  Đưa ra lộ trình theo giai đoạn, có tiêu chí nghiệm thu và điều kiện
    dừng/rollback.
7.  Bảo đảm có thể tiếp tục phát triển hệ thống mà không tạo ra phụ
    thuộc không cần thiết vào một nhà cung cấp hạ tầng.

### 1.2 Nguyên tắc bắt buộc

-   **Audit trước, thiết kế sau, triển khai cuối cùng.**
-   Không suy đoán cấu hình hiện tại nếu chưa kiểm tra được.
-   Phân biệt rõ: Verified Fact, Inference, Assumption, Recommendation
    và Open Question.
-   Không được giản lược tính năng hoặc thay đổi kiến trúc ứng dụng chỉ
    để dễ triển khai hạ tầng.
-   Không được tự ý thay đổi production, firewall, DNS, database,
    secrets, IAM, routing, deployment pipeline hoặc dữ liệu.
-   Không được chạy migration, upgrade, restart hoặc destructive command
    trên production trong giai đoạn đánh giá.
-   Không được đưa credentials, private keys, access tokens hoặc dữ liệu
    nhạy cảm vào tài liệu đầu ra.
-   Mọi đề xuất phải xét đến khả năng vận hành thực tế của đội ngũ Vạn
    An.
-   Không coi snapshot hoặc bản sao lưu trên cùng một máy chủ là chiến
    lược backup đầy đủ.
-   Không coi việc đang sử dụng Google Cloud đồng nghĩa hệ thống đã có
    High Availability hoặc Disaster Recovery.

------------------------------------------------------------------------

## 2. Phạm vi đánh giá

### 2.1 Phạm vi trong

Devin cần rà soát tối thiểu các nhóm sau, nếu tồn tại trong codebase
hoặc hạ tầng:

  -----------------------------------------------------------------------
  Nhóm                                Thành phần cần kiểm tra
  ----------------------------------- -----------------------------------
  Core platform                       CoreHub (in-process), Gateway
                                      (YARP + Order Creator), API,
                                      authentication/authorization

  Merchant applications               ShopERP (Blazor Server), KhachLink
                                      (Blazor WASM + nginx), Directory
                                      (Blazor SSR)

  Edge tooling                        Crawler (VanAn.Crawler —
                                      doanhnghiep.vn / GDT lookup)

  Background processing               NatsSyncWorker + Outbox (in-process
                                      trong Gateway), DataSyncSubscriber
                                      (Gateway), OrderSyncSubscriber /
                                      TenantSyncSubscriber (ShopERP) —
                                      KHÔNG phải service deploy riêng

  AI                                  TenantAI (vanan-ai, pgvector),
                                      Gemini / Google Vision OCR,
                                      Gemini Live (voice AI)

  Voice/Realtime                      VanAn Voice (vanan-voice), WebRTC
                                      signaling, coturn TURN

  Metering                            vanan-metering (phí nền tảng),
                                      usage metering, wallet/topup

  Data                                PostgreSQL 15 (Gateway PG — source
                                      of truth), SQLite per-tenant
                                      (ShopERP), NATS JetStream, Redis,
                                      pgvector, R2 object storage

  Integration                         Payment (VietQR), e-invoice,
                                      notification (Brevo), external APIs
                                      (GoDaddy, doanhnghiep.vn...)

  Delivery                            Dockerfiles, Docker Compose
                                      (1 stack/VPS), GitHub Actions CD
                                      (cd-multivps.yml → GHCR → SSH)

  Operations                          Seq (central log), health checks,
                                      prod-smoke, RV protocol

  Security                            Secrets (GitHub + .env.* per VPS),
                                      TLS (nginx/certbot), firewall,
                                      backup access
  -----------------------------------------------------------------------

Không được mặc định mọi thành phần trên đều đã tồn tại hoặc đang được
triển khai. Hãy ghi trạng thái thực tế cho từng thành phần.

### 2.2 Ngoài phạm vi

-   Không triển khai kiến trúc mới trong đợt audit này.
-   Không di chuyển production workload.
-   Không mua hoặc cấu hình máy chủ vật lý.
-   Không thay đổi source code nghiệp vụ, API contract hoặc database
    schema.
-   Không thay thế nhà cung cấp cloud hoặc dịch vụ bên ngoài.
-   Không cam kết RPO/RTO nếu chưa chứng minh được bằng thiết kế và thử
    nghiệm.

------------------------------------------------------------------------

## 3. Kiểm kê hiện trạng (Current-State Audit)

### 3.1 Codebase audit

Đọc cấu trúc repository và xác định:

-   Các service/module thực sự có trong codebase.
-   Ngôn ngữ, framework, runtime và phiên bản.
-   Dockerfile, Compose files, scripts và deployment manifests.
-   Các port, protocol và network dependency.
-   Environment variables và secrets references (chỉ ghi tên biến, không
    ghi giá trị).
-   Database connections, migrations, schema ownership và data
    dependencies.
-   Queue, cache, object storage và external API dependencies.
-   Health checks, readiness/liveness checks.
-   Logging, metrics, tracing và error reporting.
-   Persistent volumes, local file writes và dữ liệu cần bảo tồn.
-   Cơ chế startup, shutdown, retry, idempotency và graceful
    degradation.
-   Các giả định về hostname, IP tĩnh, DNS, TLS hoặc cloud metadata.
-   Những phần đang phụ thuộc đặc thù Google Cloud.

### 3.2 GCP audit

Kiểm tra bằng quyền read-only nếu có thể:

-   Project, region/zone và resource inventory.
-   Compute Engine VM, machine type, OS, disk type/size.
-   VPC, subnet, firewall rules, routes, external/internal IP.
-   Load balancer, DNS, TLS certificate và ingress.
-   Cloud Storage hoặc object storage đang dùng.
-   Cloud Logging/Monitoring, alerting và uptime checks.
-   IAM/service accounts và quyền truy cập ở mức cần thiết.
-   Snapshot policies, backup jobs, retention và vị trí lưu bản sao.
-   Chi phí theo nhóm dịch vụ nếu có dữ liệu billing.
-   Deployment method và cách rollback hiện tại.

**Đặc biệt với PostgreSQL:**

-   PostgreSQL đang chạy trên VM, managed service hay container?
-   Phiên bản PostgreSQL, dung lượng dữ liệu, tốc độ tăng trưởng.
-   Disk layout, filesystem, IOPS/throughput nếu có.
-   Cấu hình WAL, archive mode, replication và checkpoint.
-   Backup hiện tại: loại backup, lịch chạy, retention, nơi lưu.
-   Có PITR (Point-in-Time Recovery) hay không?
-   Backup có nằm ngoài VM/zone/project hoặc ngoài GCP hay không?
-   Lần restore test gần nhất và bằng chứng kết quả.
-   RPO/RTO hiện có được đo lường hay chỉ là giả định?
-   Database có bị truy cập trực tiếp từ Internet không?
-   Ứng dụng nào là database owner và các service nào đang truy cập?

### 3.2.1 PostgreSQL Backup Verification -- BẮT BUỘC, ƯU TIÊN SỐ 1

Bằng chứng từ codebase (2026-11-27): PostgreSQL 15 chạy dưới dạng
container `postgres:15-alpine` trên VM `vanan-gateway` (GCP
asia-southeast1-a) với volume `postgres_data` (`docker-compose.gateway.yml`).
Trong repository KHÔNG có cấu hình WAL archiving, pg_dump/pg_basebackup
cron, snapshot policy hoặc restore test — trạng thái backup production
hiện tại là **Unknown / chưa xác minh**. Vì Gateway PG là source of truth
cho Orders + Accounting + Tenants + ShopInstances + Users, đây là rủi ro
dữ liệu lớn nhất của toàn hệ thống.

Checklist bắt buộc (thực hiện ở Phase 0, quyền read-only trên VM):

-   [ ] Backup hiện tại trên VM gateway: pg_dump cron? pg_basebackup?
        GCP disk snapshot? Hay không có gì?
-   [ ] WAL level trong container (`archive_mode`, `archive_command`) —
        nếu OFF thì KHÔNG có PITR.
-   [ ] Vị trí lưu backup: cùng VM? cùng zone? offsite? (đối chiếu
        nguyên tắc 3-2-1-1-0, SRS §7.1).
-   [ ] Retention và quyền xóa backup (phải tách khỏi quyền vận hành
        production).
-   [ ] Lần restore test gần nhất + bằng chứng kết quả (không có → ghi
        rõ "chưa từng restore test").
-   [ ] Firewall rule port 5432 trên GCP: chỉ cho phép nguồn nội bộ/VPC,
        KHÔNG được public Internet (đối chiếu SRS §6.4).
-   [ ] Danh sách service truy cập PG: Gateway, ShopERP (qua VPC
        internal), TenantAI?, migration tooling...
-   [ ] RPO/RTO hiện tại được đo lường hay chỉ là giả định?

Kết quả mục này là **điều kiện gate của Phase 0** (SRS §14): không chuyển
phase nếu chưa xác định được trạng thái backup và kế hoạch PITR.

Không được kết luận "đã có backup" chỉ vì nhìn thấy snapshot hoặc một
file dump. Phải xác định tính nhất quán, khả năng phục hồi và bằng chứng
restore.

### 3.3 Bảng hiện trạng bắt buộc

Bảng hiện trạng bắt buộc — đã điền hiện trạng xác minh từ codebase
(2026-11-27, bổ sung v1.1):

  --------------------------------------------------------------------------------------------------------------------
  Service/Resource   Exists?   Current Host        Runtime             Data Store           Dependencies          Public        Backup        Criticality   Evidence
                                                                                                                                                                   
  ------------------ --------- ------------------- ------------------- --------------------- --------------------- ------------- ------------- ------------- ----------
  Gateway (YARP)     Yes       GCP VM vanan-       ASP.NET Core        PG 15 (source of     PG, NATS, Redis,      Yes (80/443   Unknown /     Critical      docker-compose.gateway.yml;
                               gateway (asia-      container           truth) + Keys        Metering, TenantAI,   via nginx)    chưa xác                   Gateway /health
                               southeast1-a)       ghcr.io/vanan-                              Brevo, R2                          minh
                                                   gateway                                                                        
  CoreHub            Yes       In-process với      Class library       PostgreSQL            (qua Gateway)         No            Unknown /     High         3_CoreHub — class library,
                               Gateway             (không container    (Gateway)                                    (in-process)  chưa xác                   không phải container riêng
                                                   riêng)                                                                        minh
  ShopERP            Yes       GCP VM vanan-       ASP.NET Core        SQLite per-tenant     NATS (subscriber),   Yes (80/443)   Unknown /     High         docker-compose.shoperp.yml;
                               shop-a (asia-       container           + PG (Gateway)       Gateway (HTTP)                        chưa xác                   SHOP_INSTANCE_ID
                               southeast1-b)                                                                                     minh
  KhachLink          Yes       GCP VM vanan-       Static WASM qua     (static — PWA)       Gateway (HTTP);      Yes (80/443)   Unknown /     Medium       5_WebApps/KhachLink;
                               khachlink (asia-    nginx                                        Seq chạy trên                        chưa xác                   compose.khachlink.yml
                               southeast1-c)                                                   cùng VPS                             minh
  Directory          Yes       Deploy target qua   Blazor SSR          PG (Gateway)         Gateway              Yes            Unknown /     Low          5_WebApps/Directory; ghcr
                               CD (host chưa       container                                                                     chưa xác                   vanan-directory
                               verify)                                                                                           minh
  Crawler            Yes       Gateway VPS (cùng   Worker container    (stateless)          doanhnghiep.vn,      No             Unknown /     Low          7_Tooling/VanAn.Crawler;
                               stack gateway)                                                  GDT, Gateway auth                    chưa xác                   compose.gateway.yml
                                                                                                                                  minh
  Metering           Yes       GCP VM vanan-       ASP.NET Core        SQLite (append-       Callers (gateway,    No (internal)  Unknown /     High         8_Metering;
                               metering (asia-     container           only, wallet)         ai, voice...)                        chưa xác                   compose.metering.yml
                               southeast1-b)                                                                                     minh
  TenantAI           Yes       Deploy target qua   ASP.NET Core        PostgreSQL 16         Gateway (HMAC ctx    Yes (via       Unknown /     Medium       9_TenantAI/vanan-ai; :5060;
                               CD (host chưa       container           pgvector              signing), Metering   nginx)         chưa xác                   compose.tenantai.yml
                               verify)                                                                                           minh
  Voice              Yes       Deploy target qua   ASP.NET Core        SQLite                Gateway (JWT chia    Yes (TURN      Unknown /     Medium       10_Voice/vanan-voice; :5070;
                               CD (host chưa       container           voice_calls           sẻ), Metering,      UDP)           chưa xác                   VoiceHub + coturn
                               verify)                                                   coturn TURN                            minh
  ATRS               Yes       Deploy target qua   ASP.NET Core        SQLite / PG           Gateway (internal    No             Unknown /     Medium       11_Atrs/vanan-atrs; :5080;
                               CD (host chưa       container           (cần verify)          key), Metering                       chưa xác                   ATRS_REMOTE_HOST
                               verify)                                                                                           minh
  PostgreSQL         Yes       GCP VM vanan-       Container           Volume                Gateway, ShopERP     Internal       UNKNOWN —     Critical      compose.gateway.yml :10;
                               gateway (asia-      postgres:15-alpine  postgres_data         (5432 nội bộ)        (5432)         CẦN XÁC       (source of    chưa có bằng chứng
                               southeast1-a)                                                                      MINH           truth)        backup/PITR trong repo → §3.2.1
  NATS               Yes       GCP VM vanan-       Container           JetStream volume      Gateway, ShopERP,    No (internal)  Unknown /     High         compose.gateway.yml :39;
                               gateway             nats:2.10           nats_data             Voice?                               chưa xác                   nats_data (queue state cần
                                                                                                                                  minh                       backup)
  Redis              Yes       GCP VM vanan-       Container           cache (volatile)      Gateway (SignalR    No             Unknown /     Low          compose.gateway.yml :65
                               gateway             redis:7                                    backplane, cache)                  chưa xác                   (P2 scaling note)
                                                                                                                                  minh
  Seq                Yes       KhachLink VPS       Container           logs                  Gateway (HTTP sink  No (internal   Unknown /     Low          Serilog serverUrl =
                               (deploy qua CD)                                             :5341)               :5341)          chưa xác                   KHACHLINK_REMOTE_HOST:5341
                                                                                                                                  minh
  R2 (Cloudflare)    Yes       Cloudflare          external            photos                Gateway (Guard)      No (signed)    Unknown /     Medium       R2__* env; bucket
                                                                                                                                    chưa xác                   vanan-guard-photos
                                                                                                                                  minh
  --------------------------------------------------------------------------------------------------------------------

Mỗi giá trị chưa xác minh phải ghi `Unknown / Not verified`, không tự
điền giả định.

------------------------------------------------------------------------

## 4. Kiến trúc cần đánh giá

## 4.1 Phương án A -- Private Cloud tự quản lý

Mô hình tham chiếu:

``` text
Physical Server
└── Proxmox VE
    ├── VM: Management / Monitoring
    ├── VM: Ubuntu – Core Services
    │   └── Docker Engine / Docker Compose
    │       ├── Gateway
    │       ├── CoreHub (nếu phù hợp)
    │       ├── Metering
    │       └── Other stateless services
    ├── VM: Ubuntu – Data
    │   └── PostgreSQL (native hoặc container sau khi đánh giá)
    ├── VM: Ubuntu – Edge
    │   └── KhachLink (static WASM + nginx) / Seq / related stateless services
    ├── VM: Ubuntu – AI / Realtime (nếu phù hợp)
    └── Backup target / Proxmox Backup Server (tách biệt khi khả thi)
```

Đây là sơ đồ khái niệm, không phải quyết định vị trí service. Devin phải
đề xuất lại dựa trên dependency map, tải, yêu cầu dữ liệu và khả năng
vận hành.

Ghi chú v1.1 (từ codebase): Production hiện tại ĐÃ chạy mô hình multi-VPS
(Option C, đã duyệt 2026-07-18): gateway stack (PG + NATS + Redis +
Gateway + nginx + Crawler) trên 1 VPS, ShopERP trên VPS riêng, KhachLink +
Seq trên VPS riêng, Metering / TenantAI / Voice / ATRS mỗi service 1 VPS
riêng (deploy qua `cd-multivps.yml` → GHCR → SSH). Option A trên Proxmox
nên TÁI TẠO cấu trúc compose-per-VPS này (1 stack/VPS, giới hạn blast
radius) thay vì gộp Gateway + CoreHub + Metering vào một VM.

### Yêu cầu đánh giá

1.  Xác định số VM hợp lý và lý do phân tách.
2.  Đề xuất CPU, RAM, storage, IOPS và network cho từng VM.
3.  Phân biệt workload stateful và stateless.
4.  Database lifecycle phải độc lập với lifecycle của application
    containers.
5.  Persistent volumes phải được thiết kế, quản lý và backup rõ ràng.
6.  Không dùng một Docker Compose project duy nhất cho tất cả workload
    nếu điều đó tạo blast radius lớn.
7.  Đánh giá Docker Engine + Compose so với Kubernetes/K3s; không mặc
    định cần Kubernetes.
8.  Đánh giá Proxmox VE, Proxmox Backup Server và phương án backup thay
    thế.
9.  Xác định điểm lỗi đơn (SPOF): máy chủ, PSU, disk, controller, NIC,
    router, switch, ISP, điện, cooling.
10. Xác định giới hạn mở rộng theo chiều dọc và chiều ngang.
11. Xác định cách thay thế phần cứng, khôi phục VM và khôi phục dịch vụ
    khi host hỏng.
12. Đánh giá khả năng vận hành từ xa, cập nhật bảo mật và xử lý sự cố.

### Rủi ro cần nêu rõ

-   Một máy chủ vật lý duy nhất là SPOF.
-   RAID không thay thế backup.
-   UPS không thay thế nguồn điện/đường truyền dự phòng.
-   Snapshot trên cùng storage không bảo vệ trước mất máy hoặc
    ransomware.
-   Private Cloud cần người chịu trách nhiệm patching, monitoring,
    incident response và restore test.
-   Internet tại địa điểm đặt máy chủ có thể không có SLA như cloud
    provider.
-   Mất điện, hỏng ổ cứng, lỗi firmware hoặc truy cập vật lý trái phép
    có thể ảnh hưởng toàn hệ thống.

## 4.2 Phương án B -- Hybrid Cloud: GCP + Private Cloud

Định hướng ban đầu để Devin đánh giá:

-   Google Cloud tiếp tục là môi trường production hiện tại.
-   Các workload Gateway, KhachLink, Metering và Vạn An AI hiện được nêu
    là nhóm dự kiến tiếp tục chạy trên GCP; phải kiểm tra lại sự phù hợp
    thay vì coi đây là quyết định cố định.
-   Private Cloud có thể phục vụ development, staging, integration
    testing, build runner, monitoring phụ trợ, backup repository hoặc DR
    standby.
-   CoreHub, PostgreSQL, Voice/STUN/TURN và các workload khác phải được
    phân tích riêng theo data gravity, latency, statefulness, network
    dependency và chi phí. Lưu ý v1.1: "Sync Worker" KHÔNG phải service
    độc lập — sync là `NatsSyncWorker` + `DataSyncSubscriber` (Gateway)
    và `OrderSyncSubscriber` / `TenantSyncSubscriber` (ShopERP) chạy
    in-process qua NATS (xem §2.1).

### Yêu cầu đánh giá

1.  Xác định workload nào nên ở GCP và vì sao.
2.  Xác định workload nào có thể đặt ở Private Cloud mà không làm tăng
    rủi ro vận hành.
3.  Xác định workload nào cần active-active, active-passive hoặc cold
    standby.
4.  Phân tích network connectivity giữa GCP và Private Cloud:
    -   Site-to-site VPN hoặc WireGuard.
    -   IP addressing và subnet overlap.
    -   DNS nội bộ và service discovery.
    -   TLS/mTLS và certificate lifecycle.
    -   Firewall/allowlist.
    -   Latency, bandwidth, packet loss và ISP failure.
5.  Định nghĩa rõ traffic nào đi qua Internet, VPN hoặc private network.
6.  Không expose PostgreSQL hoặc management interfaces trực tiếp ra
    Internet.
7.  Xác định cách đồng bộ dữ liệu, replication và tránh split-brain.
8.  Đánh giá cloud egress, inter-region traffic, storage, logging và
    backup costs.
9.  Đánh giá mức độ lock-in vào GCP APIs/services và phương án
    portability.
10. Thiết kế cách chuyển dịch vụ về GCP hoặc Private Cloud khi một bên
    gặp sự cố.

### Nguyên tắc

Hybrid Cloud không đồng nghĩa tự động có HA. Nếu Private Cloud chỉ là
một máy chủ đơn lẻ, nó có thể là môi trường DR hữu ích nhưng không phải
một cloud region tương đương GCP. Mọi vai trò của Private Cloud phải
được mô tả chính xác: dev, staging, backup, warm standby, cold standby
hay production.

------------------------------------------------------------------------

## 5. Phân tích vị trí triển khai từng service

Devin phải hoàn thành ma trận sau, có thể bổ sung hàng:

  ------------------------------------------------------------------------------------------------------------------
  Service      Stateful?   Latency       Availability    GCP              Private Cloud    Reason / Constraints
                           Sensitivity   Criticality     Recommendation   Recommendation   
  ------------ ----------- ------------- --------------- ---------------- ---------------- -------------------------
  CoreHub      Verify      Verify        High (validate) Evaluate         Evaluate         Source of truth /
                                                                                           dependencies

  Gateway      Verify      High          High (validate) Evaluate         Evaluate         Public ingress and API
                           (validate)                                                      

  KhachLink    Verify      Verify        High (validate) Evaluate         Evaluate         Sync and user traffic

  Metering     Verify      Verify        Medium/High     Evaluate         Evaluate         Usage event durability

  Vạn An AI    Verify      Verify        Medium/High     Evaluate         Evaluate         Compute/API dependencies

  ShopERP      Verify      Verify        High for        Evaluate         Evaluate         Edge/client deployment
                                         tenants                                           model

  Data Sync    Yes (NATS   Low (poll     High for data   Evaluate         Evaluate         In-process (NatsSyncWorker +
  (NATS)       JetStream)  10s)          sync                                             DataSyncSubscriber) — không
                                                                                          phải service deploy riêng                                              

  Voice        Verify      Very High     Medium/High     Evaluate         Evaluate         WebRTC signaling/TURN

  PostgreSQL   Yes         High          Critical        Evaluate         Evaluate         Source of truth and
                                                                                           recovery

  Monitoring   Verify      Medium        High            Evaluate         Evaluate         Independent alert path
                                         operationally                                     
  ------------------------------------------------------------------------------------------------------------------

Không được đánh dấu một service "stateless" nếu chưa kiểm tra local
files, local cache, session state, queue state hoặc persistent volume.

------------------------------------------------------------------------

## 6. Bảo mật và phân vùng hệ thống

### 6.1 Proxmox / Host

-   Proxmox management interface không được public Internet.
-   Quản trị qua VPN hoặc mạng quản trị riêng.
-   Bật MFA nếu khả dụng và áp dụng tài khoản cá nhân, least privilege.
-   Tách management network, server network, storage/backup network và
    guest/tenant network khi cần.
-   Không dùng chung tài khoản admin cho mọi người.
-   Có quy trình cập nhật Proxmox và kiểm tra tương thích trước khi nâng
    cấp.
-   Ghi log thao tác quản trị và có cảnh báo đăng nhập bất thường.
-   Mã hóa storage/backup khi phù hợp với khả năng phục hồi khóa.

### 6.2 Ubuntu VM

-   Chỉ mở các port cần thiết.
-   SSH key-based; hạn chế password login và root login.
-   Firewall ở cả lớp host và VM.
-   Tự động cập nhật security patch có kiểm soát.
-   Phân tách service account và quyền filesystem.
-   Theo dõi dung lượng disk, inode, RAM, CPU và filesystem health.

### 6.3 Docker

-   Không chạy container privileged nếu không có lý do được phê duyệt.
-   Tránh chạy root; dùng non-root user khi ứng dụng hỗ trợ.
-   Không mount Docker socket vào ứng dụng.
-   Pin image theo version/digest; không dùng tag `latest` cho
    production.
-   Scan image và dependency.
-   Đặt CPU/memory limits và restart policy phù hợp.
-   Không đưa secrets vào image hoặc commit vào repository.
-   Tách network theo trust boundary.
-   Xác định rõ volume persistence và backup/restore.
-   Có quy trình cập nhật image, rollback và kiểm tra health.

### 6.4 PostgreSQL và dữ liệu

-   Không public database port.
-   Tài khoản riêng cho từng service, quyền tối thiểu.
-   Không dùng superuser trong ứng dụng.
-   TLS cho kết nối qua network không tin cậy.
-   Mã hóa backup và giới hạn quyền đọc/xóa.
-   Theo dõi connection count, slow queries, disk, WAL growth,
    replication lag.
-   Kiểm tra migration và compatibility trước khi triển khai.
-   Không cho phép hai môi trường production cùng ghi vào một database
    nếu chưa có cơ chế nhất quán được thiết kế rõ ràng.

### 6.5 Secrets và IAM

-   Lập danh mục secrets theo service và môi trường.
-   Tách dev/staging/production credentials.
-   Có quy trình rotation và revoke.
-   Không lưu secrets trong tài liệu audit.
-   Đánh giá GCP Secret Manager và phương án quản lý secrets cho Private
    Cloud.
-   Backup secrets/configuration cần thiết để DR nhưng phải mã hóa và
    kiểm soát truy cập.

------------------------------------------------------------------------

## 7. Backup, Restore và Disaster Recovery

### 7.1 Mục tiêu

Thiết kế backup/DR theo nguyên tắc **3-2-1-1-0**:

-   3 bản sao dữ liệu.
-   2 loại phương tiện lưu trữ khác nhau.
-   1 bản sao offsite.
-   1 bản sao offline hoặc immutable/không thể sửa xóa trong thời gian
    retention.
-   0 lỗi được xác nhận thông qua kiểm tra backup và restore định kỳ.

### 7.2 PostgreSQL

Devin phải đề xuất chiến lược phù hợp với phiên bản và quy mô database
thực tế, tối thiểu đánh giá:

-   Base backup định kỳ.
-   WAL archiving liên tục.
-   Point-in-Time Recovery (PITR).
-   Retention theo RPO/RTO và chi phí.
-   Mã hóa khi truyền và khi lưu.
-   Bản sao lưu độc lập với máy chủ/VM production.
-   Quyền xóa backup tách biệt với quyền vận hành production.
-   Giám sát backup age, WAL archive lag, backup failures và dung lượng.
-   Restore test vào môi trường cô lập.
-   Kiểm tra tính toàn vẹn và khả năng ứng dụng kết nối sau restore.

Không coi VM snapshot đơn lẻ là phương án backup PostgreSQL đủ tin cậy.
Snapshot có thể là lớp bổ sung nếu được thiết kế nhất quán, nhưng phải
có backup database-aware và restore test.

### 7.3 Backup các thành phần khác

Đánh giá backup cho:

-   VM configuration và VM disks.
-   Docker Compose, deployment manifests và configuration.
-   Persistent volumes.
-   Object storage và file uploads.
-   Secrets/keys cần thiết cho phục hồi.
-   DNS, certificates và network configuration.
-   Monitoring/alert rules.
-   Source code và CI/CD configuration.

Source code trong Git không thay thế backup cho dữ liệu vận hành.

### 7.4 RPO/RTO mục tiêu ban đầu

Các giá trị dưới đây là **mục tiêu để đánh giá tính khả thi**, không
phải cam kết đã đạt:

  -----------------------------------------------------------------------------------
  Workload                  Target RPO           Target RTO Validation Required
  --------------- -------------------- -------------------- -------------------------
  CoreHub /                   ≤ 5 phút              ≤ 1 giờ WAL/PITR/replication and
  PostgreSQL                                                restore test

  Gateway                    ≤ 15 phút              ≤ 1 giờ Redeploy, config and
                                                            dependency recovery

  KhachLink                  ≤ 15 phút              ≤ 2 giờ Data sync and replay
                                                            consistency

  Metering                   ≤ 15 phút              ≤ 2 giờ Event durability and
                                                            reconciliation

  Vạn An AI                    ≤ 1 giờ              ≤ 4 giờ Configuration/model/API
                                                            dependency recovery

  Voice                        ≤ 1 giờ              ≤ 2 giờ Signaling/TURN
                                                            redeployment and DNS

  Development                 ≤ 24 giờ             ≤ 24 giờ Repository and
                                                            environment rebuild
  -----------------------------------------------------------------------------------

Devin phải điều chỉnh mục tiêu nếu chi phí hoặc năng lực kỹ thuật không
phù hợp, nhưng phải nêu rõ trade-off và xin quyết định của chủ dự án.

### 7.5 DR scenarios bắt buộc

Phân tích ít nhất:

1.  Hỏng một ổ đĩa.
2.  Hỏng toàn bộ máy chủ Proxmox.
3.  Mất điện kéo dài.
4.  Mất Internet tại Private Cloud.
5.  GCP region/zone/service outage.
6.  PostgreSQL corruption hoặc accidental deletion.
7.  Ransomware hoặc tài khoản quản trị bị chiếm.
8.  Lộ credentials hoặc xóa nhầm backup.
9.  Lỗi deploy làm production không hoạt động.
10. DNS/certificate/network failure.

Với mỗi kịch bản, mô tả detection, decision owner, recovery steps, data
loss window, expected downtime, dependencies và rollback.

### 7.6 Restore drills

Đề xuất lịch kiểm tra:

-   Backup job: giám sát tự động mỗi lần chạy.
-   Restore sample: tối thiểu hàng tháng trong giai đoạn đầu.
-   Full DR exercise: tối thiểu mỗi quý hoặc theo mức độ thay đổi hệ
    thống.
-   Ghi lại thời gian thực tế, lỗi, RPO/RTO đo được và action items.

Không được báo cáo đạt RPO/RTO chỉ dựa trên thiết kế lý thuyết.

------------------------------------------------------------------------

## 8. Network, DNS và kết nối

Devin phải tạo sơ đồ network cho cả hai phương án, thể hiện:

-   Public ingress.
-   Private subnets.
-   Management network.
-   Database network.
-   Backup network.
-   VPN tunnel.
-   DNS resolution.
-   TLS termination.
-   Firewall boundaries.
-   Outbound Internet dependencies.

Đánh giá:

-   Static IP có thực sự cần không?
-   Có thể dùng dynamic DNS hoặc tunnel an toàn không?
-   VPN site-to-site hay WireGuard phù hợp hơn?
-   Có subnet overlap giữa GCP và Private Cloud không?
-   ISP có public IP, CGNAT hay giới hạn inbound không?
-   Có cần đường truyền Internet thứ hai không?
-   Cách hoạt động khi VPN down.
-   Có cơ chế tránh lộ dịch vụ nội bộ qua reverse proxy hoặc cấu hình
    sai firewall không?

Không giả định Private Cloud có thể nhận inbound traffic ổn định trước
khi xác minh ISP, IP và điều kiện mạng thực tế.

------------------------------------------------------------------------

## 9. Capacity, hiệu năng và khả năng mở rộng

Đánh giá theo các mốc phát triển của Vạn An:

-   100 cửa hàng.
-   1.000 cửa hàng.
-   10.000 cửa hàng.

Nếu codebase hoặc workload hiện tại có benchmark đáng tin cậy, dùng dữ
liệu đó. Nếu chưa có, lập kế hoạch load test và ghi rõ đây là estimate,
không phải measured capacity.

### 9.1 Các chỉ số cần xem xét

-   Concurrent users / concurrent requests.
-   Request rate và peak throughput.
-   Transactions per second.
-   Database connections, query latency, write amplification.
-   Queue depth, processing lag và retry rate.
-   CPU, RAM, disk IOPS, storage growth.
-   Network throughput, latency, packet loss.
-   AI requests, token/compute usage và external API limits.
-   WebRTC concurrent sessions, TURN bandwidth và relay ratio.
-   Backup window và restore duration.

### 9.2 Yêu cầu đầu ra

-   Capacity model theo từng service.
-   Bottleneck hiện tại và bottleneck dự kiến.
-   Scale-up/scale-out thresholds.
-   Cảnh báo khi vượt ngưỡng.
-   Dự báo storage 12/36/60 tháng.
-   Kịch bản tăng trưởng nhanh hơn dự kiến.
-   Phân biệt capacity của application, database, network và đội ngũ vận
    hành.

Không lấy số lượng tenant làm chỉ số tải duy nhất; phải xác định
workload profile và hành vi sử dụng.

------------------------------------------------------------------------

## 10. CI/CD, deployment và portability

Đánh giá quy trình triển khai hiện tại và đề xuất chuẩn chung:

-   Build artifact/image một lần, deploy theo môi trường.
-   Configuration tách khỏi image.
-   Secrets không nằm trong repository.
-   Image version/digest được pin.
-   Health check trước/sau deployment.
-   Rollback rõ ràng.
-   Database migration có kiểm soát và backup trước thay đổi rủi ro.
-   Deployment không gây downtime nếu kiến trúc và ngân sách cho phép.
-   Staging phản ánh các dependency production quan trọng.
-   Có thể tái triển khai service trên GCP hoặc Private Cloud từ source
    và configuration đã quản lý.

Lập bảng đánh giá lock-in:

  -------------------------------------------------------------------------------------
  Dependency   Current               Portability   Migration   Risk        Mitigation
               Provider/Technology                 Effort                  
  ------------ --------------------- ------------- ----------- ----------- ------------

  -------------------------------------------------------------------------------------

Không đề xuất thay thế một dịch vụ managed chỉ vì muốn "cloud-neutral"
nếu việc tự vận hành làm tăng rủi ro hoặc tổng chi phí.

------------------------------------------------------------------------

## 11. Monitoring, logging và vận hành

Thiết kế quan sát hệ thống ở cả hai môi trường:

-   Centralized logs hoặc phương án tập trung log có kiểm soát.
-   Metrics cho host, VM, container, application, database và network.
-   Uptime/health checks từ bên ngoài hạ tầng chính.
-   Alert routing tới người phụ trách.
-   Disk capacity, backup age, replication lag và certificate expiry
    alerts.
-   Audit logs cho hoạt động quản trị.
-   Dashboard cho service health và infrastructure health.
-   Runbook cho các lỗi thường gặp.
-   Quy trình incident severity, escalation và postmortem.

Monitoring không nên phụ thuộc hoàn toàn vào chính máy chủ đang được
giám sát. Cần ít nhất một cơ chế cảnh báo ngoài hệ thống chính.

------------------------------------------------------------------------

## 12. Phân tích chi phí (TCO)

Không chỉ so sánh giá VM GCP với giá mua máy chủ. Phải tính tổng chi phí
sở hữu (TCO) trong 3 năm và 5 năm.

### 12.1 Private Cloud -- CAPEX/OPEX

**CAPEX:** - Server, CPU, RAM. - Enterprise SSD/NVMe và phương án disk
redundancy. - HBA/RAID controller nếu cần. - NIC, switch,
router/firewall. - UPS và thiết bị điện. - Thiết bị backup/NAS hoặc
server backup riêng. - Thiết bị dự phòng và chi phí thay thế.

**OPEX:** - Điện năng và cooling. - Internet và đường truyền dự phòng. -
Colocation hoặc chi phí mặt bằng nếu có. - Bảo trì, thay disk/PSU/fan. -
Backup offsite/immutable storage. - Monitoring và security tooling. -
Nhân lực vận hành, trực sự cố và thời gian bảo trì. - Chi phí
downtime/rủi ro gián đoạn.

### 12.2 Google Cloud

Tách chi phí:

-   Compute.
-   Persistent disks.
-   Database (nếu managed).
-   Storage/backup.
-   Network egress.
-   Load balancer, IP, DNS và certificates nếu tính phí.
-   Logging/monitoring.
-   VPN/network services.
-   Snapshots và retention.
-   Support plan hoặc dịch vụ liên quan.

### 12.3 Hybrid Cloud

Tính cả hai phía và chi phí kết nối:

-   GCP production.
-   Private Cloud hardware/operations.
-   VPN/network.
-   Replication và backup transfer.
-   Egress.
-   Offsite immutable backup.
-   DR testing.
-   Nhân lực vận hành hai môi trường.

### 12.4 Bảng TCO bắt buộc

Lập bảng theo kịch bản:

  -------------------------------------------------------------------------
  Cost Category    Option A -- Private   Option B -- Hybrid Notes /
                                 Cloud                Cloud Assumptions
  --------------- -------------------- -------------------- ---------------
  Initial CAPEX                                             

  Monthly OPEX                                              

  GCP monthly                                               
  cost                                                      

  Backup /                                                  
  offsite                                                   

  Network /                                                 
  egress                                                    

  Maintenance                                               

  Operations                                                
  labor                                                     

  3-year TCO                                                

  5-year TCO                                                
  -------------------------------------------------------------------------

Cung cấp ít nhất ba kịch bản: Conservative, Expected, Growth. Ghi rõ giả
định, đơn vị tiền tệ, thuế (nếu tính), tỷ giá (nếu dùng) và độ tin cậy
của số liệu.

------------------------------------------------------------------------

## 13. Risk Register

Tạo risk register có tối thiểu các cột:

  --------------------------------------------------------------------------------------------
  ID      Risk    Likelihood   Impact   Severity   Detection   Mitigation   Owner   Residual
                                                                                    Risk
  ------- ------- ------------ -------- ---------- ----------- ------------ ------- ----------

  --------------------------------------------------------------------------------------------

Phải có tối thiểu các rủi ro:

-   Single physical host failure.
-   Backup failure hoặc backup không restore được.
-   Ransomware/credential compromise.
-   ISP/power outage.
-   Database corruption.
-   Split-brain hoặc replication inconsistency.
-   Misconfigured firewall/public exposure.
-   Thiếu năng lực vận hành 24/7.
-   Hardware lifecycle/parts availability.
-   GCP cost growth/egress.
-   Vendor lock-in.
-   Deployment/configuration drift giữa hai môi trường.
-   DR environment không được kiểm thử.
-   Private Cloud trở thành production dependency nhưng không có
    redundancy.

------------------------------------------------------------------------

## 14. Lộ trình triển khai đề xuất

Đây là lộ trình để Devin đề xuất và điều chỉnh; chưa phải phê duyệt
triển khai.

### Phase 0 -- Audit và Architecture Decision

-   Hoàn tất codebase audit.
-   Hoàn tất GCP inventory.
-   Xác minh PostgreSQL và backup hiện tại.
-   Lập dependency map và current architecture.
-   Đánh giá hai phương án.
-   Hoàn thành TCO, risk register và ADR.
-   Trình chủ dự án phê duyệt.

**Gate:** Không chuyển phase nếu thiếu evidence về dữ liệu, backup và
dependency production.

### Phase 1 -- Private Cloud Lab

Sau khi được phê duyệt:

-   Dựng Proxmox lab không chứa production data nhạy cảm.
-   Tạo Ubuntu VM và Docker environment.
-   Kiểm thử network segmentation, secrets và monitoring.
-   Thử deployment/rollback từ source.
-   Đo hiệu năng và chi phí vận hành.

### Phase 2 -- Staging / Integration

-   Chuyển hoặc dựng staging theo kiến trúc đã duyệt.
-   Kiểm tra CI/CD và configuration parity.
-   Thử kết nối GCP--Private Cloud.
-   Thực hiện restore test và DR simulation.
-   Không để staging ghi vào production database.

### Phase 3 -- Backup / DR

-   Triển khai backup offsite/immutable theo phê duyệt.
-   Thiết lập PostgreSQL WAL/PITR nếu phù hợp.
-   Tách quyền backup khỏi quyền production.
-   Thực hiện restore drill.
-   Đo RPO/RTO thực tế và cập nhật runbook.

### Phase 4 -- Workload Optimization

Chỉ xem xét chuyển workload production khi: - Có business case và TCO rõ
ràng. - Có benchmark và capacity evidence. - Security review đạt yêu
cầu. - Backup/restore đã kiểm chứng. - Có cutover/rollback plan. - Có
người chịu trách nhiệm vận hành. - Chủ dự án phê duyệt bằng văn bản.

------------------------------------------------------------------------

## 15. Deliverables bắt buộc

Devin phải tạo các tài liệu sau trong thư mục tài liệu hạ tầng phù hợp
của repository:

1.  `CODEBASE_INFRASTRUCTURE_AUDIT.md` -- Kiểm kê codebase và runtime.
2.  `CURRENT_ARCHITECTURE.md` -- Sơ đồ và mô tả kiến trúc hiện tại.
3.  `DEPENDENCY_MAP.md` -- Dependency map giữa service, database và
    external systems.
4.  `GCP_INFRASTRUCTURE_AUDIT.md` -- Kiểm kê GCP và cấu hình liên quan.
5.  `PRIVATE_CLOUD_ARCHITECTURE.md` -- Thiết kế Phương án A.
6.  `HYBRID_CLOUD_ARCHITECTURE.md` -- Thiết kế Phương án B.
7.  `SECURITY_ASSESSMENT.md` -- Threat surface và security controls.
8.  `BACKUP_DR_STRATEGY.md` -- Backup, restore, DR, RPO/RTO.
9.  `PERFORMANCE_CAPACITY_PLAN.md` -- Capacity model và test plan.
10. `INFRASTRUCTURE_COST_MODEL.md` -- TCO 3 năm/5 năm.
11. `MIGRATION_ROADMAP.md` -- Roadmap, gates, rollback.
12. `ARCHITECTURE_DECISION_RECORD.md` -- Khuyến nghị cuối cùng và lý do.
13. `RISK_REGISTER.md` -- Risk register.

Mỗi tài liệu phải có ngày đánh giá, phạm vi, nguồn bằng chứng, giả định,
phần chưa xác minh và người/nhóm chịu trách nhiệm nếu biết.

------------------------------------------------------------------------

## 16. Tiêu chí nghiệm thu

Đợt đánh giá chỉ được xem là hoàn thành khi:

-   [ ] Đã kiểm kê các service thực sự có trong codebase.
-   [ ] Đã lập dependency map có thể truy vết.
-   [ ] Đã xác minh hạ tầng GCP đang chạy.
-   [ ] Đã xác định chính xác PostgreSQL đang ở đâu và cách backup hiện
    tại.
-   [ ] Đã phân biệt rõ backup, snapshot, replication và DR.
-   [ ] Đã đánh giá đầy đủ Option A và Option B.
-   [ ] Đã có đề xuất placement cho từng service kèm lý do.
-   [ ] Đã có network diagram và security boundary.
-   [ ] Đã có backup/restore/DR design và RPO/RTO khả thi.
-   [ ] Đã có capacity model theo 100/1.000/10.000 cửa hàng hoặc mốc phù
    hợp hơn có giải thích.
-   [ ] Đã có TCO 3 năm và 5 năm với giả định minh bạch.
-   [ ] Đã có risk register và migration roadmap.
-   [ ] Đã có ADR nêu rõ khuyến nghị và các phương án bị loại.
-   [ ] Không có thay đổi production ngoài phạm vi được phép.

------------------------------------------------------------------------

## 17. Các câu hỏi Devin phải trả lời

1.  Với codebase và workload hiện tại, Vạn An có thực sự cần Proxmox
    Private Cloud ngay không?
2.  Phương án A có điểm lỗi đơn nào không thể chấp nhận? Cần tối thiểu
    bao nhiêu host để đạt mức sẵn sàng mong muốn?
3.  Trong Hybrid Cloud, service nào nên tiếp tục ở GCP, service nào có
    thể chạy Private Cloud?
4.  PostgreSQL nên tiếp tục ở GCP, chuyển sang Private Cloud hay dùng mô
    hình primary/standby? Vì sao?
5.  Backup PostgreSQL hiện tại có thực sự đáp ứng khả năng PITR và
    restore không?
6.  Nếu GCP hoặc Private Cloud gặp sự cố, hệ thống phục hồi như thế nào?
    RPO/RTO đo được là bao nhiêu?
7.  Nếu Private Cloud mất điện hoặc Internet, production trên GCP có bị
    ảnh hưởng không?
8.  Làm sao chống ransomware và việc attacker xóa cả production lẫn
    backup?
9.  TCO 3 năm và 5 năm của hai phương án là bao nhiêu ở quy mô 100,
    1.000 và 10.000 cửa hàng?
10. Đội ngũ hiện tại có đủ năng lực vận hành Private Cloud an toàn
    không? Những trách nhiệm vận hành nào còn thiếu?
11. Mức độ phụ thuộc GCP hiện tại là gì? Có thể chuyển workload sang nền
    tảng khác với effort nào?
12. Khuyến nghị cuối cùng là gì, và điều kiện nào khiến Devin thay đổi
    khuyến nghị đó?

------------------------------------------------------------------------

## 18. Quy tắc làm việc và giới hạn quyền hạn

### 18.1 Được phép trong giai đoạn này

-   Đọc codebase và cấu hình repository.
-   Thực hiện lệnh kiểm tra read-only.
-   Kiểm tra trạng thái resource GCP bằng quyền read-only được cấp.
-   Đọc log/metrics đã được cho phép.
-   Tạo tài liệu audit, sơ đồ, bảng so sánh và đề xuất.
-   Chạy phân tích offline hoặc benchmark trong môi trường không
    production nếu không gây ảnh hưởng.

### 18.2 Không được phép

-   Không sửa source code hoặc cấu hình triển khai hiện hành.
-   Không chạy deploy, migration, restart hoặc upgrade production.
-   Không thay đổi firewall, IAM, DNS, routing, VPN hoặc public
    exposure.
-   Không tạo/xóa VM, disk, bucket, database hoặc cloud resource.
-   Không xóa, ghi đè hoặc di chuyển backup.
-   Không sao chép production data ra máy cá nhân hoặc môi trường không
    được phê duyệt.
-   Không đưa secrets vào báo cáo.
-   Không tự ý mua phần cứng, đăng ký dịch vụ hoặc phát sinh chi phí.

Nếu cần một thao tác có thể ảnh hưởng hệ thống hoặc phát sinh chi phí,
phải dừng và xin phê duyệt trước.

------------------------------------------------------------------------

## 19. Yêu cầu về chất lượng phân tích

-   Phải đọc codebase thực tế, không chỉ dựa vào tên service trong tài
    liệu.
-   Mọi kết luận quan trọng phải dẫn chứng file, cấu hình, resource ID
    đã được làm mờ thông tin nhạy cảm, log hoặc phép đo.
-   Không được tuyên bố đã kiểm tra GCP nếu không có quyền truy cập.
-   Không được bịa cấu hình, benchmark, chi phí hoặc kết quả restore.
-   Mọi estimate phải có công thức và giả định.
-   Nêu rõ điều gì đã xác minh, điều gì chưa thể xác minh và cần ai cung
    cấp.
-   Nếu thiếu quyền truy cập, lập danh sách chính xác quyền/dữ liệu còn
    thiếu; không tự suy diễn.
-   Nếu đề xuất khác với định hướng ban đầu, phải giải thích bằng bằng
    chứng và trade-off.
-   Ưu tiên kiến trúc đơn giản, có thể vận hành, có đường nâng cấp;
    không over-engineer.
-   Không tối ưu chi phí bằng cách hy sinh tính toàn vẹn dữ liệu hoặc
    khả năng phục hồi.

------------------------------------------------------------------------

## 20. Kết quả cuối cùng cần trình bày

Trong `ARCHITECTURE_DECISION_RECORD.md`, Devin phải đưa ra:

1.  **Executive Summary:** Khuyến nghị ngắn gọn.
2.  **Current State:** Hệ thống đang thực sự vận hành như thế nào.
3.  **Option A Assessment:** Điểm mạnh, điểm yếu, rủi ro, chi phí.
4.  **Option B Assessment:** Điểm mạnh, điểm yếu, rủi ro, chi phí.
5.  **Recommended Target Architecture:** Sơ đồ kiến trúc đề xuất.
6.  **Service Placement Matrix:** Vị trí triển khai từng service.
7.  **PostgreSQL Decision:** Vị trí, HA, backup, PITR và DR.
8.  **Security & Operations Model:** Ai vận hành, kiểm soát nào bắt
    buộc.
9.  **TCO:** 3 năm/5 năm và các kịch bản tăng trưởng.
10. **Roadmap:** Các phase, gates, rollback và điều kiện chuyển phase.
11. **Open Decisions:** Những điểm cần chủ dự án quyết định.
12. **Rejected Alternatives:** Các phương án không chọn và lý do.

**Quyết định cuối cùng thuộc về chủ dự án Vạn An.** Devin có trách nhiệm
đưa ra phân tích trung thực, có bằng chứng, phản biện cả giả định ban
đầu và không được tự chuyển từ giai đoạn đánh giá sang triển khai.

------------------------------------------------------------------------

**END OF SRS**
