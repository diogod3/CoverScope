param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [string] $Version = "0.1.0-beta.1"
)

$ErrorActionPreference = "Stop"
$package = Get-Item (Resolve-Path $PackagePath).Path
$root = Join-Path ([System.IO.Path]::GetTempPath()) "coverscope-smoke-$([Guid]::NewGuid().ToString('N'))"
$toolDirectory = Join-Path $root "tool"
$invocationDirectory = Join-Path $root "workspace"
$nugetConfig = Join-Path $root "NuGet.Config"
$process = $null

try {
    New-Item -ItemType Directory -Path $toolDirectory, $invocationDirectory | Out-Null
    Set-Content -Path (Join-Path $invocationDirectory "Smoke.sln") -Value ""

    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$($package.DirectoryName)" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfig

    & dotnet tool install DD3.CoverScope.Tool --tool-path $toolDirectory --version $Version --configfile $nugetConfig
    if ($LASTEXITCODE -ne 0) {
        throw "Tool installation failed with exit code $LASTEXITCODE."
    }

    $toolName = if ($IsWindows) { "coverscope.exe" } else { "coverscope" }
    $toolPath = Join-Path $toolDirectory $toolName

    $reportedVersion = (& $toolPath --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $reportedVersion -ne $Version) {
        throw "Expected --version to report '$Version', but received '$reportedVersion'."
    }

    & $toolPath --help | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "--help failed with exit code $LASTEXITCODE."
    }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $toolPath
    $startInfo.WorkingDirectory = $invocationDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.ArgumentList.Add("Smoke.sln")
    $startInfo.ArgumentList.Add("--no-browser")
    $startInfo.ArgumentList.Add("--port")
    $startInfo.ArgumentList.Add($port.ToString())

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "The installed tool process did not start."
    }

    $baseUrl = "http://127.0.0.1:$port"
    $started = $false
    $lastProbeError = "No response received."
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) {
            throw "CoverScope exited before it became ready with exit code $($process.ExitCode)."
        }

        try {
            $home = Invoke-WebRequest -Uri "$baseUrl/" -UseBasicParsing -TimeoutSec 2
            if ($home.StatusCode -eq 200) {
                $started = $true
                break
            }
        }
        catch {
            $lastProbeError = $_.Exception.Message
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $started) {
        throw "CoverScope did not become ready at $baseUrl. Last probe error: $lastProbeError"
    }

    if ($home.Content -notmatch "CoverScope" -or $home.Content -notmatch "Smoke.sln") {
        throw "The home page did not contain the expected application and target text."
    }

    foreach ($asset in @(
        "/app.css?v=smoke",
        "/coverscope-mark.svg?v=smoke",
        "/coverscope.js?v=smoke",
        "/_framework/blazor.web.js"
    )) {
        $response = Invoke-WebRequest -Uri "$baseUrl$asset" -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -ne 200 -or $response.RawContentLength -le 0) {
            throw "Static asset '$asset' was not served correctly."
        }
    }

    Write-Host "Smoke test passed at $baseUrl."
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        if (-not $IsWindows) {
            & /bin/kill -INT $process.Id
            if (-not $process.WaitForExit(10000)) {
                $process.Kill($true)
            }
        }
        else {
            $process.Kill($true)
        }
    }

    if ($null -ne $process) {
        $process.Dispose()
    }

    if (Test-Path $root) {
        Remove-Item -Path $root -Recurse -Force
    }
}
