@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM FFVI Screen Reader - Build and Deploy (Windows)
REM ============================================================

REM --- Set your game install folder here (must match your actual install path) ---
set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY VI PR"

set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%FFVI_ScreenReader.csproj"
set "OUTPUT_DLL=%SCRIPT_DIR%bin\Release\net6.0\FFVI_ScreenReader.dll"
set "MODS_DIR=%GAME_DIR%\Mods"

echo.
echo ==========================================
echo Building FFVI Screen Reader (Release)...
echo Project: "%PROJECT%"
echo GameDir : "%GAME_DIR%"
echo ModsDir : "%MODS_DIR%"
echo ==========================================
echo.

REM --- Sanity checks ---
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet was not found. Install the .NET 6 SDK and try again.
  echo.
  pause
  exit /b 1
)

if not exist "%PROJECT%" (
  echo ERROR: Could not find project file:
  echo "%PROJECT%"
  echo.
  pause
  exit /b 1
)

if not exist "%GAME_DIR%" (
  echo ERROR: GAME_DIR does not exist:
  echo "%GAME_DIR%"
  echo.
  echo Update GAME_DIR at the top of this .bat file to your actual FFVI install folder.
  pause
  exit /b 1
)

REM --- Build ---
dotnet build "%PROJECT%" -c Release
if errorlevel 1 (
  echo.
  echo ERROR: Build failed. Scroll up for the compiler error.
  echo.
  pause
  exit /b 1
)

REM --- Verify output DLL exists ---
if not exist "%OUTPUT_DLL%" (
  echo.
  echo ERROR: Build succeeded but DLL not found at:
  echo "%OUTPUT_DLL%"
  echo.
  pause
  exit /b 1
)

REM --- Ensure Mods directory exists ---
if not exist "%MODS_DIR%" (
  echo.
  echo Mods folder not found. Creating:
  echo "%MODS_DIR%"
  mkdir "%MODS_DIR%"
)

REM --- Deploy ---
echo.
echo Copying DLL to Mods folder...
copy /Y "%OUTPUT_DLL%" "%MODS_DIR%\FFVI_ScreenReader.dll" >nul
if errorlevel 1 (
  echo ERROR: Copy failed (permissions or path issue).
  echo Try running this script as Administrator, or check your Steam library path.
  echo.
  pause
  exit /b 1
)

echo.
echo SUCCESS: Deployed "%MODS_DIR%\FFVI_ScreenReader.dll"
echo Output DLL: "%OUTPUT_DLL%"
echo.
pause
exit /b 0
