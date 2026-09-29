[CmdletBinding()]
param(
    [string]$OutputDirectory = 'D:\dev-tools-path\packages',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$parent = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputDirectory))
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

foreach ($project in @(
    (Join-Path $root 'src\Anvil\Anvil.csproj'),
    (Join-Path $root 'src\Anvil.Razor\Anvil.Razor.csproj'),
    (Join-Path $root 'src\Anvil.Cli\Anvil.Cli.csproj')
)) {
    & dotnet pack $project '--configuration' $Configuration '--no-restore' '--output' $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Package creation failed for $project with exit code $LASTEXITCODE."
    }
}

Write-Output "Published Anvil packages to $([System.IO.Path]::GetFullPath($OutputDirectory))"
