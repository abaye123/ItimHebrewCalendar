@echo off
REM ============================================================================
REM  בניית HebcalNative.dll מקוד Go
REM ============================================================================
REM  דרישות:
REM    1. Go 1.21+ מותקן (https://go.dev/dl/)
REM    2. MinGW-w64 עבור gcc (תלוי בהגדרת CGO)
REM       Chocolatey: choco install mingw
REM       או: https://www.mingw-w64.org/downloads/
REM    3. הרץ מהתיקייה HebcalNative\ של הפרויקט
REM ============================================================================

setlocal

echo ========================================
echo ItimHebrewCalendar - בונה את HebcalNative.dll
echo ========================================
echo.

REM עבור לתיקיית ה-lib
cd /d "%~dp0lib"
if errorlevel 1 (
  echo [שגיאה] תיקיית lib לא נמצאה
  exit /b 1
)

echo [1/3] מוריד תלויות Go...
go mod tidy
if errorlevel 1 (
  echo [שגיאה] go mod tidy נכשל
  exit /b 1
)

echo.
echo [2/3] מוודא ש-gcc נגיש...
where gcc >nul 2>nul
if errorlevel 1 (
  echo [שגיאה] gcc לא נמצא ב-PATH.
  echo התקן MinGW-w64 דרך chocolatey:  choco install mingw
  echo או הורד מ: https://www.mingw-w64.org/
  exit /b 1
)

echo.
echo [3/3] מקמפל את ה-DLL...
set CGO_ENABLED=1
set GOOS=windows
set GOARCH=amd64

go build -buildmode=c-shared -o HebcalNative.dll hebcal_export.go
if errorlevel 1 (
  echo [שגיאה] הקומפילציה נכשלה
  exit /b 1
)

REM העבר ל-Resources של הפרויקט
if not exist "%~dp0..\Resources" mkdir "%~dp0..\Resources"
move /Y HebcalNative.dll "%~dp0..\Resources\HebcalNative.dll"
if exist HebcalNative.h del HebcalNative.h

echo.
echo ========================================
echo הצלחה! הקובץ זמין ב:
echo   %~dp0..\Resources\HebcalNative.dll
echo ========================================

endlocal
