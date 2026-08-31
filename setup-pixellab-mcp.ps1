# Register the PixelLab MCP server in ZCode's USER-level config (per machine, key never enters git).
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File setup-pixellab-mcp.ps1 -ApiKey <your_pixellab_api_key>
param(
    [Parameter(Mandatory = $true)]
    [string]$ApiKey
)

$ErrorActionPreference = 'Stop'
$cfgPath = Join-Path $env:USERPROFILE '.zcode\cli\config.json'
$dir = Split-Path $cfgPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

if (Test-Path $cfgPath) {
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
} else {
    $cfg = New-Object PSObject
}

if (-not $cfg.PSObject.Properties['mcp']) {
    $cfg | Add-Member NoteProperty mcp (New-Object PSObject)
}
$mcp = $cfg.PSObject.Properties['mcp'].Value
if (-not $mcp.PSObject.Properties['servers']) {
    $mcp | Add-Member NoteProperty servers (New-Object PSObject)
}
$servers = $mcp.PSObject.Properties['servers'].Value

$server = New-Object PSObject
$server | Add-Member NoteProperty type 'http'
$server | Add-Member NoteProperty url 'https://api.pixellab.ai/mcp'
$server | Add-Member NoteProperty headers (New-Object PSObject -Property @{ Authorization = "Bearer $ApiKey" })

if ($servers.PSObject.Properties['pixellab']) {
    $servers.PSObject.Properties['pixellab'].Value = $server
} else {
    $servers | Add-Member NoteProperty pixellab $server
}

$json = $cfg | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($cfgPath, $json)
Write-Host "[OK] PixelLab MCP registered in: $cfgPath"
Write-Host "     Restart ZCode (new session), then verify in Settings -> MCP."
