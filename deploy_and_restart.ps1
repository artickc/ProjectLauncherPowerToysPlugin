$ErrorActionPreference = "Continue"

try {
    $projectName = "ProjectLauncher"
    
    # Find the solution file or project file to build
    $slnFile = Get-Item "*.sln" -ErrorAction SilentlyContinue | Select-Object -First 1
    $projFile = Get-Item "Community.PowerToys.Run.Plugin.ProjectLauncher.csproj" -ErrorAction SilentlyContinue
    
    if (-not $slnFile -and -not $projFile) {
        Write-Host "ERROR: Could not find solution or project file." -ForegroundColor Red
        Write-Host "Current: $(Get-Location)" -ForegroundColor Yellow
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    $buildTarget = if ($slnFile) { $slnFile.Name } else { $projFile.Name }
    Write-Host "Build target: $buildTarget" -ForegroundColor Yellow

Write-Host "Building plugin..." -ForegroundColor Cyan

# Kill any processes that might lock files
@("dotnet", "msbuild", "vbcscompiler", "conhost") | ForEach-Object {
    Get-Process $_ -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

    Write-Host "Cleaning..." -ForegroundColor Yellow
    try {
        dotnet clean -c Release --nologo 2>&1 | Out-Null
    } catch {
        Write-Host "Warning: Clean failed: $_" -ForegroundColor Yellow
    }
    
    Write-Host "Building..." -ForegroundColor Cyan
    & dotnet build $buildTarget -c Release --nologo 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED!" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }

    $buildDir1 = "bin\Release\net9.0-windows10.0.19041.0"
    $buildDir2 = "bin\Release\net9.0-windows"
    
    $buildDir = ""
    if (Test-Path $buildDir1) {
        $buildDir = $buildDir1
    } elseif (Test-Path $buildDir2) {
        $buildDir = $buildDir2
    }
    
    if (-not $buildDir) {
        Write-Host "ERROR: Build directory not found!" -ForegroundColor Red
        Write-Host "Checked: $buildDir1" -ForegroundColor Yellow
        Write-Host "Checked: $buildDir2" -ForegroundColor Yellow
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    $buildDir = Resolve-Path $buildDir
    Write-Host "Build dir found: $buildDir" -ForegroundColor Green
    
    # Deploy
    $localAppData = [Environment]::GetEnvironmentVariable("LOCALAPPDATA")
    $pluginDir = "$localAppData\Microsoft\PowerToys\PowerToys Run\Plugins\$projectName"
    
    Write-Host "Deploying to: $pluginDir" -ForegroundColor Cyan
    # Close PowerToys
    try {
        Get-Process PowerToys -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    } catch { }
    
    # Clear old plugin
    try {
        if (Test-Path $pluginDir) {
            Remove-Item "$pluginDir\*" -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            New-Item -Path $pluginDir -ItemType Directory -Force | Out-Null
        }
    } catch {
        Write-Host "Warning: Failed to clear plugin dir: $_" -ForegroundColor Yellow
    }
    
    # Copy new files
    try {
        Copy-Item "$buildDir\*" -Destination $pluginDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Files deployed successfully." -ForegroundColor Green
    } catch {
        Write-Host "Error copying files: $_" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    # Remove Wox.Plugin.dll
    try {
        if (Test-Path "$pluginDir\Wox.Plugin.dll") {
            Remove-Item "$pluginDir\Wox.Plugin.dll" -Force -ErrorAction SilentlyContinue
        }
    } catch { }
    
    Write-Host "`n=== SUCCESS ===" -ForegroundColor Green
    Write-Host "Deployment complete!" -ForegroundColor Green
    Write-Host "Start PowerToys manually when ready." -ForegroundColor Cyan
    
} catch {
    Write-Host "FATAL ERROR: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host "`nPress Enter to close..." -ForegroundColor Yellow
Read-Host
