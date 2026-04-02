param(
    [string]$ExtensionDir = "D:\Projects\babaSHELL\vscode\babashell"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $ExtensionDir)) {
    throw "Extension directory not found: $ExtensionDir"
}

Push-Location $ExtensionDir
try {
    Write-Host "npm install..."
    & npm install | Out-Host

    Write-Host "compile..."
    & npm run compile | Out-Host

    Write-Host "vsce package..."
    & npx @vscode/vsce package | Out-Host

    $vsix = Get-ChildItem -Filter *.vsix | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$vsix) { throw "VSIX not found." }

    Write-Host "VS Code install..."
    & code --install-extension $vsix.FullName | Out-Host

    Write-Host "Ready: $($vsix.FullName)"
}
finally {
    Pop-Location
}
