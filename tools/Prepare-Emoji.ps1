$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectPath = Split-Path -Parent $PSScriptRoot
$inputZip = Join-Path $projectPath 'work\twemoji-17.0.3.zip'
$testFile = Join-Path $projectPath 'work\emoji-test-17.0.txt'
$source = [IO.Compression.ZipFile]::OpenRead($inputZip)
$outStream = [IO.File]::Create((Join-Path $projectPath 'assets\emoji.zip'))
$archive = [IO.Compression.ZipArchive]::new($outStream, [IO.Compression.ZipArchiveMode]::Create)
$names = @{}
try {
    foreach ($entry in $source.Entries) {
        if ($entry.FullName -match '/72x72/([^/]+)\.png$') {
            $name = $Matches[1]; $names[$name] = $true
            $target = $archive.CreateEntry($name + '.png', [IO.Compression.CompressionLevel]::Optimal)
            $input = $entry.Open(); $output = $target.Open()
            try { $input.CopyTo($output) } finally { $input.Dispose(); $output.Dispose() }
        }
        if ($entry.FullName -match '/(LICENSE-GRAPHICS|LICENSE)$') {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $projectPath ('assets\Twemoji-' + $Matches[1] + '.txt')), $true)
        }
    }
} finally { $archive.Dispose(); $outStream.Dispose(); $source.Dispose() }
$mapping = [Collections.Generic.List[string]]::new()
$normalized = @{}
foreach ($name in $names.Keys) { $normalized[($name -split '-' | Where-Object { $_ -ne 'fe0f' }) -join '-'] = $name }
$missing = [Collections.Generic.List[string]]::new()
$fullyQualified = 0
foreach ($line in [IO.File]::ReadAllLines($testFile)) {
    if ($line -match '^([0-9A-F ]+)\s*;\s*(fully-qualified|minimally-qualified|unqualified|component)\s*#') {
        $points = @($Matches[1].Trim() -split ' +'); $status = $Matches[2]
        $exact = ($points | ForEach-Object { $_.TrimStart('0').ToLowerInvariant() }) -join '-'
        $simple = ($points | Where-Object { $_ -ne 'FE0F' } | ForEach-Object { $_.TrimStart('0').ToLowerInvariant() }) -join '-'
        $asset = if ($names.ContainsKey($exact)) { $exact } elseif ($normalized.ContainsKey($simple)) { $normalized[$simple] } else { $null }
        if ($asset) { $mapping.Add($exact + "`t" + $asset) }
        elseif ($status -eq 'fully-qualified') { $missing.Add($exact) }
        if ($status -eq 'fully-qualified') { $fullyQualified++ }
    }
}
[IO.File]::WriteAllLines((Join-Path $projectPath 'assets\emoji-map.txt'), $mapping, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $testFile -Destination (Join-Path $projectPath 'assets\emoji-test-17.0.txt') -Force
[pscustomobject]@{Assets=$names.Count;Sequences=$mapping.Count;FullyQualified=$fullyQualified;Missing=$missing.Count} | ConvertTo-Json -Compress
if ($missing.Count) { $missing | Select-Object -First 30; throw 'Some fully-qualified emoji are missing.' }
