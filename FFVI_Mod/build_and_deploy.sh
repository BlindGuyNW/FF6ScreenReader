#!/bin/bash

# FFVI Screen Reader Mod Build and Deploy Script
# This script builds the mod and copies it to the game's Mods directory

set -e

echo "Building FFVI Screen Reader Mod..."

# Clean previous build
if [ -d "bin" ]; then
    rm -rf bin
fi

if [ -d "obj" ]; then
    rm -rf obj
fi

# Build the project
dotnet build FFVI_ScreenReader.csproj --configuration Release --verbosity minimal

# Check if build was successful
if [ ! -f "bin/Release/net6.0/FFVI_ScreenReader.dll" ]; then
    echo "Build failed! DLL not found."
    exit 1
fi

# Deploy to game directory
GAME_MODS_DIR="/mnt/c/games/Final Fantasy I-VI Bundle Pixel Remaster/FF6/Mods"
echo "Deploying to: $GAME_MODS_DIR"

# Create Mods directory if it doesn't exist
mkdir -p "$GAME_MODS_DIR"

# Copy the DLL
cp "bin/Release/net6.0/FFVI_ScreenReader.dll" "$GAME_MODS_DIR/"

echo "Build and deployment complete!"
echo "Mod DLL copied to: $GAME_MODS_DIR/FFVI_ScreenReader.dll"
echo ""
echo "To use this mod:"
echo "1. Make sure MelonLoader is installed in your FFVI game directory"
echo "2. Download Tolk.dll from https://github.com/dkager/tolk/releases"
echo "3. Place Tolk.dll in the game's main directory (next to the game exe)"
echo "4. Run the game - the mod should load automatically"
echo ""
echo "The mod will announce menu selections when you navigate with arrow keys."