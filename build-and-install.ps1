param(
    [string]$InstallPath = "$env:USERPROFILE\.senf\bin",
    [switch]$AddToPath,
    [switch]$Help
)

function Show-Help {
    Write-Host "SenfCli Build and Install Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\build-and-install.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -InstallPath <path>   Installation directory (default: $env:USERPROFILE\.senf\bin)"
    Write-Host "  -AddToPath            Automatically add installation path to user PATH"
    Write-Host "  -Help                 Show this help message"
}

if ($Help) {
    Show-Help
    exit 0
}

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ScriptRoot "SenfCli.csproj"
$PublishPath = Join-Path $ScriptRoot "bin\publish"

if (-not (Test-Path $ProjectFile)) {
    Write-Host "ERROR: Could not find project file at $ProjectFile" -ForegroundColor Red
    exit 1
}

Write-Host "Building SenfCli..." -ForegroundColor Green

dotnet publish "$ProjectFile" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$PublishPath"
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful" -ForegroundColor Green

if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

Write-Host "Copying files to $InstallPath" -ForegroundColor Cyan
Copy-Item "$PublishPath\*" -Destination $InstallPath -Recurse -Force

$shimPath = Join-Path $InstallPath "senf.cmd"
"@echo off`r`n`"%~dp0SenfCli.exe`" %*`r`n" | Set-Content -Path $shimPath -Encoding ASCII

if ($AddToPath) {
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ([string]::IsNullOrWhiteSpace($currentPath)) {
        $currentPath = $InstallPath
    }
    elseif ($currentPath -notlike "*$InstallPath*") {
        $currentPath = "$currentPath;$InstallPath"
    }

    [Environment]::SetEnvironmentVariable("PATH", $currentPath, "User")
    Write-Host "PATH updated for current user" -ForegroundColor Green
}

Write-Host "Installation complete" -ForegroundColor Green
Write-Host "Installed binary: $(Join-Path $InstallPath 'SenfCli.exe')"
Write-Host "Command shim: $shimPath"
