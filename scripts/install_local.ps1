# PVC Studio Pro — Local Windows Installation & Distribution Packager
param(
    [switch]$Rebuild
)
$ErrorActionPreference = "Stop"

$appName = "PVC Studio Pro"
$projectRoot = "d:\pvc card pro"
$sourceDir = Join-Path $projectRoot "publish\PVCStudioPro_win-x64"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\PVC Studio Pro"
$exePath = Join-Path $installDir "PVCStudioPro.App.exe"
$distZip = Join-Path $projectRoot "publish\PVCStudioPro_Portable_win-x64.zip"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Installing $appName (100% Self-Contained) on this Laptop" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 0. Publish self-contained if requested or missing
if ($Rebuild -or (-not (Test-Path (Join-Path $sourceDir "coreclr.dll")))) {
    Write-Host "[0/6] Publishing self-contained release package (Zero .NET prerequisites required)..." -ForegroundColor Yellow
    $dotnetExe = if (Test-Path "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe") {
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
    } else {
        "dotnet"
    }
    & $dotnetExe publish "$projectRoot\src\PVCStudioPro.App\PVCStudioPro.App.csproj" -c Release -r win-x64 --self-contained true -o $sourceDir
    if ($LASTEXITCODE -ne 0) {
        throw "Self-contained publish failed with code $LASTEXITCODE"
    }
}

# 1. Ensure source exists and kill any running instance
if (-not (Test-Path (Join-Path $sourceDir "PVCStudioPro.App.exe"))) {
    throw "Published application not found at: $sourceDir"
}
Get-Process -Name "PVCStudioPro.App" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 2. Create Target Directory
Write-Host "[1/6] Creating application directory: $installDir" -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copy Application Files
Write-Host "[2/6] Copying binaries and dependencies..." -ForegroundColor Yellow
robocopy $sourceDir $installDir /E /IS /IT /NP /NDL /NFL /R:2 /W:1 | Out-Null
if ($LASTEXITCODE -gt 7) {
    throw "Robocopy failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $exePath)) {
    throw "Installation failed: $exePath not found after copy."
}
Write-Host "      Binaries deployed successfully." -ForegroundColor Green

# 4. Create Desktop & Start Menu Shortcuts via WScript.Shell
Write-Host "[3/6] Creating Windows Desktop and Start Menu shortcuts..." -ForegroundColor Yellow
$wsh = New-Object -ComObject WScript.Shell

# Desktop Shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnkPath = Join-Path $desktopPath "$appName.lnk"
$desktopShortcut = $wsh.CreateShortcut($desktopLnkPath)
$desktopShortcut.TargetPath = $exePath
$desktopShortcut.WorkingDirectory = $installDir
$desktopShortcut.Description = "Professional PVC Card Production Suite"
$desktopShortcut.IconLocation = "$exePath,0"
$desktopShortcut.Save()
Write-Host "      Created Desktop shortcut: $desktopLnkPath" -ForegroundColor Green

# Start Menu Shortcut
$startMenuPrograms = [Environment]::GetFolderPath("Programs")
$startMenuLnkPath = Join-Path $startMenuPrograms "$appName.lnk"
$startShortcut = $wsh.CreateShortcut($startMenuLnkPath)
$startShortcut.TargetPath = $exePath
$startShortcut.WorkingDirectory = $installDir
$startShortcut.Description = "Professional PVC Card Production Suite"
$startShortcut.IconLocation = "$exePath,0"
$startShortcut.Save()
Write-Host "      Created Start Menu shortcut: $startMenuLnkPath" -ForegroundColor Green

# 5. Create Clean Uninstaller Script
Write-Host "[4/6] Generating uninstaller..." -ForegroundColor Yellow
$uninstallerPath = Join-Path $installDir "uninstall.cmd"
$uninstallCmdContent = @"
@echo off
echo Uninstalling PVC Studio Pro...
del /f /q "$desktopLnkPath" 2>nul
del /f /q "$startMenuLnkPath" 2>nul
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\PVCStudioPro" /f 2>nul
echo Cleaning up files...
cd /d "%TEMP%"
rmdir /s /q "$installDir" 2>nul
echo PVC Studio Pro has been completely uninstalled.
pause
"@
[System.IO.File]::WriteAllText($uninstallerPath, $uninstallCmdContent)

# 6. Register in Windows Settings / Installed Apps (Registry)
Write-Host "[5/6] Registering in Windows Installed Apps registry..." -ForegroundColor Yellow
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PVCStudioPro"
if (-not (Test-Path $regKey)) {
    New-Item -Path $regKey -Force | Out-Null
}

$installSizeKB = [int]((Get-ChildItem $installDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1KB)

Set-ItemProperty -Path $regKey -Name "DisplayName" -Value "PVC Studio Pro"
Set-ItemProperty -Path $regKey -Name "DisplayVersion" -Value "2.0.0-RC"
Set-ItemProperty -Path $regKey -Name "Publisher" -Value "PVC Studio Pro Team"
Set-ItemProperty -Path $regKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $regKey -Name "DisplayIcon" -Value "$exePath,0"
Set-ItemProperty -Path $regKey -Name "UninstallString" -Value "`"$uninstallerPath`""
Set-ItemProperty -Path $regKey -Name "EstimatedSize" -Value $installSizeKB
Set-ItemProperty -Path $regKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $regKey -Name "NoRepair" -Value 1 -Type DWord

# 7. Package Portable Transferable Distribution ZIP
Write-Host "[6/6] Generating portable distribution package: $distZip" -ForegroundColor Yellow
if (Test-Path $distZip) {
    Remove-Item $distZip -Force
}
Compress-Archive -Path "$sourceDir\*" -DestinationPath $distZip -CompressionLevel Optimal
Write-Host "      Portable package created successfully: $([math]::Round((Get-Item $distZip).Length / 1MB, 2)) MB" -ForegroundColor Green

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Installation & Packaging Complete!" -ForegroundColor Green
Write-Host " Application Location: $exePath" -ForegroundColor White
Write-Host " Installed Size: $([math]::Round($installSizeKB / 1024, 2)) MB" -ForegroundColor White
Write-Host " Desktop Shortcut: $desktopLnkPath" -ForegroundColor White
Write-Host " Start Menu Shortcut: $startMenuLnkPath" -ForegroundColor White
Write-Host " Portable Archive: $distZip" -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Cyan
