# GitHub Actions Workflow Configuration
# This file documents the CI/CD setup for AnonWallClient

## Workflows Overview

### 1. release.yml - Build and Release
**Trigger**: Git tags (v*.*.*) or manual dispatch
**Purpose**: Build for all platforms and create GitHub releases
**Platforms**: Android, Windows, iOS, macOS
**Artifacts**: APK, ZIP files for each platform
**Improvements**: 
- Added `--skip-sign-check` for MAUI workload installation
- Better error handling and artifact verification
- Self-contained Windows builds
- Conditional release creation (succeeds if any platform builds)

### 2. ci.yml - Continuous Integration  
**Trigger**: Push/PR to main/develop branches
**Purpose**: Test builds and code quality checks
**Features**: Multi-platform builds, code formatting, security scans
**Improvements**:
- Fixed Ubuntu runner issues by using Windows for MAUI builds
- Improved code formatting checks (informational only)
- Better security vulnerability scanning
- Added dependency analysis job

### 3. dependencies.yml - Dependency Management
**Trigger**: Weekly schedule or manual dispatch
**Purpose**: Automatically update NuGet packages
**Features**: Security updates, compatibility checks, automated PRs
**Improvements**:
- Better change detection and verification
- Enhanced PR descriptions with verification steps
- Conditional package updates based on project content

## Release Process

### Manual Release
1. Go to Actions ? "Build and Release AnonWallClient"
2. Click "Run workflow"
3. Enter version number (e.g., 1.0.1)
4. Click "Run workflow"

### Tag-based Release
1. Create and push a version tag:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```
2. Release workflow runs automatically

### Pre-release Versions
Versions containing 'alpha', 'beta', or 'rc' are marked as pre-releases.

## Platform Requirements

### Android
- Java 11+
- Android SDK (handled by GitHub Actions)
- Produces: APK file

### Windows  
- Windows 10/11 SDK (handled by GitHub Actions)
- Produces: Self-contained ZIP

### iOS
- Xcode (macOS runner)
- Produces: Build artifacts (requires signing for distribution)

### macOS
- Xcode (macOS runner) 
- Produces: .app bundle in ZIP

## Configuration Files

### .editorconfig
- Establishes consistent code formatting rules
- Supports C#, XML, JSON, YAML formatting
- Integrates with `dotnet format` command

## Workflow Improvements Made

### Reliability Enhancements
- Added `--skip-sign-check` flag to prevent signing issues
- Better error handling with `continue-on-error` where appropriate
- Conditional job execution based on previous results
- Enhanced artifact verification

### Build Quality
- Moved from Ubuntu to Windows runners for MAUI compatibility
- Self-contained Windows deployments
- Improved security scanning with proper exit code handling
- Added dependency analysis and outdated package detection

### Development Experience
- Informational code formatting (doesn't fail builds)
- Detailed release notes with build status
- Better PR descriptions for dependency updates
- Enhanced logging and debugging output

## Secrets Configuration

No secrets required for basic building. For signed releases, add:
- `ANDROID_KEYSTORE`: Base64 encoded keystore
- `ANDROID_KEYSTORE_PASSWORD`: Keystore password
- `IOS_CERTIFICATE`: Base64 encoded certificate
- `IOS_PROVISIONING_PROFILE`: Base64 encoded profile

## Troubleshooting

### Build Failures
- ? Check .NET version compatibility (using 8.0.x)
- ? Verify MAUI workload installation (using --skip-sign-check)
- ? Review platform-specific requirements
- ? Check for missing dependencies or package conflicts

### CI/CD Issues
- ? Ubuntu builds: Moved MAUI builds to Windows runners
- ? Code formatting: Made informational to prevent CI failures
- ? Security scans: Improved error handling for clean projects
- ? Artifact uploads: Added proper error checking

### Missing Artifacts
- ? Enhanced artifact verification and listing
- ? Better error messages for missing files
- ? Conditional release creation (partial success handling)

### Release Creation Issues
- ? Verify repository permissions for GitHub token
- ? Check tag format (must start with 'v')
- ? Release proceeds if any platform builds successfully
- ? Enhanced release notes with build status

### Dependency Updates
- ? Improved change detection
- ? Build verification before PR creation
- ? Better error handling for update failures
- ? Enhanced PR descriptions with verification steps

## Best Practices Implemented

1. **Fail Fast**: Quick feedback on build issues
2. **Graceful Degradation**: Partial success handling for releases
3. **Clear Communication**: Detailed logging and status reporting
4. **Automation**: Minimal manual intervention required
5. **Consistency**: Standardized formatting and coding rules
6. **Security**: Regular vulnerability scanning and updates