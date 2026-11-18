# PVC Studio Pro — Local Windows Installation Script
$ErrorActionPreference = "Stop"

$appName = "PVC Studio Pro"
$sourceDir = "d:\pvc card pro\publish\PVCStudioPro_win-x64"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\PVC Studio Pro"
$exePath = Join-Path $installDir "PVCStudioPro.App.exe"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Installing $appName on this Laptop" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Ensure source exists
if (-not (Test-Path (Join-Path $sourceDir "PVCStudioPro.App.exe"))) {
    throw "Published application not found at: $sourceDir"
}

# 2. Create Target Directory
Write-Host "[1/5] Creating application directory: $installDir" -ForegroundColor Yellow
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copy Application Files
Write-Host "[2/5] Copying binaries and dependencies..." -ForegroundColor Yellow
robocopy $sourceDir $installDir /E /IS /IT /NP /NDL /NFL | Out-Null

if (-not (Test-Path $exePath)) {
    throw "Installation failed: $exePath not found after copy."
}
Write-Host "      Binaries deployed successfully." -ForegroundColor Green

# 4. Create Desktop & Start Menu Shortcuts via WScript.Shell
Write-Host "[3/5] Creating Windows Desktop and Start Menu shortcuts..." -ForegroundColor Yellow
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
Write-Host "[4/5] Generating uninstaller..." -ForegroundColor Yellow
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
Write-Host "[5/5] Registering in Windows Installed Apps registry..." -ForegroundColor Yellow
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PVCStudioPro"
if (-not (Test-Path $regKey)) {
    New-Item -Path $regKey -Force | Out-Null
}

$installSizeKB = [int]((Get-ChildItem $installDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1KB)

Set-ItemProperty -Path $regKey -Name "DisplayName" -Value "PVC Studio Pro"
Set-ItemProperty -Path $regKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $regKey -Name "Publisher" -Value "PVC Studio Pro Team"
Set-ItemProperty -Path $regKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $regKey -Name "DisplayIcon" -Value "$exePath,0"
Set-ItemProperty -Path $regKey -Name "UninstallString" -Value "`"$uninstallerPath`""
Set-ItemProperty -Path $regKey -Name "EstimatedSize" -Value $installSizeKB
Set-ItemProperty -Path $regKey -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $regKey -Name "NoRepair" -Value 1 -Type DWord

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Installation Complete!" -ForegroundColor Green
Write-Host " Application Location: $exePath" -ForegroundColor White
Write-Host " Installed Size: $([math]::Round($installSizeKB / 1024, 2)) MB" -ForegroundColor White
Write-Host " Desktop Shortcut: $desktopLnkPath" -ForegroundColor White
Write-Host " Start Menu Shortcut: $startMenuLnkPath" -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Cyan
