@echo off
REM ============================================================================
REM  ItimHebrewCalendar - בנייה מלאה (DLL + EXE + setup.exe)
REM ============================================================================

setlocal

echo ========================================
echo ItimHebrewCalendar - בנייה מלאה
echo ========================================
echo.

echo [שלב 1/3] בונה את HebcalNative.dll...
call "%~dp0HebcalNative\build-hebcal-dll.bat"
if errorlevel 1 (
  echo [שגיאה] בניית ה-DLL נכשלה
  exit /b 1
)

echo.
echo [שלב 2/3] בונה את ה-EXE באמצעות dotnet publish...
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained false
if errorlevel 1 (
  echo [שגיאה] dotnet publish נכשל
  exit /b 1
)

echo.
echo [שלב 3/3] בונה את setup.exe באמצעות Inno Setup...

set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
  echo [אזהרה] Inno Setup 6 לא מותקן בנתיב הרגיל.
  echo         הורד מ: https://jrsoftware.org/isinfo.php
  echo         דילוג על יצירת setup.exe.
  goto :done
)

cd Installer
%ISCC% ItimHebrewCalendar.iss
if errorlevel 1 (
  echo [שגיאה] Inno Setup נכשל
  exit /b 1
)

:done
echo.
echo ========================================
echo הבנייה הסתיימה בהצלחה!
echo.
echo קבצי הפלט:
echo   EXE:   bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\ItimHebrewCalendar.exe
echo   Setup: Release\ItimHebrewCalendar-Setup-1.0.0.exe
echo ========================================

endlocal
