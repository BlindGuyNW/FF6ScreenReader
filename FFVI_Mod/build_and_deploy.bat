@echo off
setlocal enabledelayedexpansion

REM ============================================================
REM FFVI Screen Reader - Build and Deploy
REM ============================================================
REM
REM Game directory is auto-detected from Steam. To override,
REM set the FFVI_GAME_DIR environment variable before running.
REM ============================================================

set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%FFVI_ScreenReader.csproj"
set "GAME_NAME=FINAL FANTASY VI PR"

REM --- Resolve game directory ---
if defined FFVI_GAME_DIR (
  echo Using FFVI_GAME_DIR: "%FFVI_GAME_DIR%"
  set "GAME_DIR=%FFVI_GAME_DIR%"
  goto :found_game
)

echo Searching for %GAME_NAME%...

REM Try Steam registry (64-bit)
set "STEAM_PATH="
for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\WOW6432Node\Valve\Steam" /v InstallPath 2^>nul') do set "STEAM_PATH=%%b"

REM Fallback: current user registry
if not defined STEAM_PATH (
  for /f "tokens=2*" %%a in ('reg query "HKCU\SOFTWARE\Valve\Steam" /v SteamPath 2^>nul') do set "STEAM_PATH=%%b"
)

if not defined STEAM_PATH (
  echo ERROR: Could not find Steam in the Windows registry.
  echo.
  echo Set the FFVI_GAME_DIR environment variable to your game install folder:
  echo   set "FFVI_GAME_DIR=D:\path\to\steamapps\common\FINAL FANTASY VI PR"
  echo Then run this script again.
  echo.
  pause
  exit /b 1
)

REM Normalize forward slashes (HKCU sometimes uses them)
set "STEAM_PATH=%STEAM_PATH:/=\%"
echo Found Steam at: "%STEAM_PATH%"

REM Check default Steam library first
if exist "%STEAM_PATH%\steamapps\common\%GAME_NAME%" (
  set "GAME_DIR=%STEAM_PATH%\steamapps\common\%GAME_NAME%"
  goto :found_game
)

REM Parse libraryfolders.vdf for additional Steam libraries
set "VDF=%STEAM_PATH%\steamapps\libraryfolders.vdf"
if not exist "%VDF%" goto :not_found

for /f "usebackq delims=" %%L in ("%VDF%") do (
  set "LINE=%%L"
  if not "!LINE:path=!"=="!LINE!" (
    REM Extract path value: strip "path" prefix, remove quotes, fix backslashes
    set "VAL=!LINE:*path=!"
    set VAL=!VAL:"=!
    set "VAL=!VAL:\\=\!"
    for /f "tokens=*" %%V in ("!VAL!") do (
      if exist "%%V\steamapps\common\%GAME_NAME%" (
        set "GAME_DIR=%%V\steamapps\common\%GAME_NAME%"
        goto :found_game
      )
    )
  )
)

:not_found
echo ERROR: Could not find %GAME_NAME% in any Steam library.
echo.
echo Set the FFVI_GAME_DIR environment variable to your game install folder:
echo   set "FFVI_GAME_DIR=D:\path\to\steamapps\common\FINAL FANTASY VI PR"
echo Then run this script again.
echo.
pause
exit /b 1

:found_game
set "OUTPUT_DLL=%SCRIPT_DIR%bin\Release\net6.0\FFVI_ScreenReader.dll"
set "MODS_DIR=%GAME_DIR%\Mods"

echo.
echo ==========================================
echo  Building FFVI Screen Reader (Release)
echo ==========================================
echo  Project : "%PROJECT%"
echo  Game    : "%GAME_DIR%"
echo  Mods    : "%MODS_DIR%"
echo ==========================================
echo.

REM --- Sanity checks ---
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: dotnet not found. Install the .NET 6 SDK and try again.
  echo.
  pause
  exit /b 1
)

if not exist "%PROJECT%" (
  echo ERROR: Project file not found: "%PROJECT%"
  echo.
  pause
  exit /b 1
)

if not exist "%GAME_DIR%" (
  echo ERROR: Game directory not found: "%GAME_DIR%"
  echo.
  echo If the game was moved, update FFVI_GAME_DIR and try again.
  pause
  exit /b 1
)

REM --- Build ---
dotnet build "%PROJECT%" -c Release /p:GameDir="%GAME_DIR%"
if errorlevel 1 (
  echo.
  echo ERROR: Build failed. See errors above.
  echo.
  pause
  exit /b 1
)

REM --- Verify DLL ---
if not exist "%OUTPUT_DLL%" (
  echo.
  echo ERROR: Build succeeded but DLL not found at: "%OUTPUT_DLL%"
  echo.
  pause
  exit /b 1
)

REM --- Deploy ---
if not exist "%MODS_DIR%" (
  echo Creating Mods folder: "%MODS_DIR%"
  mkdir "%MODS_DIR%"
)

echo Copying DLL to Mods folder...
copy /Y "%OUTPUT_DLL%" "%MODS_DIR%\FFVI_ScreenReader.dll" >nul
if errorlevel 1 (
  echo ERROR: Copy failed. Check permissions or try running as Administrator.
  echo.
  pause
  exit /b 1
)

echo.
echo SUCCESS: Deployed to "%MODS_DIR%\FFVI_ScreenReader.dll"
echo.
pause
exit /b 0
