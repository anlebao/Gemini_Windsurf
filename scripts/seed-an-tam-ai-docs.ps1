# seed-an-tam-ai-docs.ps1 — task_ai_p5+/P7 pilot prep: seed knowledge documents
# cho tenant An Tâm (quán chay) vào vanan-ai qua ingest API + approve gate [G14].
#
# Usage:
#   . .\scripts\seed-an-tam-ai-docs.ps1
#   Set-TenantAiEnv -BaseUrl "http://<internal>:5060" -CallerId "shoperp-a" `
#       -CallerKey "…" -Secret "…" -TenantId "e2d91cdf-d3c4-4399-b96e-e772a7a41630"
#   Invoke-AnTamSeed
#
# Docs land as pending_review → script auto-approves (operator-curated content).
# Products KHÔNG seed ở đây — menu thật nhập vào ShopERP rồi SyncNow (PO-5).
# Nguồn tham khảo menu: buddhachay.vn (xem an-tam-menu-reference.json).
# [TODO]-marked facts (địa chỉ/giờ/SĐT) — owner điền thật trước khi publish.

param(
    [switch]$SkipApprove
)

. "$PSScriptRoot\tenant-ai-ops.ps1"

$TENANT = "e2d91cdf-d3c4-4399-b96e-e772a7a41630"  # An Tâm (production)

