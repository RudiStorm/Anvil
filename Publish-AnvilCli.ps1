[CmdletBinding()]
param(
    [string]$OutputDirectory = 'D:\dev-tools-path',
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\Anvil.Cli\Anvil.Cli.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "CLI project was not found: $project"
}

$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$parent = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

& dotnet publish $project `
    '--configuration' $Configuration `
    '--runtime' $Runtime `
    '--self-contained' 'true' `
    '-p:PublishSingleFile=true' `
    '-p:IncludeNativeLibrariesForSelfExtract=true' `
    '-p:PublishTrimmed=false' `
    '--output' $output

if ($LASTEXITCODE -ne 0) {
    throw "Anvil CLI publish failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $output 'Anvil.Cli.exe'
$targetExecutable = Join-Path $output 'anvil.exe'
if (-not (Test-Path -LiteralPath $publishedExecutable)) {
    throw "The published CLI executable was not found: $publishedExecutable"
}

if (Test-Path -LiteralPath $targetExecutable) {
    Remove-Item -LiteralPath $targetExecutable -Force
}

Move-Item -LiteralPath $publishedExecutable -Destination $targetExecutable

Write-Output "Published Anvil CLI to $output"
