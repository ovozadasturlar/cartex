[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [switch]$Compress
)

$ErrorActionPreference = 'Stop'

# MSBuild worker'lari builddan keyin ~15 daqiqa tirik qolib, Cartex.Shared kabi umumiy
# loyihalarning .pdb fayllarini ushlab turadi — keyingi build MSB3026 retry'ga yiqiladi.
$env:MSBUILDDISABLENODEREUSE = '1'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src\desktop\Cartex.Desktop\Cartex.Desktop.csproj'
$stageDir = Join-Path $repoRoot "src\desktop\Cartex.Desktop\bin\Release\publish-$Runtime\"
$outDir = Join-Path $repoRoot "artifacts"

# Symbol fayllari ataylab o'chirilmaydi: Visual Studio ham shu bin\Release papkasiga .pdb yozadi
# va ochiq tursa ularni ushlab turadi. Ularni yo'q qilmoqchi bo'lish MSB3061 ga olib keladi.
$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:PublishReadyToRun=true',
    '-o', $stageDir
)

# Kichikroq fayl, lekin har ishga tushganda ochilishi sekinroq. Kassa uchun tavsiya etilmaydi.
if ($Compress) { $arguments += '-p:EnableCompressionInSingleFile=true' }

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
    if ($LASTEXITCODE -ne 0) { throw 'EXE yig''ish xato bilan tugadi.' }
}
finally { $buildLock.Dispose() }

$built = Join-Path $stageDir 'Cartex.Desktop.exe'
if (-not (Test-Path -LiteralPath $built)) { throw 'Cartex.Desktop.exe topilmadi.' }

New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$exe = Join-Path $outDir 'Cartex.Desktop.exe'
Copy-Item -LiteralPath $built -Destination $exe -Force

$size = (Get-Item -LiteralPath $exe).Length / 1MB
Write-Host ("Tayyor: {0} ({1:N0} MB)" -f $exe, $size)
