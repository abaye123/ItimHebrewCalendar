@echo off
REM Full build: native DLLs + .NET publish + installers, for x64 and (best-effort) ARM64.
REM
REM Usage:  build.bat            -> both architectures
REM         build.bat x64        -> x64 only
REM         build.bat arm64      -> arm64 only
REM
REM Always prefers Visual Studio's MSBuild because the .NET SDK ships without
REM Microsoft.Build.Packaging.Pri.Tasks.dll, which Microsoft.WindowsAppSDK 1.7
REM requires for resource (PRI) generation.

setlocal EnableDelayedExpansion

set TARGET=%1
if "%TARGET%"=="" set TARGET=both
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

set USE_DOTNET=0
set MSBUILD_EXE=
call :find_msbuild

if not defined MSBUILD_EXE (
  REM No VS MSBuild - try dotnet publish as a fallback (likely to fail for WinUI 3).
  set HAVE_DOTNET=0
  for /f "tokens=1 delims=." %%v in ('dotnet --list-sdks 2^>nul') do set HAVE_DOTNET=1

  if !HAVE_DOTNET!==1 (
    echo [warn] Visual Studio MSBuild not found - falling back to dotnet publish.
    echo        WinUI 3 projects usually need VS MSBuild for the AppxPackage tasks.
    echo        Install VS 2022 with the ".NET desktop" workload if this fails.
    echo.
    set USE_DOTNET=1
  ) else (
    echo [error] no usable build backend found.
    echo         install Visual Studio 2022 with the ".NET desktop" workload,
    echo         or set MSBUILD_EXE manually before running this script:
    echo            set MSBUILD_EXE=C:\path\to\MSBuild.exe
    exit /b 1
  )
) else (
  echo [info] using Visual Studio MSBuild
  echo        !MSBUILD_EXE!
  echo.
)

if /I "%TARGET%"=="x64"   goto :build_x64
if /I "%TARGET%"=="arm64" goto :build_arm64
if /I "%TARGET%"=="both"  goto :build_x64

echo [error] unknown target "%TARGET%". Use x64, arm64, or both.
exit /b 1

:build_x64
echo === x64 ===
call "%~dp0HebcalNative\build-hebcal-dll.bat" x64
if errorlevel 1 goto :fail

cd /d "%~dp0"
call :publish win-x64 x64
if errorlevel 1 goto :fail

if exist %ISCC% (
  cd Installer
  %ISCC% /DArch=x64 HebDate.iss
  if errorlevel 1 (cd .. & goto :fail)
  call :ensure_runtime x64
  if not errorlevel 1 (
    %ISCC% /DArch=x64 /DBundleRuntime=1 HebDate.iss
  ) else (
    echo [warn] runtime download failed - skipping x64 offline installer
  )
  cd ..
) else (
  echo [warn] Inno Setup 6 not installed - skipping installer
)

if /I "%TARGET%"=="x64" goto :done

:build_arm64
echo.
echo === ARM64 ===
call "%~dp0HebcalNative\build-hebcal-dll.bat" arm64
if errorlevel 1 (
  echo [warn] ARM64 native DLL build failed - skipping ARM64
  goto :done
)

cd /d "%~dp0"
call :publish win-arm64 ARM64
if errorlevel 1 (
  echo [warn] ARM64 publish failed - skipping ARM64
  goto :done
)

if exist %ISCC% (
  cd Installer
  %ISCC% /DArch=arm64 HebDate.iss
  if not errorlevel 1 (
    call :ensure_runtime arm64
    if not errorlevel 1 (
      %ISCC% /DArch=arm64 /DBundleRuntime=1 HebDate.iss
    ) else (
      echo [warn] runtime download failed - skipping arm64 offline installer
    )
  )
  cd ..
)

:done
echo.
echo === build complete ===
echo Outputs in Release\:
dir /b Release\ItimHebrewCalendar-Setup-*.exe 2>nul
endlocal
exit /b 0

:fail
echo === build failed ===
endlocal
exit /b 1

:publish
REM Args:  %1 = RID (win-x64 / win-arm64), %2 = Platform (x64 / ARM64)
if %USE_DOTNET%==1 (
  dotnet publish -c Release -r %1 --self-contained false -v:m
  exit /b %errorlevel%
)

REM VS MSBuild path. We must set up the VS Developer environment first so that
REM WinAppSDK's MSBuild tasks (XAML compiler, PRI generator) load correctly.
REM Without VsDevCmd.bat, MSBuild "succeeds" but silently skips XAML/PRI steps.
for %%a in ("%MSBUILD_EXE%\..\..\..\..") do set "VS_INSTALL=%%~fa"
set "VSDEVCMD=%VS_INSTALL%\Common7\Tools\VsDevCmd.bat"

if exist "%VSDEVCMD%" (
  call "%VSDEVCMD%" -arch=%2 -no_logo >nul
  msbuild ItimHebrewCalendar.csproj -restore -t:Publish -p:Configuration=Release -p:Platform=%2 -p:RuntimeIdentifier=%1 -p:SelfContained=false -v:m
) else (
  echo [warn] VsDevCmd.bat not found at %VSDEVCMD%
  echo        invoking MSBuild directly - WinUI XAML/PRI generation may be skipped
  "%MSBUILD_EXE%" -restore -t:Publish -p:Configuration=Release -p:Platform=%2 -p:RuntimeIdentifier=%1 -p:SelfContained=false -v:m
)
exit /b %errorlevel%

:ensure_runtime
REM Args: %1 = arch (x64/arm64). Downloads windowsappruntimeinstall-<arch>.exe
REM into Installer\Redist\ if missing. Returns 0 on success, 1 on failure.
set "REDIST=%~dp0Installer\Redist"
if exist "%REDIST%\windowsappruntimeinstall-%1.exe" exit /b 0
if not exist "%REDIST%" mkdir "%REDIST%"
echo [info] downloading Windows App Runtime 1.7 (%1)...
powershell -NoProfile -Command "$ProgressPreference='SilentlyContinue'; try { Invoke-WebRequest 'https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-%1.exe' -OutFile '%REDIST%\windowsappruntimeinstall-%1.exe' -UseBasicParsing; exit 0 } catch { exit 1 }"
exit /b %errorlevel%

:find_msbuild
REM Try vswhere in known locations.
for %%p in (
  "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
  "%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"
) do (
  if exist %%p (
    for /f "usebackq delims=" %%i in (`%%p -latest -prerelease -property installationPath 2^>nul`) do (
      if exist "%%i\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_EXE=%%i\MSBuild\Current\Bin\MSBuild.exe"
        goto :eof
      )
    )
  )
)

REM Fallback: scan well-known VS 2022 install paths directly.
for %%r in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
  for %%v in (Enterprise Professional Community BuildTools Preview) do (
    if exist "%%~r\Microsoft Visual Studio\2022\%%v\MSBuild\Current\Bin\MSBuild.exe" (
      set "MSBUILD_EXE=%%~r\Microsoft Visual Studio\2022\%%v\MSBuild\Current\Bin\MSBuild.exe"
      goto :eof
    )
  )
)
goto :eof
