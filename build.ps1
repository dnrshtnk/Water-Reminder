$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Required: .NET Framework 4.x for Windows x64.' }
$outputDirectory = Join-Path $projectDirectory 'dist'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$common = @('/nologo', '/platform:x64', '/optimize+', '/warn:4', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll')
$assetDirectory = Join-Path $projectDirectory 'assets'
& $compiler @common /target:exe ('/out:' + (Join-Path $outputDirectory 'BuildIcon.exe')) (Join-Path $projectDirectory 'tools\BuildIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon generator build failed.' }
& (Join-Path $outputDirectory 'BuildIcon.exe') $assetDirectory
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
$iconPath = Join-Path $assetDirectory 'water.ico'
$iconResource = '/resource:' + $iconPath + ',Water.AppIcon.ico'
& $compiler @common /target:winexe ('/out:' + (Join-Path $outputDirectory 'Water.exe')) ('/win32manifest:' + (Join-Path $projectDirectory 'app.manifest')) ('/win32icon:' + $iconPath) $iconResource @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
& $compiler @common /target:exe /main:Water.Tests ('/out:' + (Join-Path $outputDirectory 'Water.Tests.exe')) $iconResource @sources (Join-Path $projectDirectory 'tests\Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $outputDirectory 'Water.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
Write-Host ('Ready: ' + (Join-Path $outputDirectory 'Water.exe'))
