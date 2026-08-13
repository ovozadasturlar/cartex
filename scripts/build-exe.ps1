[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [switch]$Compress
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src\desktop\Cartex.Desktop\Cartex.Desktop.csproj'
$outDir = Join-Path $repoRoot "artifacts\desktop\$Runtime"

$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-o', $outDir
)

# Kichikroq fayl, lekin har ishga tushganda ochilishi sekinroq. Kassa uchun tavsiya etilmaydi.
if ($Compress) { $arguments += '-p:EnableCompressionInSingleFile=true' }

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'EXE yig''ish xato bilan tugadi.' }

$exe = Join-Path $outDir 'Cartex.Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Cartex.Desktop.exe topilmadi.' }

$size = (Get-Item -LiteralPath $exe).Length / 1MB
Write-Host ("Tayyor: {0} ({1:N0} MB)" -f $exe, $size)
