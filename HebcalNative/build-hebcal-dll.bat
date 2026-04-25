@echo off
REM Build HebcalNative.dll from Go.
REM Usage:  build-hebcal-dll.bat [x64|arm64]   (defaults to x64)
REM
REM Requirements:
REM   1. Go 1.21+ (https://go.dev/dl/)
REM   2. C compiler for CGO:
REM      - x64:   gcc from MinGW-w64    ("choco install mingw")
REM      - arm64: clang from LLVM-MinGW (https://github.com/mstorsjo/llvm-mingw)
REM               or "aarch64-w64-mingw32-gcc" on PATH

setlocal

set ARCH=%1
if "%ARCH%"=="" set ARCH=x64

if /I "%ARCH%"=="x64" (
  set GOARCH_VAL=amd64
  set CC_VAL=gcc
  set OUT_NAME=HebcalNative.dll
) else if /I "%ARCH%"=="arm64" (
  set GOARCH_VAL=arm64
  set OUT_NAME=HebcalNative.arm64.dll
  REM Prefer aarch64-w64-mingw32-gcc; fall back to clang with --target.
  where aarch64-w64-mingw32-gcc >nul 2>nul
  if not errorlevel 1 (
    set CC_VAL=aarch64-w64-mingw32-gcc
  ) else (
    where clang >nul 2>nul
    if errorlevel 1 (
      echo [error] no ARM64 cross-compiler found.
      echo         Install LLVM-MinGW from https://github.com/mstorsjo/llvm-mingw/releases
      echo         and ensure clang or aarch64-w64-mingw32-gcc is on PATH.
      exit /b 1
    )
    set CC_VAL=clang --target=aarch64-w64-mingw32
  )
) else (
  echo [error] unknown architecture "%ARCH%". Use x64 or arm64.
  exit /b 1
)

echo === Building HebcalNative.dll for %ARCH% ===

cd /d "%~dp0lib"
if errorlevel 1 (
  echo [error] lib directory not found
  exit /b 1
)

echo [1/3] go mod tidy
go mod tidy
if errorlevel 1 exit /b 1

echo [2/3] verifying compiler "%CC_VAL%"
for /f "tokens=1" %%a in ("%CC_VAL%") do set CC_BIN=%%a
where %CC_BIN% >nul 2>nul
if errorlevel 1 (
  echo [error] %CC_BIN% not found on PATH.
  exit /b 1
)

echo [3/3] go build -buildmode=c-shared
set CGO_ENABLED=1
set GOOS=windows
set GOARCH=%GOARCH_VAL%
set CC=%CC_VAL%

go build -buildmode=c-shared -o %OUT_NAME% hebcal_export.go
if errorlevel 1 (
  echo [error] go build failed
  exit /b 1
)

if not exist "%~dp0..\Resources" mkdir "%~dp0..\Resources"
move /Y %OUT_NAME% "%~dp0..\Resources\%OUT_NAME%"
if exist HebcalNative.h del HebcalNative.h
if exist HebcalNative.arm64.h del HebcalNative.arm64.h

echo ===
echo done: %~dp0..\Resources\%OUT_NAME%
echo ===

endlocal
