$ErrorActionPreference = 'Stop'
$deliveryDirectory = Join-Path $PSScriptRoot 'deliverables'
New-Item -ItemType Directory -Force -Path $deliveryDirectory | Out-Null
$binary = Join-Path $PSScriptRoot 'dist\Keyside.exe'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build first: .\build.ps1' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\PORTABLE-README.md') -Destination (Join-Path $PSScriptRoot 'dist\README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\USAGE.zh-CN.md') -Destination (Join-Path $PSScriptRoot 'dist\USAGE.zh-CN.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination (Join-Path $PSScriptRoot 'dist') -Force
$licenseDirectory = Join-Path $PSScriptRoot 'dist\licenses'
New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $PSScriptRoot 'dist') -Force
Copy-Item -LiteralPath @((Join-Path $PSScriptRoot 'assets\Twemoji-LICENSE-GRAPHICS.txt'), (Join-Path $PSScriptRoot 'assets\Twemoji-LICENSE.txt'), (Join-Path $PSScriptRoot 'assets\Unicode-LICENSE.txt')) -Destination $licenseDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'examples\Template.txt') -Destination (Join-Path $PSScriptRoot 'dist\Template.txt') -Force
$portableZip = Join-Path $deliveryDirectory 'Keyside-v0.6-win-x64.zip'
Compress-Archive -LiteralPath @($binary, (Join-Path $PSScriptRoot 'dist\Keyside.exe.config'), (Join-Path $PSScriptRoot 'dist\README.md'), (Join-Path $PSScriptRoot 'dist\USAGE.zh-CN.md'), (Join-Path $PSScriptRoot 'dist\LICENSE'), (Join-Path $PSScriptRoot 'dist\CHANGELOG.md'), (Join-Path $PSScriptRoot 'dist\THIRD-PARTY-NOTICES.md'), (Join-Path $PSScriptRoot 'dist\Template.txt'), $licenseDirectory) -DestinationPath $portableZip -Force
$sourceNames = @('src','assets','tools','docs','examples','LICENSE','CHANGELOG.md','THIRD-PARTY-NOTICES.md','README.md','README.en.md','build.ps1','package.ps1','Build.cmd','Start.cmd','Keyside.exe.config','app.manifest','ShortcutDock.csproj','.gitignore')
$sourcePaths = @($sourceNames | ForEach-Object { Join-Path $PSScriptRoot $_ })
$sourceZip = Join-Path $deliveryDirectory 'Keyside-v0.6-source.zip'
Compress-Archive -LiteralPath $sourcePaths -DestinationPath $sourceZip -Force
Get-Item -LiteralPath $portableZip,$sourceZip | Select-Object Name,Length
