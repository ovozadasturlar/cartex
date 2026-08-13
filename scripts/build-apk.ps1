[CmdletBinding()]
param(
    [ValidateSet('store', 'agent')] [string]$App = 'store',
    [switch]$Arm64Only,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$name = if ($App -eq 'store') { 'Store' } else { 'Agent' }
$project = Join-Path $repoRoot "src\mobile\Cartex.Mobile.$name\Cartex.Mobile.$name.csproj"

$sdk = @(
    $env:ANDROID_HOME,
    $env:ANDROID_SDK_ROOT,
    'C:\Program Files (x86)\Android\android-sdk',
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not $sdk) { throw 'Android SDK topilmadi. ANDROID_HOME ni belgilang.' }

$arguments = @(
    'build', $project,
    '-c', 'Release',
    '-f', 'net10.0-android',
    "-p:AndroidSdkDirectory=$sdk",
    '-p:RuntimeIdentifier=android-arm64'
)

# Faqat arm64: APK ancha kichik va build tezroq, lekin eski 32-bitli telefonlarga o'rnatilmaydi.
if ($Arm64Only) { $arguments += '-p:RuntimeIdentifiers=android-arm64' }

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'APK yig''ish xato bilan tugadi.' }

$apk = Get-ChildItem -Path (Join-Path $repoRoot "src\mobile\Cartex.Mobile.$name\bin\Release\net10.0-android\android-arm64") `
    -Filter '*-Signed.apk' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $apk) { throw 'Signed APK topilmadi.' }
Write-Host ("Tayyor: {0} ({1:N0} MB)" -f $apk.FullName, ($apk.Length / 1MB))

if ($Install)
{
    $adb = Join-Path $sdk 'platform-tools\adb.exe'
    if (-not (Test-Path -LiteralPath $adb)) { throw 'adb topilmadi.' }
    & $adb install -r $apk.FullName
    if ($LASTEXITCODE -ne 0) { throw 'Telefonga o''rnatish xato bilan tugadi.' }
}
