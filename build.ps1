# Build script for AnonWallClient (PowerShell)
# Usage: .\build.ps1 [Platform] [Configuration]
# Platforms: android, windows, ios, macos, all
# Configurations: Debug, Release

param(
    [Parameter(Position=0)]
    [ValidateSet("android", "windows", "ios", "macos", "all")]
    [string]$Platform = "all",
    
    [Parameter(Position=1)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$PROJECT = "AnonWallClient.csproj"

Write-Host "?? Building AnonWallClient" -ForegroundColor Green
Write-Host "Platform: $Platform" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host ""

# Function to build for specific platform
function Build-Platform {
    param(
        [string]$Framework,
        [string]$PlatformName
    )
    
    Write-Host "?? Building for $PlatformName..." -ForegroundColor Yellow
    
    try {
        dotnet build $PROJECT -c $Configuration -f $Framework --no-restore
        Write-Host "? $PlatformName build completed" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "? Build failed for $PlatformName" -ForegroundColor Red
        throw
    }
}

# Function to publish and package
function Publish-Platform {
    param(
        [string]$Framework,
        [string]$PlatformName
    )
    
    $OutputDir = "./artifacts/$PlatformName"
    
    Write-Host "?? Publishing $PlatformName..." -ForegroundColor Yellow
    
    # Create output directory
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    
    try {
        dotnet publish $PROJECT -c $Configuration -f $Framework -o $OutputDir
        Write-Host "? $PlatformName published to $OutputDir" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "? Publish failed for $PlatformName" -ForegroundColor Red
        throw
    }
}

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "? .NET SDK version: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Host "? .NET SDK not found. Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Check if MAUI workload is installed
$workloads = dotnet workload list
if ($workloads -notmatch "maui") {
    Write-Host "?? Installing MAUI workload..." -ForegroundColor Yellow
    dotnet workload install maui
}

# Restore dependencies
Write-Host "?? Restoring dependencies..." -ForegroundColor Yellow
try {
    dotnet restore $PROJECT
    Write-Host "? Dependencies restored" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "? Failed to restore dependencies" -ForegroundColor Red
    exit 1
}

# Build based on platform parameter
switch ($Platform) {
    "android" {
        Build-Platform "net8.0-android" "Android"
        if ($Configuration -eq "Release") {
            Publish-Platform "net8.0-android" "android"
        }
    }
    "windows" {
        Build-Platform "net8.0-windows10.0.19041.0" "Windows"
        if ($Configuration -eq "Release") {
            Publish-Platform "net8.0-windows10.0.19041.0" "windows"
        }
    }
    "ios" {
        if ($IsMacOS) {
            Build-Platform "net8.0-ios" "iOS"
            if ($Configuration -eq "Release") {
                Publish-Platform "net8.0-ios" "ios"
            }
        } else {
            Write-Host "??  iOS builds require macOS" -ForegroundColor Yellow
        }
    }
    "macos" {
        if ($IsMacOS) {
            Build-Platform "net8.0-maccatalyst" "macOS"
            if ($Configuration -eq "Release") {
                Publish-Platform "net8.0-maccatalyst" "macos"
            }
        } else {
            Write-Host "??  macOS builds require macOS" -ForegroundColor Yellow
        }
    }
    "all" {
        # Build for all available platforms
        Build-Platform "net8.0-android" "Android"
        
        if ($IsWindows) {
            Build-Platform "net8.0-windows10.0.19041.0" "Windows"
        }
        
        if ($IsMacOS) {
            Build-Platform "net8.0-ios" "iOS"
            Build-Platform "net8.0-maccatalyst" "macOS"
            Build-Platform "net8.0-windows10.0.19041.0" "Windows"
        }
        
        # Publish if Release configuration
        if ($Configuration -eq "Release") {
            Write-Host "?? Publishing all platforms..." -ForegroundColor Yellow
            Publish-Platform "net8.0-android" "android"
            
            if ($IsMacOS) {
                Publish-Platform "net8.0-ios" "ios"
                Publish-Platform "net8.0-maccatalyst" "macos"
                Publish-Platform "net8.0-windows10.0.19041.0" "windows"
            } elseif ($IsWindows) {
                Publish-Platform "net8.0-windows10.0.19041.0" "windows"
            }
        }
    }
}

Write-Host "?? Build process completed successfully!" -ForegroundColor Green

if ($Configuration -eq "Release" -and (Test-Path "./artifacts")) {
    Write-Host ""
    Write-Host "?? Published artifacts available in:" -ForegroundColor Cyan
    Get-ChildItem -Path "./artifacts" -Recurse -Include "*.apk", "*.exe", "*.app" | Select-Object -First 10 | ForEach-Object {
        Write-Host "  $($_.FullName)" -ForegroundColor Gray
    }
}