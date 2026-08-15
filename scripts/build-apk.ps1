[CmdletBinding()]
param(
    [ValidateSet('store', 'agent')] [string]$App = 'store',
    [switch]$Arm64Only,
    [switch]$Install,
    [string]$Device
)

$ErrorActionPreference = 'Stop'

# MSBuild worker'lari builddan keyin ~15 daqiqa tirik qolib, Cartex.Shared kabi umumiy
# loyihalarning .pdb fayllarini ushlab turadi — keyingi build MSB3026 retry'ga yiqiladi.
$env:MSBUILDDISABLENODEREUSE = '1'

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

# Exe va apk buildlari umumiy loyihalarni (Cartex.Shared) bitta bin\Release'ga quradi —
# bir vaqtda ishga tushirilsa fayl talashadi. Qulf: biri ishlayotganda ikkinchisi kutadi.
$lockPath = Join-Path ([IO.Path]::GetTempPath()) 'cartex-release-build.lock'
$buildLock = $null
$waitNoted = $false
while (-not $buildLock) {
    try { $buildLock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [System.IO.IOException] {
        if (-not $waitNoted) { Write-Host 'Boshqa release build ketyapti — tugashini kutyapman...'; $waitNoted = $true }
        Start-Sleep -Seconds 5
    }
}

try {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'APK yig''ish xato bilan tugadi.' }
}
finally { $buildLock.Dispose() }

$apk = Get-ChildItem -Path (Join-Path $repoRoot "src\mobile\Cartex.Mobile.$name\bin\Release\net10.0-android\android-arm64") `
    -Filter '*-Signed.apk' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $apk) { throw 'Signed APK topilmadi.' }

$outDir = Join-Path $repoRoot 'artifacts\mobile'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$published = Join-Path $outDir $apk.Name
Copy-Item -LiteralPath $apk.FullName -Destination $published -Force

Write-Host ("Tayyor: {0} ({1:N0} MB)" -f $published, ($apk.Length / 1MB))

if (-not $Install) { return }

$adb = Join-Path $sdk 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb)) { throw 'adb topilmadi.' }

if (-not $Device)
{
    $attached = @(& $adb devices | Select-Object -Skip 1 |
        Where-Object { $_ -match '\sdevice$' } |
        ForEach-Object { ($_ -split '\s+')[0] })

    if ($attached.Count -eq 0) { throw 'Ulangan qurilma yo''q. USB va "USB debugging" ni tekshiring.' }
    if ($attached.Count -gt 1)
    {
        & $adb devices -l
        throw "Bir nechta qurilma ulangan. Qaysi biriga o'rnatishni ko'rsating: -Device $($attached[0])"
    }
    $Device = $attached[0]
}

Write-Host "O'rnatilmoqda: $Device"
& $adb -s $Device install -r $published
if ($LASTEXITCODE -ne 0) { throw 'Qurilmaga o''rnatish xato bilan tugadi.' }
