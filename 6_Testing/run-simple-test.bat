@echo off
echo ========================================
echo VanAn Ecosystem - Simple Performance Test
echo ========================================
echo.

REM Create logs directory
if not exist "logs" mkdir logs

REM Generate timestamp
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "timestamp=%dt:~0,4%-%dt:~4,2%-%dt:~6,2%_%dt:~8,2%-%dt:~10,2%-%dt:~12,2%"

echo Starting simple performance test at %date% %time%
echo Target: http://localhost:5001 (Gateway API)
echo Log file: logs\simple-test-%timestamp%.txt
echo.

REM Check if k6 is installed
where k6 >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: k6 is not installed
    echo Please install k6 from: https://k6.io/docs/getting-started/installation/
    pause
    exit /b 1
)

echo.
echo ========================================
echo RUNNING SIMPLE PERFORMANCE TEST
echo ========================================
echo.

REM Run simple test without Gateway check
k6 run ^
  --vus 5 ^
  --duration 30s ^
  --console-output=logs\simple-test-%timestamp%.txt ^
  load-tests\100-orders-1min.js

if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo TEST COMPLETED
    echo ========================================
    echo.
    echo Results saved to: logs\simple-test-%timestamp%.txt
    
    REM Count orders
    findstr /i "ORDER_TEST:" logs\simple-test-%timestamp%.txt > temp-count.txt 2>nul
    for /f %%i in ('type temp-count.txt 2^>nul ^| find /c /v ""') do set ordercount=%%i
    echo Total orders processed: %ordercount%
    del temp-count.txt 2>nul
    
) else (
    echo.
    echo ========================================
    echo TEST FAILED
    echo ========================================
)

echo.
echo Press any key to open log file...
pause >nul

if exist logs\simple-test-%timestamp%.txt (
    notepad logs\simple-test-%timestamp%.txt
)

pause
