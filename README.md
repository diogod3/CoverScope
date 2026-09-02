<p align="center">
  <img src="src/DD3.CoverScope.Web/wwwroot/app-icon.svg" width="112" alt="CoverScope logo" />
</p>

<h1 align="center">CoverScope</h1>

<p align="center">
  A cross-platform, local-first .NET code coverage explorer with clear metrics,
  source highlighting, test-failure insights, and Cobertura support.
</p>

> [!NOTE]
> CoverScope is currently in early beta. The core coverage workflow is functional, but packaging, history, comparisons, and editor integrations are still evolving.

## Why CoverScope?

Coverage data is useful only when it is easy to navigate and understand. CoverScope separates coverage collection from visualization, providing a focused standalone workspace without depending on an IDE extension.

It can run coverage for a solution or test project through Coverlet, import existing Cobertura reports, merge reports from multiple test projects, and connect coverage gaps directly to annotated source code.

## Features

- Solution-wide line, branch, and method coverage metrics
- Project → namespace → class metrics drill-down
- Project → namespace → source file → class → method explorer
- Covered, partially covered, and uncovered source-line highlighting
- Covered, missed, and total line counts
- Sorting by coverage percentage, missed lines, size, or name
- Filters for coverage gaps, thresholds, minimum size, and text
- Automatic merging of reports from multiple test projects
- Partial-class aggregation across source files
- Structured failed-test results with exceptions, stack traces, output, and source locations
- Coverage results remain available when tests fail but collection succeeds
- Persistent Coverlet exclusions for assemblies, files, attributes, and auto-properties
- Cross-platform solution and project browser
- Local-first operation with no required external service

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [`coverlet.collector`](https://github.com/coverlet-coverage/coverlet) in test projects when running new coverage collections

Recent .NET test templates commonly include Coverlet. Otherwise, add it to each test project:

```bash
dotnet add path/to/Tests.csproj package coverlet.collector
```

## Getting started

Clone the repository:

```bash
git clone https://github.com/diogod3/CoverScope.git
cd CoverScope
```

Restore and run:

```bash
dotnet restore DD3.CoverScope.sln
dotnet run --project src/DD3.CoverScope.Web/DD3.CoverScope.Web.csproj
```

Alternatively, open `DD3.CoverScope.sln` in Visual Studio and start `DD3.CoverScope.Web`.

Open the local URL printed by the application. The development profile currently uses `http://localhost:5073`.

## Running coverage

1. Select the current target in the command bar.
2. Browse to a `.sln`, `.slnx`, or test-project file.
3. Configure optional collection exclusions.
4. Select **Run coverage**.

CoverScope runs the equivalent of:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Generated reports are stored beside the selected target under `coverage-output/`. This directory is ignored by Git.

If tests fail, CoverScope presents the failed tests separately from infrastructure or collection errors. When a usable coverage report was still produced, it remains available with a visible failed-run warning.

## Opening an existing report

1. Select **Open report**.
2. Choose a Cobertura XML file or enter its local path.
3. Provide a source root if the report paths cannot be resolved automatically.

## Running the tests

```bash
dotnet test DD3.CoverScope.sln
```

## Repository structure

- `src/DD3.CoverScope.Web` — Blazor application, coverage services, and static assets
- `tests/DD3.CoverScope.Tests` — parser, merger, settings, filesystem, metrics, and TRX tests
- `DD3.CoverScope.sln` — solution entry point
- `global.json` — .NET 10 SDK selection policy

## Data and privacy

CoverScope runs locally. Solution paths, source files, coverage reports, and test output are not sent to an external service by the application.

Collection settings are stored under the current user's local application-data directory. Imported reports are copied to a CoverScope application-data folder for local processing.

## Roadmap

TBD

## License

Apache License 2.0 is the intended license. Until the repository includes a `LICENSE` file with finalized copyright attribution, no open-source license has been granted.
