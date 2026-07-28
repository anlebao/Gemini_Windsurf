$sshKey = "C:\VibeCoding\CD\SSH\vanan.pem"
$host_ = "ubuntu@161.118.212.110"
$q = [char]39

$sql1 = "SELECT Status, count(*) as Cnt FROM Orders GROUP BY Status;"
$sql2 = "SELECT Id||'|'||Status||'|'||COALESCE(CustomerId,'NULL')||'|'||TotalAmount FROM Orders WHERE Status='completed' LIMIT 5;"
$sql3 = "SELECT Id||'|'||CustomerId||'|'||PointBalance FROM LoyaltyRewards;"
$sql4 = "SELECT Id||'|'||FullName||'|'||LoyaltyPoints FROM Customers;"

$sqlAll = "$sql1`n$sql2`n$sql3`n$sql4"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($sqlAll)
$b64 = [Convert]::ToBase64String($bytes)

$remoteScript = "rm -f /tmp/rvL.db* && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db /tmp/rvL.db && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-wal /tmp/rvL.db-wal && docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-shm /tmp/rvL.db-shm && echo $b64 | base64 -d > /tmp/rvL.sql && sqlite3 -header /tmp/rvL.db < /tmp/rvL.sql"

Write-Host "=== Bug 6: Loyalty investigation ==="
ssh -i $sshKey -o StrictHostKeyChecking=no $host_ $remoteScript