$docs = @(
    @{
        type  = "story"; title = "Câu chuyện Quán Chay An Tâm"
        content = @"
Quán Chay An Tâm là quán chay gia đình, lấy tên "An Tâm" — mong mỗi bữa ăn đều
đem lại sự an lành cho thân và tâm. Quán phục vụ món chay Việt truyền thống:
cơm phần, phở chay, bún riêu chay, lẩu nấm và các món kho đậm vị quê nhà.

Nguyên tắc của quán: rau củ quả tươi theo ngày, nấm các loại làm nền vị ngọt
tự nhiên, không dùng bột ngọt nhiều, nước dùng ninh từ rau củ chứ không bột sẵn.
Phù hợp khách ăn chay trường, ăn chay kỳ (mùng 1, rằm) và khách muốn bữa nhẹ
thanh mát.

[TODO: owner bổ sung] năm thành lập, câu chuyện riêng, địa chỉ quán.
"@
    },
    @{
        type  = "faq"; title = "FAQ — giờ mở cửa, giao hàng, thanh toán"
        content = @"
Giờ mở cửa: [TODO] (tham khảo: 7h00–21h00 hằng ngày, phục vụ cả ngày lễ chay mùng 1 và rằm).
Đặt bàn: khách có thể đặt bàn trước qua điện thoại hoặc đặt trên app; quán nhận
đặt tiệc chay gia đình, đám giỗ, cúng rằm theo yêu cầu.
Giao hàng: quán có giao hàng qua app và hotline [TODO SĐT]; phí ship theo khu vực.
Thanh toán: tiền mặt, chuyển khoản ngân hàng, quét mã QR VietQR tại quầy.
Đậu xe: có chỗ đậu xe máy [TODO: ô tô?].
Wifi: có wifi miễn phí cho khách [TODO tên wifi nếu cần].
Ăn chay kỳ: vào ngày rằm/mùng 1 quán phục vụ đầy đủ thực đơn, có thể đông —
khách nên đặt bàn trước.
Dị ứng: một số món có đậu phộng/mè/gluten — khách báo nhân viên để được tư vấn
món phù hợp.
"@
    },
    @{
        type  = "menu"; title = "Thực đơn tổng quan"
        content = @"
Thực đơn quán gồm các nhóm: Khai vị (gỏi cuốn chay, chả giò chay, gỏi mít non,
gỏi ngó sen, đậu hũ chiên sả ớt); Cơm (cơm phần chay, cơm chiên thơm, cơm chiên
kiểu Thái, cơm chiên sốt cà ri); Bún/Mì/Hủ tiếu (phở chay, bún riêu chay, bún
Huế chay, mì Quảng chay, hủ tiếu xào, mì xào giòn/mềm, Pad Thái chay, mỳ ý sốt
Buddha, cà ri thập cẩm); Món rau & kho (rau muống xào tỏi, cải thìa xào nấm,
đậu hũ sốt cà chua, đậu hũ kho tiêu, nấm kho tiêu, sườn non chay chua ngọt);
Canh (canh chua chay, canh rong biển, canh củ sen đậu đỏ, canh khổ qua, canh
xà lách xoong); Lẩu cho 2-4 người (lẩu nấm, lẩu Thái chua cay, lẩu chao, lẩu
kim chi, lẩu tiêu xanh, lẩu tiềm thuốc bắc); Tráng miệng & nước uống (chè đậu
xanh, chè khúc bạch, tàu hũ nóng, nước ép rau má, sinh tố).

Giá và món còn phục vụ cập nhật trên app — hỏi mình tên món cụ thể để biết giá
chính xác nhất nhé.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Nước dùng chay cơ bản"
        content = @"
Nước dùng chay là nền của phở, bún, lẩu, canh. Nguyên liệu: củ cải trắng 1 củ,
cà rốt 2 củ, bắp cải 1/4 cây, nấm đông cô khô 8-10 cái, thơm (dứa) 1/4 trái,
gừng, hành tím nướng 3 củ, muối, đường phèn.
Cách làm: nướng sơ hành-gừng cho thơm; nấm đông cô ngâm nở rửa sạch; cho tất cả
vào nồi 3 lít nước, đun sôi rồi hạ lửa liu riu 45-60 phút, vớt bọt cho nước
trong; nêm muối + ít đường phèn — nước dùng ngọt thanh tự nhiên, dùng cho phở
chay, bún, lẩu.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Phở chay"
        content = @"
Nước dùng: nước dùng chay cơ bản + thêm quế-hồi-đinh hương nướng thơm bỏ gói
vải hầm 20 phút (bí quyết mùi phở).
Topping: đậu hủ chiên cắt lát, nấm đông cô thái lát xào sơ với xì dầu, nấm rơm
bổ đôi, giá chần, hành lá-ngò gai, chanh, ớt.
Cách làm: trụng bánh phở vào tô, xếp topping, chan nước dùng đang sôi, rắc
hành ngò; ăn kèm tương đen + tương ớt chay.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Gỏi cuốn chay + sốt tương"
        content = @"
Nhân: đậu hủ chiên thái sợi, nấm đông cô xào chín với xì dầu, bún tươi, rau
sống (xà lách, húng, giá, hẹ).
Cuốn: nhúng bánh tráng mềm, xếp rau → bún → nhân, cuốn chặt.
Sốt tương chấm (quyết định độ ngon): tương hột xay nhuyễn + nước cốt dừa đun
sôi nhỏ lửa, nêm đường + ít muối, thêm đậu phộng rang giã + ớt xay; có thể pha
sốt me cho vị chua nhẹ.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Đậu hũ sốt cà chua"
        content = @"
Nguyên liệu: đậu hủ 2 miếng cắt khối, cà chua chín 3 quả, hành tím, tỏi, ngò
gai, xì dầu, đường, tiêu.
Cách làm: đậu hủ chiên vàng đều các mặt; phi thơm hành tỏi, cho cà chua băm xào
nhừ nêm xì dầu + đường + chút nước thành sốt sánh; cho đậu hủ vào đảo nhẹ 2-3
phút cho thấm, rắc ngò-tiêu. Món này ăn với cơm trắng hoặc làm nhân cơm phần.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Canh chua chay"
        content = @"
Nguyên liệu: cà chua, dứa (thơm), đậu bắp, giá, rau nhút/ngò ôm-ngò gai, củ
hành, me chín (hoặc sấu), ớt, nước mắm chay/muối, đường.
Cách làm: phi củ hành thơm, xào cà chua cho nhừ, đổ nước đun sôi; vắt nước cốt
me (ngâm me nóng) nêm chua trước vị chính; cho dứa-đậu bắp nấu chín; nêm
mặn-ngọt cân bằng, tắt bếp bỏ giá + rau thơm + ớt. Điểm then chốt: nêm chua
bằng me/sấu, không dùng chanh (mất mùi quê).
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Bún riêu chay"
        content = @"
Riêu chay: đậu hủ non/mềm dầm nhuyễn trộn nấm mèo-cà rốt băm nhỏ + chút mắm
chay, hấp/đổ khuôn cắt khối.
Nước dùng: xào cà chua nhừ với dầu điều cho đỏ, đổ nước dùng chay, nêm mắm
chay + đường + chút tôm chay (tuỳ).
Lắp: bún trụng, riêu chay, đậu hủ chiên, nấm, chan nước sôi; ăn kèm rau sống
xé, chanh, ớt, mắm chay pha.
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Lẩu nấm chay"
        content = @"
Nước lẩu: hầm mướp + củ cải + nấm đông cô khô 30-40 phút lấy ngọt; nêm muối,
đường phèn, chút dầu mè.
Nhân lẩu: các loại nấm (kim châm, bào ngư, đùi gà, rơm, mỡ), đậu hủ chiên, đậu
hủ non, bông cải, cải thảo, cải ngọt, rau muống; bún tươi hoặc mì.
Nước chấm: xì dầu + ớt, hoặc chao trắng nghiền + đường + ớt (kiểu chấm chay).
"@
    },
    @{
        type  = "recipe"; title = "Công thức — Chả giò chay"
        content = @"
Nhân: khoai môn-cà rốt sợi nhỏ, miến ngâm cắt ngắn, nấm mèo-nấm hương băm, đậu
hủ nghiền, hành-tiêu-nước mắm chay; trộn đều (có thể thêm chút bột năng cho kết
dính).
Gói: bánh tráng rế hoặc bánh tráng thường nhúng nước ấm pha chút đường, gói
chặt hai đầu.
Chiên: ngập dầu lửa vừa cho vàng giòn đều; chấm nước mắm chay chua ngọt + rau
sống + dưa chua.
"@
    }
)

function Invoke-AnTamSeed {
    $created = @()
    foreach ($d in $docs) {
        Write-Host "→ $($d.type): $($d.title)" -ForegroundColor Cyan
        $res = Send-AiDocument -Type $d.type -Title $d.title -Content $d.content -Classification 'public'
        if ($res -and $res.id) { $created += $res.id; Write-Host "   id=$($res.id) $($res.approvalStatus)" }
        else { Write-Warning "   failed: $($res | ConvertTo-Json -Compress -Depth 4)" }
    }
    if (-not $SkipApprove) {
        foreach ($id in $created) {
            Approve-AiDoc -Id $id | Out-Null
            Write-Host "   approved $id" -ForegroundColor Green
        }
    }
    Write-Host "`nDone — $($created.Count)/$($docs.Count) docs seeded$(if($SkipApprove){' (pending — approve via Get-AiPending)'})"
}

Write-Host "seed-an-tam-ai-docs loaded — Set-TenantAiEnv first (tenant $TENANT), then Invoke-AnTamSeed"
