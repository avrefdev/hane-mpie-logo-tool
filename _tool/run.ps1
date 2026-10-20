param([string]$Mode = 'replace', [string]$Location = '')
$ErrorActionPreference = 'Stop'
$toolDir = Split-Path -Parent $PSScriptRoot
try {
    $source = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'LogoTool.cs'))
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing -Language CSharp
} catch {
    Write-Host "ERROR: Could not start the tool: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
exit [LogoTool]::Run($Mode, $toolDir, $Location)
