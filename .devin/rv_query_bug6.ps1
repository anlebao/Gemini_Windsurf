$sshKey = "C:\VibeCoding\CD\SSH\vanan.pem"
$host_ = "ubuntu@161.118.212.110"

$sql = @"
SELECT Id||'|'||Status||'|'||COALESCE(CustomerId,'NULL')||'|'||COALESCE(CustomerDeviceId,'NULL')||'|'||COALESCE(CustomerInfo_FullName,'NULL')||'|'||TotalAmount FROM Orders WHERE Id LIKE '019FA22A%';
SELECT '---CUSTOMERS---';
SELECT Id||'|'||FullName||'|'||COALESCE(DeviceId,'NULL')||'|'||LoyaltyPoints FROM Customers;
SELECT '---LOYALTY---';
SELECT Id||'|'||CustomerId||'|'||PointBalance FROM LoyaltyRewards;
"@
$bytes = [System.Text.Encoding]::UTF8.GetBytes($sql)
$b64 = [Convert]::ToBase64String($bytes)

$remoteScript = "rm -f /tmp/rvL.db* && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db /tmp/rvL.db && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-wal /tmp/rvL.db-wal && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-shm /tmp/rvL.db-shm && echo $b64 | base64 -d > /tmp/rvL.sql && sqlite3 /tmp/rvL.db < /tmp/rvL.sql"

Write-Host "=== Bug 6: Verify order 019FA22A has CustomerDeviceId + CustomerInfo ==="
ssh -i $sshKey -o StrictHostKeyChecking=no $host_ $remoteScript
