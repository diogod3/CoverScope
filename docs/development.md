# Development

Build, validation, and architecture details for contributors. See the [README](../README.md) for product information and installation, or the [review guide](review-guide.md) for evidence interpretation.

## Development and validation

From the repository root:

```text
dotnet restore DD3.CoverScope.sln
dotnet build DD3.CoverScope.sln --configuration Release --no-restore
dotnet test DD3.CoverScope.sln --configuration Release --no-build
dotnet pack src/DD3.CoverScope.Web/DD3.CoverScope.Web.csproj --configuration Release --no-build --output artifacts/packages
pwsh -File scripts/Inspect-Package.ps1 -PackagePath artifacts/packages/DD3.CoverScope.Tool.0.2.0-beta.2.nupkg
pwsh -File scripts/Smoke-Test.ps1 -PackagePath artifacts/packages/DD3.CoverScope.Tool.0.2.0-beta.2.nupkg
```

The final two commands require PowerShell. Package inspection checks required contents and unwanted embedded data. The smoke test installs the package and checks that an explicit target opens setup without automatic collection; it does not execute a complete review. `ReviewProcessTests` exercises Git comparisons, source-worker integration, and process cancellation, and can restore temporary fixture projects.

The maintainer validated installation, a complete review, and reopening saved history for the previous beta. CI checks build, tests, packing, package contents, and installed-tool startup on Windows, Linux, and macOS. Use the [current workflow results](https://github.com/diogod3/CoverScope/actions/workflows/ci.yml) to assess a specific commit. These checks do not replace visual verification of graph interactions or a complete review against a real repository.

NuGet.org publication was established with [v0.2.0-beta.1](https://github.com/diogod3/CoverScope/releases/tag/v0.2.0-beta.1). Each new release must pass its own validation and publication workflow.

## Try a local package

After packing, install in a new folder outside the checkout:

```text
dotnet tool install DD3.CoverScope.Tool --tool-path ../coverscope-beta-test --version 0.2.0-beta.2 --source ./artifacts/packages --no-cache
cd ../coverscope-beta-test
./coverscope
```

Choose an unused installation folder. On Windows, the executable is `coverscope.exe`. This installs your locally built package without requiring publication to NuGet.org.

## CI and releases

Pull requests and pushes to `main` run restore, build, tests, packing, package inspection, and an installed-tool smoke test on Windows, Linux, and macOS. The release workflow reuses these same checks for the tagged commit and waits for every platform to pass. It publishes the tested Linux-built package to NuGet.org, then attaches it to a GitHub prerelease with the matching release notes.

### One-time NuGet.org setup

Publishing uses [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) through [NuGet/login](https://github.com/NuGet/login).

1. In NuGet.org, create a GitHub trusted publishing policy with owner `diogod3`, repository `CoverScope`, and workflow filename `release.yml`. Leave the environment field empty; the workflow does not use a GitHub environment.
2. Scope the policy to `DD3.CoverScope.Tool`. Allow publishing new packages and new versions so the first publication is covered. The account must be entitled to publish that package ID.
3. Add a GitHub Actions repository secret named `NUGET_USER` containing that NuGet.org account's profile username, not its email address.

The workflow exchanges GitHub's identity token for a short-lived NuGet API key. No permanent NuGet API key or GitHub Packages credentials are needed. Follow NuGet.org's policy activation guidance when scheduling the first publication.

### Publish a beta

Merge the release changes into `main` and confirm CI passes. Keep the project `Version` and `docs/releases/v<VERSION>.md` aligned. Then tag that commit; for this beta:

```text
git switch main
git pull --ff-only
git tag -a v0.2.0-beta.2 -m "CoverScope v0.2.0-beta.2"
git push origin v0.2.0-beta.2
```

The release workflow rejects tags whose version or release notes do not match, or whose commit is not on `main`. Review the workflow result and confirm that the package is available on NuGet.org before announcing the release. Installation instructions in the README apply once publication completes.

## Application structure

- `Brokers`: filesystem/process boundaries and process containment.
- `Services/Reviews`: persistence, comparison, verification, evidence interpretation, export, and run orchestration.
- `Models/Reviews`: run records, source identities, checks, and evidence.
- `Analysis`: a separate-process Roslyn/MSBuild indexing entry point.
- `Components/Review`: source, types, relationships, verification, progress, and target selection.

The architecture follows The Standard's principles of explicit responsibilities and separation of infrastructure from application flow. Components request actions and render evidence; they do not start verification tools. Orchestration owns the run lifecycle; parsers interpret artifacts without deciding whether a change is acceptable.
