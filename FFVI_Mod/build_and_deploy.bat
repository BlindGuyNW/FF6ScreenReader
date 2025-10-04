@echo off
setlocal

echo Building FFVI Screen Reader Mod...

REM Clean previous build
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj

REM Build the project
dotnet build FFVI_ScreenReader.csproj --configuration Release --verbosity minimal

REM Check if build was successful
if not exist "bin\Release\net6.0\FFVI_ScreenReader.dll" (
    echo Build failed! DLL not found.
    pause
    exit /b 1
)

REM Deploy to game directory
set "GAME_MODS_DIR=C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY VI PR\Mods"
echo Deploying to: %GAME_MODS_DIR%

REM Create Mods directory if it doesn't exist
if not exist "%GAME_MODS_DIR%" mkdir "%GAME_MODS_DIR%"

REM Copy the DLL
copy "bin\Release\net6.0\FFVI_ScreenReader.dll" "%GAME_MODS_DIR%\"

echo.
echo Build and deployment complete!
echo Mod DLL copied to: %GAME_MODS_DIR%\FFVI_ScreenReader.dll
echo.
echo To use this mod:
echo 1. Make sure MelonLoader is installed in your FFVI game directory
echo 2. Download Tolk.dll from https://github.com/dkager/tolk/releases
echo 3. Place Tolk.dll in the game's main directory (next to the game exe)
echo 4. Run the game - the mod should load automatically
echo.
echo The mod will announce menu selections when you navigate with arrow keys.
echo.
pause