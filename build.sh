#!/bin/bash
# Build script for AnonWallClient
# Usage: ./build.sh [platform] [configuration]
# Platforms: android, windows, ios, macos, all
# Configurations: Debug, Release

set -e

PROJECT="AnonWallClient.csproj"
PLATFORM=${1:-"all"}
CONFIG=${2:-"Release"}

echo "?? Building AnonWallClient"
echo "Platform: $PLATFORM"
echo "Configuration: $CONFIG"
echo ""

# Function to build for specific platform
build_platform() {
    local framework=$1
    local platform_name=$2
    
    echo "?? Building for $platform_name..."
    
    if ! dotnet build "$PROJECT" -c "$CONFIG" -f "$framework" --no-restore; then
        echo "? Build failed for $platform_name"
        return 1
    fi
    
    echo "? $platform_name build completed"
    echo ""
}

# Function to publish and package
publish_platform() {
    local framework=$1
    local platform_name=$2
    local output_dir="./artifacts/$platform_name"
    
    echo "?? Publishing $platform_name..."
    
    # Create output directory
    mkdir -p "$output_dir"
    
    if ! dotnet publish "$PROJECT" -c "$CONFIG" -f "$framework" -o "$output_dir"; then
        echo "? Publish failed for $platform_name"
        return 1
    fi
    
    echo "? $platform_name published to $output_dir"
    echo ""
}

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "? .NET SDK not found. Please install .NET 8.0 SDK"
    exit 1
fi

# Check if MAUI workload is installed
if ! dotnet workload list | grep -q "maui"; then
    echo "?? Installing MAUI workload..."
    dotnet workload install maui
fi

# Restore dependencies
echo "?? Restoring dependencies..."
if ! dotnet restore "$PROJECT"; then
    echo "? Failed to restore dependencies"
    exit 1
fi
echo "? Dependencies restored"
echo ""

# Build based on platform parameter
case $PLATFORM in
    "android")
        build_platform "net8.0-android" "Android"
        if [ "$CONFIG" = "Release" ]; then
            publish_platform "net8.0-android" "android"
        fi
        ;;
    "windows")
        build_platform "net8.0-windows10.0.19041.0" "Windows"
        if [ "$CONFIG" = "Release" ]; then
            publish_platform "net8.0-windows10.0.19041.0" "windows"
        fi
        ;;
    "ios")
        if [[ "$OSTYPE" == "darwin"* ]]; then
            build_platform "net8.0-ios" "iOS"
            if [ "$CONFIG" = "Release" ]; then
                publish_platform "net8.0-ios" "ios"
            fi
        else
            echo "??  iOS builds require macOS"
        fi
        ;;
    "macos")
        if [[ "$OSTYPE" == "darwin"* ]]; then
            build_platform "net8.0-maccatalyst" "macOS"
            if [ "$CONFIG" = "Release" ]; then
                publish_platform "net8.0-maccatalyst" "macos"
            fi
        else
            echo "??  macOS builds require macOS"
        fi
        ;;
    "all")
        # Build for all available platforms
        build_platform "net8.0-android" "Android"
        
        if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
            build_platform "net8.0-windows10.0.19041.0" "Windows"
        fi
        
        if [[ "$OSTYPE" == "darwin"* ]]; then
            build_platform "net8.0-ios" "iOS"
            build_platform "net8.0-maccatalyst" "macOS"
            build_platform "net8.0-windows10.0.19041.0" "Windows"
        fi
        
        # Publish if Release configuration
        if [ "$CONFIG" = "Release" ]; then
            echo "?? Publishing all platforms..."
            publish_platform "net8.0-android" "android"
            
            if [[ "$OSTYPE" == "darwin"* ]]; then
                publish_platform "net8.0-ios" "ios"
                publish_platform "net8.0-maccatalyst" "macos"
                publish_platform "net8.0-windows10.0.19041.0" "windows"
            elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "win32" ]]; then
                publish_platform "net8.0-windows10.0.19041.0" "windows"
            fi
        fi
        ;;
    *)
        echo "? Unknown platform: $PLATFORM"
        echo "Available platforms: android, windows, ios, macos, all"
        exit 1
        ;;
esac

echo "?? Build process completed successfully!"

if [ "$CONFIG" = "Release" ] && [ -d "./artifacts" ]; then
    echo ""
    echo "?? Published artifacts available in:"
    find ./artifacts -name "*.apk" -o -name "*.exe" -o -name "*.app" 2>/dev/null | head -10
fi