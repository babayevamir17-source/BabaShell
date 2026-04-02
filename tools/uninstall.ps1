param(
    [string]$InstallDir = "C:\Program Files\BabaShell"
)

$ErrorActionPreference = "Stop"

$path = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($path -like "*$InstallDir*") {
    $newPath = ($path -split ";") | Where-Object { $_ -and ($_ -ne $InstallDir) }
    [Environment]::SetEnvironmentVariable("Path", ($newPath -join ";"), "Machine")
}

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

$extKey = "Registry::HKEY_CLASSES_ROOT\.babashell"
$progKey = "Registry::HKEY_CLASSES_ROOT\BabaShellFile"

if (Test-Path $extKey) { Remove-Item -Path $extKey -Recurse -Force }
if (Test-Path $progKey) { Remove-Item -Path $progKey -Recurse -Force }

Write-Host "BabaShell silindi."
