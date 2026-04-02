param(
    [string]$InstallDir = "C:\Program Files\BabaShell",
    [string]$BuildConfig = "Release",
    [ValidateSet("Machine","User")]
    [string]$Scope = "Machine",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "babaSHELL\babaSHELL.csproj"

Write-Host "BabaShell install dir: $InstallDir"
Write-Host "Scope: $Scope"

if (!(Test-Path $project)) {
    throw "babaSHELL.csproj not found: $project"
}

if (-not $SkipBuild) {
    Write-Host "Publishing (single-file)..."
    & dotnet publish $project -c $BuildConfig -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o $InstallDir | Out-Host
}

$exePath = Join-Path $InstallDir "babashell.exe"
if (!(Test-Path $exePath)) {
    throw "babashell.exe not found: $exePath"
}

# PATH
$path = [Environment]::GetEnvironmentVariable("Path", $Scope)
if ($path -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", $path + ";" + $InstallDir, $Scope)
}

# File association (HKCR for Machine, HKCU\Software\Classes for User)
$progId = "BabaShellFile"
if ($Scope -eq "Machine") {
    $extKey = "Registry::HKEY_CLASSES_ROOT\.babashell"
    $progKey = "Registry::HKEY_CLASSES_ROOT\$progId"
} else {
    $extKey = "Registry::HKEY_CURRENT_USER\Software\Classes\.babashell"
    $progKey = "Registry::HKEY_CURRENT_USER\Software\Classes\$progId"
}

New-Item -Path $extKey -Force | Out-Null
Set-ItemProperty -Path $extKey -Name "(Default)" -Value $progId

New-Item -Path $progKey -Force | Out-Null
Set-ItemProperty -Path $progKey -Name "(Default)" -Value "BabaShell Script"

$iconKey = Join-Path $progKey "DefaultIcon"
New-Item -Path $iconKey -Force | Out-Null
Set-ItemProperty -Path $iconKey -Name "(Default)" -Value "`"$InstallDir\babashell.exe`",0"

$cmdKey = Join-Path $progKey "shell\open\command"
New-Item -Path $cmdKey -Force | Out-Null
Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value "`"$InstallDir\babashell.exe`" `"%1`""

Write-Host "Install complete. Open a new terminal and run 'babashell'."
