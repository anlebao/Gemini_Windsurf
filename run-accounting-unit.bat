@echo off
setlocal enabledelayedexpansion

echo ==========================================
echo 🧪 ACCOUNTING UNIT/INTEGRATION TESTS
echo ==========================================
echo ℹ️  This script runs only Unit/Integration tests (no Docker required)
echo ℹ️  For full test suite including E2E, use run-accounting-full.bat
echo ==========================================

REM ===== STEP 1: UNIT/INTEGRATION TESTS =====
echo 📦 Running Unit/Integration Tests (6_Tests)...
cd 6_Tests
dotnet test --filter "FullyQualifiedName~Accounting"
if %errorlevel% neq 0 (
    echo ❌ Unit/Integration tests failed
    cd ..
    exit /b 1
)
echo ✅ Unit/Integration tests passed
cd ..

REM ===== SUCCESS =====
echo ==========================================
echo 🎉 ACCOUNTING UNIT/INTEGRATION TESTS PASSED
echo ==========================================
echo ✅ Unit/Integration Tests: PASSED
echo ℹ️  E2E Tests: SKIPPED (requires Docker)
echo ==========================================

:end
echo Done.
pause
