@echo off
setlocal enabledelayedexpansion

echo ==========================================
echo 🚀 ecoNexus ONE-CLICK START + TEST (V2)
echo ==========================================

REM ===== 0. DEFAULT MODE =====
set DEFAULT_MODE=smoke

REM ===== 1. READ PARAM =====
set MODE=%1
if "%MODE%"=="" (
    set MODE=%DEFAULT_MODE%
)
echo 🎯 Mode: %MODE%

REM ===== 2. RESET FLAGS =====
set SMOKE=true
set E2E=false
set LOAD=false
set CHAOS=false

REM ===== 3. MODE SWITCH =====
if /i "%MODE%"=="smoke" (
    set E2E=false
    set LOAD=false
    set CHAOS=false
)
if /i "%MODE%"=="e2e" (
    set E2E=true
)
if /i "%MODE%"=="load" (
    set E2E=true
    set LOAD=true
)
if /i "%MODE%"=="chaos" (
    set E2E=true
    set LOAD=true
    set CHAOS=true
)
if /i "%MODE%"=="full" (
    set E2E=true
    set LOAD=true
    set CHAOS=true
)

REM ===== 4. FALLBACK TO .env.test =====
if exist 6_Testing\.env.test (
    echo 📦 Loading fallback from .env.test...
    for /f "tokens=1,2 delims==" %%a in (6_Testing\.env.test) do (
        if /i "%%a"=="ENABLE_E2E" if "%MODE%"=="custom" set E2E=%%b
        if /i "%%a"=="ENABLE_LOAD_TEST" if "%MODE%"=="custom" set LOAD=%%b
        if /i "%%a"=="ENABLE_CHAOS" if "%MODE%"=="custom" set CHAOS=%%b
    )
)
echo Flags: E2E=%E2E% LOAD=%LOAD% CHAOS=%CHAOS%

REM ===== 5. BUILD SERVICE LIST =====
set SERVICES=postgres
if "%E2E%"=="true" (
    set SERVICES=%SERVICES% seq nats
)
if "%LOAD%"=="true" (
    set SERVICES=%SERVICES% nats
)
if "%CHAOS%"=="true" (
    set SERVICES=%SERVICES% seq nats
)

echo 📦 Services:
echo %SERVICES%

REM ===== 6. START DOCKER =====
echo 🚀 Starting containers...
docker-compose up -d %SERVICES%
if %errorlevel% neq 0 (
    echo ❌ Docker start failed
    exit /b 1
)

REM ===== 7. WAIT =====
echo ⏳ Waiting for services...
timeout /t 10 > nul

REM ===== 8. HEALTH CHECK =====
echo 🩺 Checking health...
call :check postgres:5432 PostgreSQL
echo ✅ All services healthy

REM ===== 9. RUN TEST =====
echo 🧪 Running tests...
cd 6_Testing
if "%CHAOS%"=="true" (
    call npm run test:quality-gate:full
) else (
    call npm run test:quality-gate
)
if %errorlevel% neq 0 (
    echo ❌ TEST FAILED
    cd ..
    goto fail
)
cd ..

echo ==========================================
echo 🎉 ALL TEST PASSED
echo ==========================================
goto end

:check
docker exec %1 pg_isready -U vanan_admin -d VanAnCoreHub >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ %2 failed
    goto fail
)
exit /b 0

:fail
echo ==========================================
echo 💥 SYSTEM FAILED
echo ==========================================
docker-compose logs --tail=50
goto cleanup

:cleanup
echo 🧹 Cleaning up...
docker-compose down

:end
echo Done.
pause
