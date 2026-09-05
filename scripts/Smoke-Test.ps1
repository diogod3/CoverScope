param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [string] $Version = "0.1.0-beta.3"
)

$ErrorActionPreference = "Stop"
$package = Get-Item (Resolve-Path $PackagePath).Path
$root = Join-Path ([System.IO.Path]::GetTempPath()) "coverscope-smoke-$([Guid]::NewGuid().ToString('N'))"
$toolDirectory = Join-Path $root "tool"
$invocationDirectory = Join-Path $root "workspace"
$nugetConfig = Join-Path $root "NuGet.Config"
$process = $null
$secondaryProcess = $null
$browserProcesses = @()

try {
    New-Item -ItemType Directory -Path $toolDirectory, $invocationDirectory | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $invocationDirectory "Smoke.Tests.csproj")
    @"
using Xunit;

public sealed class SmokeTests
{
    [Fact]
    public void CoverageTarget() => Assert.True(true);
}
"@ | Set-Content -Path (Join-Path $invocationDirectory "SmokeTests.cs")

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
    $startInfo.ArgumentList.Add("Smoke.Tests.csproj")
    $startInfo.ArgumentList.Add("--no-browser")
    $startInfo.ArgumentList.Add("--port")
    $startInfo.ArgumentList.Add($port.ToString())

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "The installed tool process did not start."
    }

    $fallbackUrl = "http://127.0.0.1:$port"
    $friendlyUrl = "http://coverscope.localhost:$port"
    $started = $false
    $lastProbeError = "No response received."
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) {
            throw "CoverScope exited before it became ready with exit code $($process.ExitCode)."
        }

        try {
            $homeResponse = Invoke-WebRequest -Uri "$fallbackUrl/" -UseBasicParsing -TimeoutSec 2
            if ($homeResponse.StatusCode -eq 200) {
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
        throw "CoverScope did not become ready at $fallbackUrl. Last probe error: $lastProbeError"
    }

    $listeners = @([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
        Where-Object { $_.Port -eq $port })
    if ($listeners.Count -eq 0) {
        throw "No active listener was found for CoverScope port $port."
    }
    if (@($listeners | Where-Object { -not $_.Address.Equals([System.Net.IPAddress]::Loopback) }).Count -gt 0) {
        $addresses = ($listeners | ForEach-Object { $_.Address.ToString() }) -join ", "
        throw "CoverScope must listen only on IPv4 loopback, but port $port was bound at: $addresses"
    }

    function Assert-CoverScopeEndpoint([string] $origin) {
        $homePageResponse = Invoke-WebRequest -Uri "$origin/" -UseBasicParsing -NoProxy -TimeoutSec 10
        if ($homePageResponse.StatusCode -ne 200 -or
            $homePageResponse.Content -notmatch "CoverScope" -or
            $homePageResponse.Content -notmatch "Smoke.Tests.csproj") {
            throw "The home page was not served correctly through $origin."
        }

        foreach ($asset in @(
            "/app.css?v=smoke",
            "/coverscope-mark.svg?v=smoke",
            "/coverscope.js?v=smoke",
            "/_framework/blazor.web.js"
        )) {
            $response = Invoke-WebRequest -Uri "$origin$asset" -UseBasicParsing -NoProxy -TimeoutSec 10
            if ($response.StatusCode -ne 200 -or $response.RawContentLength -le 0) {
                throw "Static asset '$asset' was not served correctly through $origin."
            }
        }

        $negotiate = Invoke-WebRequest `
            -Uri "$origin/_blazor/negotiate?negotiateVersion=1" `
            -Method Post `
            -ContentType "application/json" `
            -Body "" `
            -UseBasicParsing `
            -NoProxy `
            -TimeoutSec 10
        if ($negotiate.StatusCode -ne 200 -or $negotiate.Content -notmatch "connectionId") {
            throw "The Blazor connection negotiation did not succeed through $origin."
        }
    }

    Assert-CoverScopeEndpoint $fallbackUrl
    try {
        Assert-CoverScopeEndpoint $friendlyUrl
        Write-Host "Friendly URL served the app, assets, and Blazor negotiation through the system HTTP resolver."
    }
    catch {
        if (-not $IsWindows -or $_.Exception.Message -notmatch "No such host") {
            throw
        }

        Write-Warning "The Windows runner system HTTP resolver does not resolve localhost subdomains; browser verification remains required."
    }

    Write-Host "Numeric loopback fallback served the app, assets, and Blazor negotiation."
    $baseUrl = $friendlyUrl

    $browserCandidates = @()
    if ($IsWindows) {
        $browserCandidates += Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"
        if ($null -ne ${env:ProgramFiles(x86)}) {
            $browserCandidates += Join-Path ${env:ProgramFiles(x86)} "Google\Chrome\Application\chrome.exe"
        }
    }
    elseif ($IsMacOS) {
        $browserCandidates += "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
        $browserCandidates += "/Applications/Chromium.app/Contents/MacOS/Chromium"
    }
    else {
        foreach ($commandName in @("google-chrome", "google-chrome-stable", "chromium", "chromium-browser")) {
            $browserCommand = Get-Command $commandName -ErrorAction SilentlyContinue
            if ($null -ne $browserCommand) {
                $browserCandidates += $browserCommand.Source
            }
        }
    }

    $browserPath = $browserCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if ($null -eq $browserPath) {
        Write-Warning "Chrome or Chromium was not available; skipping interactive automatic-collection verification."
    }
    else {
        function Start-CoverScopeBrowser([string] $profilePath) {
            $arguments = @(
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--no-first-run",
                "--no-default-browser-check",
                "--no-proxy-server",
                "--user-data-dir=$profilePath",
                $baseUrl
            )
            return Start-Process -FilePath $browserPath -ArgumentList $arguments -PassThru
        }

        $firstBrowser = Start-CoverScopeBrowser (Join-Path $root "browser-first")
        $browserProcesses += $firstBrowser
        $coverageRoot = Join-Path $invocationDirectory ".coverscope/reports"
        $automaticRunCompleted = $false
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            $runDirectories = if (Test-Path $coverageRoot) {
                @(Get-ChildItem -Path $coverageRoot -Directory)
            }
            else {
                @()
            }
            if ($runDirectories.Count -gt 0) {
                $manifestPath = Join-Path $runDirectories[0].FullName "run.json"
                if (Test-Path $manifestPath) {
                    $manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
                    $coverageArtifacts = @($manifest.artifacts | Where-Object { $_.kind -eq "coverage" })
                    $testArtifacts = @($manifest.artifacts | Where-Object { $_.kind -eq "testResults" })
                    if ($manifest.schemaVersion -eq 1 -and
                        $manifest.status -eq "completed" -and
                        $coverageArtifacts.Count -gt 0 -and
                        $testArtifacts.Count -gt 0) {
                        $automaticRunCompleted = $true
                        break
                    }
                }
            }
            Start-Sleep -Milliseconds 500
        }

        if (-not $automaticRunCompleted) {
            throw "An interactive browser loaded CoverScope, but explicit-target coverage did not produce a successful run manifest with coverage and test artifacts."
        }

        $initialRunCount = $runDirectories.Count
        if (-not $firstBrowser.HasExited) {
            $firstBrowser.Kill()
            if (-not $firstBrowser.WaitForExit(10000)) {
                throw "The first headless browser did not stop within 10 seconds."
            }
        }

        $secondBrowser = Start-CoverScopeBrowser (Join-Path $root "browser-second")
        $browserProcesses += $secondBrowser
        Start-Sleep -Seconds 3
        $finalRunCount = @(Get-ChildItem -Path $coverageRoot -Directory).Count
        if ($finalRunCount -ne $initialRunCount) {
            throw "A second interactive browser circuit started duplicate coverage. Expected $initialRunCount run, found $finalRunCount."
        }

        $secondListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $secondListener.Start()
        $secondPort = ([System.Net.IPEndPoint]$secondListener.LocalEndpoint).Port
        $secondListener.Stop()

        $secondStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $secondStartInfo.FileName = $toolPath
        $secondStartInfo.WorkingDirectory = $invocationDirectory
        $secondStartInfo.UseShellExecute = $false
        $secondStartInfo.ArgumentList.Add("Smoke.Tests.csproj")
        $secondStartInfo.ArgumentList.Add("--no-browser")
        $secondStartInfo.ArgumentList.Add("--port")
        $secondStartInfo.ArgumentList.Add($secondPort.ToString())

        $secondaryProcess = [System.Diagnostics.Process]::new()
        $secondaryProcess.StartInfo = $secondStartInfo
        if (-not $secondaryProcess.Start()) {
            throw "The second installed tool process did not start."
        }

        $secondFallbackUrl = "http://127.0.0.1:$secondPort"
        $secondStarted = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            if ($secondaryProcess.HasExited) {
                throw "The second CoverScope process exited before it became ready with exit code $($secondaryProcess.ExitCode)."
            }
            try {
                $response = Invoke-WebRequest -Uri "$secondFallbackUrl/" -UseBasicParsing -TimeoutSec 2
                if ($response.StatusCode -eq 200) {
                    $secondStarted = $true
                    break
                }
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }
        if (-not $secondStarted) {
            throw "The second CoverScope process did not become ready at $secondFallbackUrl."
        }

        $baseUrl = "http://coverscope.localhost:$secondPort"
        $thirdBrowser = Start-CoverScopeBrowser (Join-Path $root "browser-third")
        $browserProcesses += $thirdBrowser
        $secondRunCompleted = $false
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            $runDirectories = @(Get-ChildItem -Path $coverageRoot -Directory)
            if ($runDirectories.Count -gt $initialRunCount) {
                $terminalManifests = @($runDirectories | ForEach-Object {
                    $path = Join-Path $_.FullName "run.json"
                    if (Test-Path $path) {
                        Get-Content -Raw -Path $path | ConvertFrom-Json
                    }
                } | Where-Object { $_.status -eq "completed" })
                if ($terminalManifests.Count -gt $initialRunCount) {
                    $secondRunCompleted = $true
                    break
                }
            }
            Start-Sleep -Milliseconds 500
        }
        if (-not $secondRunCompleted) {
            throw "Running the installed tool twice did not preserve two separate completed runs."
        }

        Write-Host "Automatic explicit-target collection ran once per process and preserved two completed runs."
    }

    Write-Host "Smoke test passed at $baseUrl."
}
finally {
    foreach ($browserProcess in $browserProcesses) {
        if ($null -ne $browserProcess -and -not $browserProcess.HasExited) {
            $browserProcess.Kill()
            if (-not $browserProcess.WaitForExit(10000)) {
                Write-Warning "A headless browser process did not stop within 10 seconds."
            }
        }
        if ($null -ne $browserProcess) {
            $browserProcess.Dispose()
        }
    }
    foreach ($toolProcess in @($secondaryProcess, $process)) {
        if ($null -ne $toolProcess -and -not $toolProcess.HasExited) {
            if (-not $IsWindows) {
                & /bin/kill -INT $toolProcess.Id
                if (-not $toolProcess.WaitForExit(10000)) {
                    $toolProcess.Kill($true)
                }
            }
            else {
                $toolProcess.Kill($true)
                $toolProcess.WaitForExit()
            }
        }

        if ($null -ne $toolProcess) {
            $toolProcess.Dispose()
        }
    }

    if (Test-Path $root) {
        try {
            Remove-Item -Path $root -Recurse -Force
        }
        catch {
            Write-Warning "Temporary smoke-test files could not be removed: $($_.Exception.Message)"
        }
    }
}
