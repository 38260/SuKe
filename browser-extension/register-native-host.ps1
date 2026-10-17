param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionId,

    [Parameter(Mandatory = $true)]
    [string]$SuKePath,

    [switch]$Chrome,
    [switch]$Edge
)

if (-not $Chrome -and -not $Edge) {
    $Chrome = $true
    $Edge = $true
}

if ($ExtensionId -notmatch '^[a-p]{32}$') {
    throw 'ExtensionId must be the 32-character lowercase browser extension ID.'
}

$hostDirectory = Join-Path $env:LOCALAPPDATA 'SuKe'
New-Item -ItemType Directory -Path $hostDirectory -Force | Out-Null
$manifestPath = Join-Path $hostDirectory 'native-host-manifest.json'
$manifest = [ordered]@{
    name = 'com.suke.browser'
    description = 'SuKe browser-session native messaging host'
    path = (Resolve-Path $SuKePath).Path
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath -Encoding UTF8

$locations = @()
if ($Chrome) { $locations += 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.suke.browser' }
if ($Edge) { $locations += 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.suke.browser' }
foreach ($location in $locations) {
    New-Item -Path $location -Force | Out-Null
    Set-ItemProperty -Path $location -Name '(default)' -Value $manifestPath
}

Write-Output "Registered $manifestPath for $($locations.Count) browser(s)."
