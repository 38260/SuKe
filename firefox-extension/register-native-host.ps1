param(
    [Parameter(Mandatory = $true)]
    [string]$SuKePath
)

$resolvedExecutable = (Resolve-Path -LiteralPath $SuKePath).Path
$hostDirectory = Join-Path $env:LOCALAPPDATA 'SuKe'
New-Item -ItemType Directory -Path $hostDirectory -Force | Out-Null
$manifestPath = Join-Path $hostDirectory 'native-host-manifest-firefox.json'
$manifest = [ordered]@{
    name = 'com.suke.browser'
    description = 'SuKe Firefox session native messaging host'
    path = $resolvedExecutable
    type = 'stdio'
    allowed_extensions = @('suke-browser-connector@suke.app')
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

$location = 'HKCU:\Software\Mozilla\NativeMessagingHosts\com.suke.browser'
New-Item -Path $location -Force | Out-Null
Set-ItemProperty -Path $location -Name '(default)' -Value $manifestPath

Write-Output "Registered the SuKe native host for Firefox at $manifestPath."
