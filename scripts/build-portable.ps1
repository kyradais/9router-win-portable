# 9Router Portable Build Script for Windows
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
Set-Location $rootDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Building 9Router Portable Distribution  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$distDir = Join-Path $rootDir "dist"
$portableDir = Join-Path $distDir "9RouterPortable"
$appRouterDir = Join-Path $portableDir "app\9router"
$dataDir = Join-Path $portableDir "data"
$runtimeDir = Join-Path $portableDir "runtime"
$nodeDir = Join-Path $runtimeDir "node"

# 1. Clean build directory
if (Test-Path $distDir) {
    Write-Host "[1/6] Cleaning existing dist directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $distDir
}

New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
New-Item -ItemType Directory -Force -Path $appRouterDir | Out-Null
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
New-Item -ItemType Directory -Force -Path $nodeDir | Out-Null

# 2. Build upstream Next.js app
Write-Host "[2/6] Building upstream Next.js 9Router application..." -ForegroundColor Yellow
Set-Location (Join-Path $rootDir "upstream-9router")

if (!(Test-Path "node_modules")) {
    Write-Host "Running npm install..." -ForegroundColor Gray
    npm install
}

npm run build

# 3. Copy standalone output & sql.js dependencies
Write-Host "[3/6] Copying 9Router application assets..." -ForegroundColor Yellow
$standaloneDir = Join-Path $rootDir "upstream-9router\.next\standalone"
if (!(Test-Path $standaloneDir)) {
    throw "Standalone output directory not found at $standaloneDir. Ensure Next.js output mode is 'standalone'."
}

Copy-Item -Recurse -Force "$standaloneDir\*" $appRouterDir

# Ensure sql-wasm.wasm is present in app directory for sql.js driver fallback
$wasmSource = Join-Path $rootDir "upstream-9router\node_modules\sql.js\dist\sql-wasm.wasm"
if (Test-Path $wasmSource) {
    Copy-Item -Force $wasmSource $appRouterDir
    Copy-Item -Force $wasmSource (Join-Path $appRouterDir "node_modules\sql.js\dist\") -ErrorAction SilentlyContinue
}

# 4. Download and extract Node.js Portable Runtime
Write-Host "[4/6] Setting up Node.js v20 runtime..." -ForegroundColor Yellow
$nodeZipPath = Join-Path $env:TEMP "node-v20.12.0-win-x64.zip"
$nodeUrl = "https://nodejs.org/dist/v20.12.0/node-v20.12.0-win-x64.zip"

if (!(Test-Path (Join-Path $nodeDir "node.exe"))) {
    if (!(Test-Path $nodeZipPath)) {
        Write-Host "Downloading Node.js v20.12.0 x64 runtime..." -ForegroundColor Gray
        Invoke-WebRequest -Uri $nodeUrl -OutFile $nodeZipPath
    }

    Write-Host "Extracting Node.js runtime..." -ForegroundColor Gray
    $extractTemp = Join-Path $env:TEMP "node_extract_temp"
    if (Test-Path $extractTemp) { Remove-Item -Recurse -Force $extractTemp }
    Expand-Archive -Path $nodeZipPath -DestinationPath $extractTemp

    $extractedNodeDir = Join-Path $extractTemp "node-v20.12.0-win-x64"
    Copy-Item -Recurse -Force "$extractedNodeDir\*" $nodeDir
    Remove-Item -Recurse -Force $extractTemp
}

# 5. Compile C# WinForms Launcher
Write-Host "[5/6] Compiling 9Router Portable WinForms Launcher..." -ForegroundColor Yellow
$launcherProj = Join-Path $rootDir "src\9RouterPortable\9RouterPortable.csproj"

dotnet publish $launcherProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $portableDir

# 6. Verify Portable Package
Write-Host "[6/6] Verifying Portable Package Structure..." -ForegroundColor Yellow
$requiredFiles = @(
    (Join-Path $portableDir "9RouterPortable.exe"),
    (Join-Path $nodeDir "node.exe"),
    (Join-Path $appRouterDir "server.js")
)

foreach ($file in $requiredFiles) {
    if (!(Test-Path $file)) {
        throw "Verification failed: Required output file missing: $file"
    }
}

Set-Location $rootDir
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " BUILD SUCCESSFUL! Portable app ready at: " -ForegroundColor Green
Write-Host " $portableDir" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
