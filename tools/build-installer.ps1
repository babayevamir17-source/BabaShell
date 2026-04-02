param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$IsccPath = "",
    [switch]$SkipVsCode,
    [switch]$SkipVisualStudio
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot "dist"
$cliDir = Join-Path $dist "cli"
$vscodeDir = Join-Path $dist "vscode"
$vsDir = Join-Path $dist "vs"

New-Item -ItemType Directory -Path $dist -Force | Out-Null
New-Item -ItemType Directory -Path $cliDir -Force | Out-Null
New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
New-Item -ItemType Directory -Path $vsDir -Force | Out-Null

Write-Host "Publishing BabaShell CLI..."
$project = Join-Path $repoRoot "babaSHELL\babaSHELL.csproj"
& dotnet publish $project -c $Configuration -r $Runtime -p:PublishSingleFile=true -p:SelfContained=true -o $cliDir | Out-Host

if (-not $SkipVsCode) {
    Write-Host "Packaging VS Code extension..."
    $extensionDir = Join-Path $repoRoot "vscode\babashell"
    Push-Location $extensionDir
    try {
        & npm install | Out-Host
        & npm run compile | Out-Host
        & npx @vscode/vsce package | Out-Host
        $vsix = Get-ChildItem -Filter *.vsix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($vsix) {
            Copy-Item $vsix.FullName (Join-Path $vscodeDir "BabaShell.vsix") -Force
        } else {
            Write-Warning "VS Code VSIX not found."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipVisualStudio) {
    Write-Host "Building Visual Studio VSIX..."
    $vsixProject = Join-Path $repoRoot "BabaShell.Vsix\BabaShell.Vsix.csproj"
    & dotnet build $vsixProject -c $Configuration | Out-Host
    $vsixOut = Get-ChildItem -Recurse -Filter *.vsix (Join-Path $repoRoot "BabaShell.Vsix") | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($vsixOut) {
        Copy-Item $vsixOut.FullName (Join-Path $vsDir "BabaShell.Vsix.vsix") -Force
    } else {
        Write-Warning "Visual Studio VSIX not found."
    }
}

$iss = Join-Path $repoRoot "tools\installer.iss"
if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $IsccPath -or !(Test-Path $IsccPath)) {
    throw "ISCC.exe not found. Install Inno Setup 6 or pass -IsccPath."
}

$pkgJson = Join-Path $repoRoot "vscode\babashell\package.json"
$version = "0.1.0"
if (Test-Path $pkgJson) {
    $version = (Get-Content $pkgJson | ConvertFrom-Json).version
}

Write-Host "Building installer..."
& $IsccPath "/DAppVersion=$version" $iss | Out-Host
Write-Host "Installer ready under dist\\BabaShell-Setup-$version.exe"
