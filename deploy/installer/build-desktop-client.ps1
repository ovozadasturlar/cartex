[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$installerDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $installerDir '..\..')
$project = Join-Path $repoRoot 'src\desktop\Cartex.Desktop\Cartex.Desktop.csproj'
$publishDir = Join-Path $installerDir 'publish\desktop-client'
$installerScript = Join-Path $installerDir 'cartex-desktop.iss'

& dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Cartex Desktop publish xato bilan tugadi.' }

$isccPath = "C:\Program Files (x86)\Inno Setup\ISCC.exe"
$isccPath = if ($null -ne $isccCommand)
{
    $isccCommand.Source
}
else
{
    @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup\ISCC.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($isccPath))
{
    $isccPath = @(
        'C:\Program Files\Inno Setup 7\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:USERPROFILE 'AppData\Local\Programs\Inno Setup 7\ISCC.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($isccPath))
{
    throw 'Inno Setup 7 topilmadi. Uni o''rnating, so''ng ushbu skriptni qayta ishga tushiring.'
}

& $isccPath $installerScript
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup installer yaratishda xato yuz berdi.' }

Write-Host "Tayyor: $(Join-Path $installerDir 'output\cartex-desktop-setup-0.0.1.exe')"
