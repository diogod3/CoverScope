param([Parameter(Mandatory = $true)][string] $PackagePath)
$ErrorActionPreference = "Stop"
$package = (Resolve-Path $PackagePath).Path
$root = Join-Path ([System.IO.Path]::GetTempPath()) ("coverscope-smoke-" + [Guid]::NewGuid().ToString("N"))
$tool = Join-Path $root "tool"
$repository = Join-Path $root "repository"
$process = $null
try {
    New-Item -ItemType Directory -Path $tool, $repository | Out-Null
    $version = ([System.IO.Compression.ZipFile]::OpenRead($package))
    try {
        $entry = $version.Entries | Where-Object { $_.FullName -eq 'DD3.CoverScope.Tool.nuspec' }
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try { $nuspec = [xml]$reader.ReadToEnd(); $packageVersion = $nuspec.package.metadata.version } finally { $reader.Dispose() }
    } finally { $version.Dispose() }
    $nugetConfig = Join-Path $root "NuGet.Config"
    @"
<configuration><packageSources><clear /><add key="local" value="$([System.Security.SecurityElement]::Escape((Split-Path $package)))" /></packageSources></configuration>
"@ | Set-Content $nugetConfig
    & dotnet tool install DD3.CoverScope.Tool --tool-path $tool --version $packageVersion --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) { throw "Tool installation failed." }
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' | Set-Content (Join-Path $repository "Smoke.csproj")
    'public class Example { }' | Set-Content (Join-Path $repository "Example.cs")
    & git -C $repository init -b main
    & git -C $repository -c user.name=Fixture -c user.email=fixture@example.invalid add .
    & git -C $repository -c user.name=Fixture -c user.email=fixture@example.invalid commit -m base
    if ($LASTEXITCODE -ne 0) { throw "Git fixture creation failed." }
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port; $listener.Stop()
    $command = Join-Path $tool $(if ($IsWindows) { "coverscope.exe" } else { "coverscope" })
    $start = [System.Diagnostics.ProcessStartInfo]::new($command)
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.ArgumentList.Add((Join-Path $repository "Smoke.csproj"))
    $start.ArgumentList.Add("--no-browser")
    $start.ArgumentList.Add("--port")
    $start.ArgumentList.Add($port.ToString())
    $process = [System.Diagnostics.Process]::Start($start)
    $page = $null
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw "Installed tool exited unexpectedly." }
        try { $page = Invoke-WebRequest "http://127.0.0.1:$port/" -NoProxy -TimeoutSec 2; break }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if ($null -eq $page -or $page.Content -notmatch 'Compare committed changes' -or $page.Content -notmatch 'Smoke.csproj') {
        throw "Installed tool did not show explicit run setup."
    }
    if (Test-Path (Join-Path $repository '.coverscope/reports')) { throw "Startup unexpectedly created legacy coverage output." }
    Write-Host "Installed tool opens explicit committed-review setup. No automatic collection occurred."
    Write-Host "Run the ReviewProcessTests suite for actual Git and process-cancellation integration checks."
} finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
}
