param(
    [string]$RepoRoot = "",
    [string]$Remote = "origin",
    [string]$Branch = "main",
    [string]$MessagePrefix = "auto sync",
    [int]$DebounceSeconds = 3
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}
$RepoRoot = (Resolve-Path $RepoRoot).Path

$ignoreRegex = '\\\.git\\|\\\.vs\\|\\bin\\|\\obj\\|\\dist\\|\\node_modules\\|\\vscode\\babashell\\out\\'

$script:pending = $false
$script:lastChange = Get-Date
$script:syncing = $false

function Invoke-Sync {
    if ($script:syncing) { return }
    $script:syncing = $true
    try {
        $status = & git -C $RepoRoot status --porcelain
        if ([string]::IsNullOrWhiteSpace($status)) {
            return
        }

        & git -C $RepoRoot add -A | Out-Null
        $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        & git -C $RepoRoot commit -m "$MessagePrefix $ts" | Out-Null
        & git -C $RepoRoot push $Remote $Branch | Out-Null
    }
    finally {
        $script:pending = $false
        $script:syncing = $false
    }
}

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $RepoRoot
$watcher.IncludeSubdirectories = $true
$watcher.EnableRaisingEvents = $true

$action = {
    $path = $Event.SourceEventArgs.FullPath
    if ($path -match $using:ignoreRegex) { return }
    $script:pending = $true
    $script:lastChange = Get-Date
}

Register-ObjectEvent $watcher Changed -Action $action | Out-Null
Register-ObjectEvent $watcher Created -Action $action | Out-Null
Register-ObjectEvent $watcher Deleted -Action $action | Out-Null
Register-ObjectEvent $watcher Renamed -Action $action | Out-Null

Write-Host "Auto-sync started. Watching: $RepoRoot"
Write-Host "Remote: $Remote  Branch: $Branch  Debounce: ${DebounceSeconds}s"

while ($true) {
    Start-Sleep -Milliseconds 500
    if ($script:pending -and ((Get-Date) - $script:lastChange).TotalSeconds -ge $DebounceSeconds) {
        Invoke-Sync
    }
}
