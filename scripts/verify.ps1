param(
    [string]$RuntimeIdentifier = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    function Invoke-DotNet {
        & dotnet @args
        if ($LASTEXITCODE) { throw "dotnet failed: $args" }
    }
    Invoke-DotNet build jieba.NET.sln -c Release --nologo
    if (-not $SkipTests) {
        Invoke-DotNet test jieba.NET.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/test-results
    }
    # Restore treats NU1901–NU1904 as errors for every project, including transitive dependencies.
    Invoke-DotNet list jieba.NET.sln package --vulnerable --include-transitive --no-restore
    Invoke-DotNet pack src/Segmenter/Segmenter.csproj -c Release --no-build -o artifacts/packages
    Invoke-DotNet pack src/Analyser/Analyser.csproj -c Release --no-build -o artifacts/packages
    $packageCache = Join-Path $repo ('artifacts/smoke-cache/' + [Guid]::NewGuid().ToString('N'))
    $sourceConfig = Join-Path $repo 'artifacts/package-smoke.NuGet.Config'
    $localFeed = [System.Security.SecurityElement]::Escape((Join-Path $repo 'artifacts/packages'))
    [System.IO.File]::WriteAllText($sourceConfig, "<configuration><packageSources><clear /><add key='local' value='$localFeed' /><add key='nuget.org' value='https://api.nuget.org/v3/index.json' /></packageSources></configuration>")
    $smokeOutput = Join-Path $repo "artifacts/package-smoke/$RuntimeIdentifier"
    Invoke-DotNet publish test/PackageSmoke/PackageSmoke.csproj -c Release -r $RuntimeIdentifier --self-contained false '-p:UsePackages=true' '-p:PublishSingleFile=true' "-p:RestorePackagesPath=$packageCache" "-p:RestoreConfigFile=$sourceConfig" -o $smokeOutput
    $executable = Join-Path $smokeOutput $(if ($IsWindows -or $env:OS -eq 'Windows_NT') { 'PackageSmoke.exe' } else { 'PackageSmoke' })
    if (Test-Path (Join-Path $smokeOutput 'Resources')) { throw 'Resources must be embedded.' }
    # Run the installed packages outside the checkout and without loose resource files.
    Push-Location ([System.IO.Path]::GetTempPath())
    try {
        foreach ($mode in @('embedded', 'external', 'missing', 'corrupt')) {
            & $executable $mode
            if ($LASTEXITCODE) { throw "Package smoke test failed: $mode" }
        }
    }
    finally { Pop-Location }

    $cli = Join-Path $repo 'src/Segmenter.Cli/bin/Release/net10.0/JiebaNet.Segmenter.Cli.dll'
    $help = & dotnet $cli --help
    if ($LASTEXITCODE -or -not ($help -match '--no-hmm')) { throw 'CLI help failed.' }
    & dotnet $cli --version
    if ($LASTEXITCODE) { throw 'CLI version failed.' }
    & dotnet $cli --unknown
    if ($LASTEXITCODE -ne 2) { throw 'CLI invalid-option exit code failed.' }
    & dotnet $cli --file (Join-Path $repo 'artifacts/nonexistent-input.txt')
    if ($LASTEXITCODE -ne 1) { throw 'CLI missing-file exit code failed.' }
    $inputPath = Join-Path $repo 'artifacts/cli-input.txt'
    [System.IO.File]::WriteAllText($inputPath, "他来到了网易杭研大厦`n北京清华大学", [System.Text.UTF8Encoding]::new($false))
    $withHmm = & dotnet $cli --file $inputPath --delimiter '|'
    if ($LASTEXITCODE) { throw 'CLI segmentation failed.' }
    $withoutHmm = & dotnet $cli --file $inputPath --no-hmm --delimiter '|'
    if ($LASTEXITCODE -or ($withHmm -join "`n") -eq ($withoutHmm -join "`n")) { throw 'CLI --no-hmm did not change segmentation.' }
    Write-Host 'All verification passed.'
}
finally { Pop-Location }
