$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputPath = Join-Path $projectRoot "publish\ModFrame"

if (Test-Path -LiteralPath $outputPath)
{
    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot)
    $resolvedOutputPath = [System.IO.Path]::GetFullPath($outputPath)
    if (-not $resolvedOutputPath.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "The release output path is outside the project directory."
    }

    Remove-Item -LiteralPath $resolvedOutputPath -Recurse -Force
}

dotnet publish "$projectRoot\ModFrame.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath

Write-Host "ModFrame was published to $outputPath"
