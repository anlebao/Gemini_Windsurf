#!/bin/bash
# RV P6 P3+P5: seed AddOn (demo data) + sửa tên offering/category có dấu tiếng Việt
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"
TENANT_B="4345dff8-b4b7-411f-b8f1-faf36f242d70"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"
A1="$(python3 -c 'import uuid; print(uuid.uuid4())')"
A2="$(python3 -c 'import uuid; print(uuid.uuid4())')"

docker exec -i vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
-- P5: tên có dấu (UTF-8)
UPDATE "AppointmentOfferings" SET "DisplayName" = 'Massage 60 phút' WHERE "TenantId" = '$TENANT_A' AND "DisplayName" = 'Massage 60 phut';
UPDATE "AppointmentOfferings" SET "DisplayName" = 'Massage 90 phút' WHERE "TenantId" = '$TENANT_A' AND "DisplayName" = 'Massage 90 phut';
UPDATE "AppointmentOfferings" SET "DisplayName" = 'Cắt tóc nam' WHERE "TenantId" = '$TENANT_B' AND "DisplayName" = 'Cat toc nam';
UPDATE "ServiceCategories" SET "Name" = 'Massage' WHERE "TenantId" = '$TENANT_A' AND "Name" = 'Massage';
UPDATE "ServiceCategories" SET "Name" = 'Cắt tóc' WHERE "TenantId" = '$TENANT_B' AND "Name" = 'Cat toc';
-- P3: AddOn demo cho tenant A (non-scheduling §8.3)
INSERT INTO "AddOns" ("Id","Name","Price","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$A1','Nước uống',20000,true,'$TENANT_A','$NOW','$NOW',false),
 ('$A2','Phụ thu phòng VIP',50000,true,'$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
SELECT 'addon seeded' AS status;
SQL
echo "=== verify ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"DisplayName\" FROM \"AppointmentOfferings\"; SELECT \"Name\", \"Price\" FROM \"AddOns\";"' 2>&1 | head -10
echo DONE
