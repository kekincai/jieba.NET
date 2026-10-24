param(
    [string]$BaselineDll,
    [string[]]$Modes = @('cut', 'no-hmm', 'search', 'tfidf'),
    [int]$Repetitions = 3,
    [int]$Operations = 20000,
    [int]$WarmupCalls = 50000,
    [int]$SettleMs = 1000,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $repo 'artifacts/performance/results.json' }
if ($BaselineDll) { $BaselineDll = (Resolve-Path -LiteralPath $BaselineDll).Path }
dotnet build (Join-Path $repo 'tools/Benchmark/Benchmark.csproj') -c Release --nologo -v minimal
if ($LASTEXITCODE) { throw 'Benchmark build failed.' }
$modern = Join-Path $repo 'tools/Benchmark/bin/Release/net10.0/Benchmark.dll'
$results = [System.Collections.Generic.List[object]]::new()
for ($repeat = 1; $repeat -le $Repetitions; $repeat++) {
    foreach ($mode in $Modes) {
        $implementations = [ordered]@{ Modern = $modern }
        if ($BaselineDll) {
            # Alternate process order to reduce warm-cache and background-load bias.
            $implementations = if ($repeat % 2) { [ordered]@{ Baseline = $BaselineDll; Modern = $modern } }
                               else { [ordered]@{ Modern = $modern; Baseline = $BaselineDll } }
        }
        foreach ($implementation in $implementations.GetEnumerator()) {
            $json = & dotnet $implementation.Value $mode $Operations $WarmupCalls $SettleMs
            if ($LASTEXITCODE) { throw "Benchmark failed: $($implementation.Key), $mode" }
            $entry = $json | ConvertFrom-Json
            $entry | Add-Member Implementation $implementation.Key
            $entry | Add-Member Repetition $repeat
            $results.Add($entry)
            Write-Host ($entry | ConvertTo-Json -Compress)
        }
    }
}
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath -Parent) | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "Saved: $OutputPath"
