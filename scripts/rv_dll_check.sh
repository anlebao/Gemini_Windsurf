#!/bin/bash
echo "=== KhachLink DLL check ==="
KL_DLL=$(docker exec vanan-khachlink find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" 2>/dev/null | head -1)
if [ -z "$KL_DLL" ]; then
  KL_DLL=$(docker exec vanan-khachlink find /app -name "VanAn.KhachLink.dll" 2>/dev/null | head -1)
fi
echo "DLL path: $KL_DLL"
if [ -n "$KL_DLL" ]; then
  echo "DLL timestamp:"
  docker exec vanan-khachlink ls -la "$KL_DLL" 2>/dev/null
  echo "DLL size:"
  docker exec vanan-khachlink stat -c %s "$KL_DLL" 2>/dev/null
  echo "Check for Missions page in DLL (string search):"
  docker exec vanan-khachlink sh -c "strings '$KL_DLL' 2>/dev/null | grep -i 'Missions' | head -5" 2>/dev/null || echo "strings not available"
  echo "Check for BirthdayEntry in DLL:"
  docker exec vanan-khachlink sh -c "strings '$KL_DLL' 2>/dev/null | grep -i 'BirthdayEntry\|Ngày sinh\|birthday' | head -5" 2>/dev/null || echo "strings not available"
fi

echo
echo "=== ShopERP DLL check ==="
SE_DLL=$(docker exec vanan-shoperp find /app -name "VanAn.ShopERP.dll" 2>/dev/null | head -1)
echo "DLL path: $SE_DLL"
if [ -n "$SE_DLL" ]; then
  echo "DLL timestamp:"
  docker exec vanan-shoperp ls -la "$SE_DLL" 2>/dev/null
  echo "Check for MissionsAdmin in DLL:"
  docker exec vanan-shoperp sh -c "strings '$SE_DLL' 2>/dev/null | grep -i 'MissionsAdmin\|Quản lý Nhiệm' | head -5" 2>/dev/null || echo "strings not available"
  echo "Check for MissionsController in DLL:"
  docker exec vanan-shoperp sh -c "strings '$SE_DLL' 2>/dev/null | grep -i 'MissionsController' | head -5" 2>/dev/null || echo "strings not available"
  echo "Check for BirthdayBonusJob in DLL:"
  docker exec vanan-shoperp sh -c "strings '$SE_DLL' 2>/dev/null | grep -i 'BirthdayBonusJob\|VoucherExpiryReminderJob' | head -5" 2>/dev/null || echo "strings not available"
fi

echo
echo "=== Gateway DLL check ==="
GW_DLL=$(docker exec vanan-gateway find /app -name "VanAn.Gateway.dll" 2>/dev/null | head -1)
echo "DLL path: $GW_DLL"
if [ -n "$GW_DLL" ]; then
  echo "DLL timestamp:"
  docker exec vanan-gateway ls -la "$GW_DLL" 2>/dev/null
  echo "Check for MissionsController in DLL:"
  docker exec vanan-gateway sh -c "strings '$GW_DLL' 2>/dev/null | grep -i 'MissionsController' | head -5" 2>/dev/null || echo "strings not available"
  echo "Check for CustomerProfileController in DLL:"
  docker exec vanan-gateway sh -c "strings '$GW_DLL' 2>/dev/null | grep -i 'CustomerProfileController' | head -5" 2>/dev/null || echo "strings not available"
fi

echo
echo "=== CoreHub DLL check (MissionService) ==="
CH_DLL=$(docker exec vanan-shoperp find /app -name "VanAn.CoreHub.dll" 2>/dev/null | head -1)
echo "DLL path: $CH_DLL"
if [ -n "$CH_DLL" ]; then
  echo "Check for MissionService in DLL:"
  docker exec vanan-shoperp sh -c "strings '$CH_DLL' 2>/dev/null | grep -i 'MissionService\|MissionRepository' | head -5" 2>/dev/null || echo "strings not available"
fi

echo
echo "=== ShopERP SQLite: check Missions table exists ==="
docker exec vanan-shoperp dotnet ef migrations list --no-build --project /app/VanAn.ShopERP.csproj 2>/dev/null | tail -5 || echo "EF CLI not available, trying alternative..."
