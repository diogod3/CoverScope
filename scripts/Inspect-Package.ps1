param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = "Stop"
$resolvedPackage = (Resolve-Path $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)

try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    $requiredEntries = @(
        "DD3.CoverScope.Tool.nuspec",
        "README.md",
        "LICENSE",
        "tools/net10.0/any/DotnetToolSettings.xml",
        "tools/net10.0/any/DD3.CoverScope.Web.dll",
        "tools/net10.0/any/DD3.CoverScope.Web.deps.json",
        "tools/net10.0/any/DD3.CoverScope.Web.runtimeconfig.json",
        "tools/net10.0/any/wwwroot/app.css",
        "tools/net10.0/any/wwwroot/coverscope-mark.svg",
        "tools/net10.0/any/wwwroot/coverscope.js",
        "tools/net10.0/any/wwwroot/favicon.svg",
        "tools/net10.0/any/wwwroot/icon-512.png"
    )

    $missingEntries = @($requiredEntries | Where-Object { $_ -notin $entries })
    if ($missingEntries.Count -gt 0) {
        throw "Package is missing required entries: $($missingEntries -join ', ')"
    }

    $forbiddenEntries = @($entries | Where-Object {
        $_ -match "(^|/)(coverage-output|TestResults|private-fixtures)(/|$)" -or
        $_ -match "(^|/)(\.env|NuGet\.Config)$"
    })
    if ($forbiddenEntries.Count -gt 0) {
        throw "Package contains forbidden local or generated entries: $($forbiddenEntries -join ', ')"
    }

    $textEntryPattern = "\.(nuspec|md|json|xml|config|props|targets|css|js|svg)$"
    foreach ($entry in $archive.Entries | Where-Object { $_.FullName -match $textEntryPattern }) {
        $stream = $entry.Open()
        $reader = [System.IO.StreamReader]::new($stream)
        try {
            $text = $reader.ReadToEnd()
            if ($text -match "github_pat_[A-Za-z0-9_]+" -or
                $text -match "ghp_[A-Za-z0-9]{20,}" -or
                $text -match "GITHUB_NUGET_TOKEN\s*[:=]") {
                throw "Package entry '$($entry.FullName)' appears to contain a credential."
            }
            if ($text -match "(?i)([A-Z]:\\Users\\[^\\]+\\|/Users/[^/]+/|/home/[^/]+/)") {
                throw "Package entry '$($entry.FullName)' contains an absolute user path."
            }
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }

    Write-Host "Package hygiene checks passed."
    Write-Host "Package contents:"
    $entries | Sort-Object | ForEach-Object { Write-Host "  $_" }
}
finally {
    $archive.Dispose()
}
