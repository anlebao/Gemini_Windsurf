@echo off
setlocal enabledelayedexpansion

echo ==========================================
echo 🧪 ACCOUNTING FULL TEST SUITE
echo ==========================================

REM ===== STEP 1: UNIT/INTEGRATION TESTS =====
echo 📦 Step 1: Unit/Integration Tests (6_Tests)...
cd 6_Tests
dotnet test --filter "FullyQualifiedName~Accounting"
if %errorlevel% neq 0 (
    echo ❌ Unit/Integration tests failed
    cd ..
    exit /b 1
)
echo ✅ Unit/Integration tests passed
cd ..

REM ===== STEP 2: BUILD SHOPERP (bắt buộc để code changes có hiệu lực) =====
echo 📦 Step 2: Build ShopERP...
cd 5_WebApps\ShopERP
dotnet build VanAn.ShopERP.csproj --configuration Debug -nologo -v q
if %errorlevel% neq 0 (
    echo ❌ ShopERP build failed
    cd ..\.. 
    exit /b 1
)
echo ✅ ShopERP build passed
cd ..\..

REM ===== STEP 3: START SHOPERP APP =====
REM NOTE: ShopERP uses SQLite with WAL mode (no PostgreSQL required)
REM ShopERP Program.cs reads ASPNETCORE_URLS via Configuration
echo 📦 Starting ShopERP app on http://localhost:5003 ...
start "ShopERP" cmd /c "set ASPNETCORE_URLS=http://localhost:5003 && cd 5_WebApps\ShopERP && dotnet run --no-build --no-launch-profile > ..\..\shoperp.log 2>&1"

REM ===== STEP 4: WAIT FOR SHOPERP READY =====
echo ⏳ Waiting for ShopERP to be ready (up to 60s)...
set /a count=0
:wait_app
timeout /t 3 > nul
curl -s -o nul -w "%%{http_code}" http://localhost:5003/login > tmp_status.txt 2>nul
set /p HTTP_STATUS=<tmp_status.txt
del tmp_status.txt 2>nul
if "%HTTP_STATUS%"=="200" goto app_ready
if "%HTTP_STATUS%"=="302" goto app_ready
set /a count+=1
if %count% geq 20 (
    echo ❌ ShopERP failed to start within 60s
    goto fail
)
goto wait_app
:app_ready
echo ✅ ShopERP is ready

REM ===== STEP 5: E2E TESTS =====
REM Chạy project e2e-tests (không dùng --project browser riêng lẻ) để tránh
REM parallel race condition trên SQLite khi nhiều projects cùng ghi DB.
echo 📦 Step 5: E2E Tests (6_Testing)...
cd 6_Testing
npx playwright test e2e-tests/accounting-*.spec.ts e2e-tests/balance-dashboard-flow.spec.ts e2e-tests/expense-entry-flow.spec.ts e2e-tests/export-excel-flow.spec.ts --project=e2e-tests --reporter=line
if %errorlevel% neq 0 (
    echo ❌ E2E tests failed
    cd ..
    goto fail
)
echo ✅ E2E tests passed
cd ..

REM ===== SUCCESS =====
echo ==========================================
echo 🎉 ALL ACCOUNTING TESTS PASSED
echo ==========================================
echo ✅ Unit/Integration Tests: PASSED
echo ✅ E2E Tests: PASSED
goto cleanup

:fail
echo ==========================================
echo 💥 ACCOUNTING TESTS FAILED
echo ==========================================
goto cleanup

:cleanup
echo 🧹 Cleaning up...
echo Stopping ShopERP app...
taskkill /FI "WINDOWTITLE eq ShopERP*" /F >nul 2>&1
REM NOTE: Không dùng /IM dotnet.exe vì sẽ kill tất cả dotnet process (kể cả CoreHub, Gateway...)

:end
echo Done.
pause
