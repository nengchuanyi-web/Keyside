param([switch]$Verify)
$ErrorActionPreference = 'Stop'
# Windows PowerShell loads the installed .NET Framework WPF assemblies; no NuGet or SDK required.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $forwardArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    if ($Verify) { $forwardArgs += '-Verify' }
    & "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" @forwardArgs
    exit $LASTEXITCODE
}
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms,System.Drawing,System.Runtime.Serialization,System.Core,System.IO.Compression
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 C# compiler not found.' }
$outputDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$references = @(
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\System.dll'),
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\System.Xml.dll'),
    [System.Windows.Window].Assembly.Location,
    [System.Windows.Interop.HwndSource].Assembly.Location,
    [System.Windows.Threading.DispatcherObject].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location,
    [System.Windows.Forms.NotifyIcon].Assembly.Location,
    [System.Drawing.Bitmap].Assembly.Location,
    [System.Runtime.Serialization.DataContractAttribute].Assembly.Location,
    [System.Linq.Enumerable].Assembly.Location,
    [System.IO.Compression.ZipArchive].Assembly.Location
)
$compilerArgs = @('/nologo', '/noconfig', '/utf8output', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', '/warn:4',
    ('/out:' + (Join-Path $outputDirectory 'Keyside.exe')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/resource:' + (Join-Path $PSScriptRoot 'src\Ui.xaml') + ',ShortcutDock.Ui.xaml'))
foreach ($emojiResource in @('emoji.zip','emoji-map.txt','emoji-test-17.0.txt')) {
    $compilerArgs += '/resource:' + (Join-Path $PSScriptRoot ('assets\' + $emojiResource)) + ',ShortcutDock.' + $emojiResource
}
$compilerArgs += '/resource:' + (Join-Path $PSScriptRoot 'assets\Xiaohongshu.png') + ',ShortcutDock.Xiaohongshu.png'
$iconPath = Join-Path $PSScriptRoot 'assets\Keyside.ico'
if (Test-Path -LiteralPath $iconPath) { $compilerArgs += '/win32icon:' + $iconPath }
foreach ($reference in $references) { $compilerArgs += '/reference:' + $reference }
$compilerArgs += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Keyside.exe.config') -Destination $outputDirectory -Force
Write-Output ('Built: ' + (Join-Path $outputDirectory 'Keyside.exe'))
if ($Verify) {
    $verificationDirectory = Join-Path $PSScriptRoot 'qa'
    $verifyProcess = Start-Process -FilePath (Join-Path $outputDirectory 'Keyside.exe') -ArgumentList @('--verify', ('"' + $verificationDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($verifyProcess.ExitCode -ne 0) { throw ('Verification failed. Read ' + $verificationDirectory) }
    Get-Content -LiteralPath (Join-Path $verificationDirectory 'verification.txt')
}
