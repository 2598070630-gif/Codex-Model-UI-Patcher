$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$OutDir = Join-Path $Root 'dist'
$Source = Join-Path $Root 'CodexModelUIPatcher.cs'
$Manifest = Join-Path $Root 'CodexModelUIPatcher.exe.manifest'
$Output = Join-Path $OutDir 'CodexModelUIPatcher.exe'

if (-not (Test-Path -LiteralPath $Csc)) {
  throw "C# compiler not found: $Csc"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

& $Csc `
  /nologo `
  /target:exe `
  /platform:anycpu `
  /win32manifest:$Manifest `
  /out:$Output `
  $Source

Write-Host "Built: $Output"
