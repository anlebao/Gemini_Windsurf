@echo off
echo ========================================
echo VanAn Ecosystem - 100 Orders Performance Test
echo ========================================
echo.

REM Create logs directory if not exists
if not exist "logs" mkdir logs

REM Generate timestamp for log file
for /f "tokens=2 delims==" %%a in ('wmic OS Get localdatetime /value') do set "dt=%%a"
set "YYYY=%dt:~0,4%"
set "MM=%dt:~4,2%"
set "DD=%dt:~6,2%"
set "HH=%dt:~8,2%"
set "Min=%dt:~10,2%"
set "Sec=%dt:~12,2%"
set "timestamp=%YYYY%-%MM%-%DD%_%HH%-%Min%-%Sec%"

echo Starting performance test at %date% %time%
echo Log file: logs\100-orders-test-%timestamp%.txt
echo.

REM Check if k6 is installed
where k6 >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: k6 is not installed or not in PATH
    echo Please install k6 from: https://k6.io/docs/getting-started/installation/
    pause
    exit /b 1
)

REM Check if Gateway is running
echo Checking if Gateway API is available...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:5001/health' -UseBasicParsing -TimeoutSec 5; Write-Output $response.StatusCode } catch { Write-Output 'Error' }" >nul 2>&1
if %errorlevel% neq 0 (
    echo WARNING: Gateway API may not be running on http://localhost:5001
    echo Please ensure the Gateway service is started before running this test
    echo.
    echo Starting Gateway service...
    cd ..\..\2_Gateway
    start /B dotnet run --urls "http://localhost:5001" > ..\6_Testing\logs\gateway-%timestamp%.log 2>&1
    echo Gateway started in background. Waiting 10 seconds...
    timeout /t 10 /nobreak >nul
    cd ..\6_Testing
)

echo.
echo ========================================
echo RUNNING 100 ORDERS IN 1 MINUTE TEST
echo ========================================
echo.

REM Run k6 test with file logging
k6 run ^
  --vus 10 ^
  --duration 60s ^
  --out json=logs\100-orders-metrics-%timestamp%.json ^
  --console-output=logs\100-orders-test-%timestamp%.txt ^
  load-tests\100-orders-1min.js

REM Check test results
if %errorlevel% equ 0 (
    echo.
    echo ========================================
    echo TEST COMPLETED SUCCESSFULLY
    echo ========================================
    echo.
    echo Results saved to:
    echo   - Console log: logs\100-orders-test-%timestamp%.txt
    echo   - JSON metrics: logs\100-orders-metrics-%timestamp%.json
    echo   - Gateway log: logs\gateway-%timestamp%.log
    echo.
    echo Analyzing results...
    
    REM Display summary from log file
    echo.
    echo === TEST SUMMARY ===
    findstr /i "ORDER_TEST:" logs\100-orders-test-%timestamp%.txt > temp-summary.txt 2>nul
    for /f %%i in ('type temp-summary.txt 2^>nul ^| find /c /v ""') do set ordercount=%%i
    echo Total orders processed: %ordercount%
    del temp-summary.txt 2>nul
    
) else (
    echo.
    echo ========================================
    echo TEST FAILED
    echo ========================================
    echo.
    echo Check the log file for details: logs\100-orders-test-%timestamp%.txt
)

echo.
echo Press any key to open the log file...
pause >nul

REM Open log file in notepad
if exist logs\100-orders-test-%timestamp%.txt (
    notepad logs\100-orders-test-%timestamp%.txt
) else (
    echo Log file not found. Check logs directory.
)

echo.
echo Test completed. Check the logs folder for detailed results.
pause
