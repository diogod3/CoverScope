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
        "icon-512.png",
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

    Write-Host "Package contents:"
    $entries | Sort-Object | ForEach-Object { Write-Host "  $_" }
}
finally {
    $archive.Dispose()
}
