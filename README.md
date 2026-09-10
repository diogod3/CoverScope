<p align="center">
  <img src="src/DD3.CoverScope.Web/wwwroot/app-icon.svg" width="112" alt="CoverScope logo" />
</p>

<h1 align="center">CoverScope</h1>

<p align="center">
  Review your .NET changes with source diffs, tests, coverage,
  and code relationships in one place.
</p>

> [!NOTE]
> CoverScope is currently in beta. Expect rough edges and changes as the tool evolves. Feedback from real-world use helps shape the next release.

<!-- Add a demo of the current committed-review workflow here before public launch. -->

## Why CoverScope?

Reviewing a change means more than reading a diff. You need to understand **what changed**, **how it was implemented**, **how it was verified**, and **what else deserves attention**.

CoverScope brings that information into a local workspace. Start with an overview of your branch, explore the changed code, inspect test results and coverage, and follow relationships to related types.

Review visually, revisit a saved run, or share the same evidence with an agent through Markdown and JSON exports. No model connection is required. You decide what the evidence means and whether the change is ready.

## Features

- **Explore branch changes** — navigate changed files, types, members, and tests, with diffs and full-source views.
- **See changes and coverage together** — identify added or modified code and whether it was exercised, without losing the surrounding context.
- **Focus on your branch** — filter coverage and formatting findings to changed files or lines.
- **Investigate related code** — explore an interactive graph with pan, zoom, grouped interfaces and implementations, and direct navigation to types.
- **Inspect verification results** — review builds, test failures, coverage gaps, and formatting findings together. Available coverage remains useful even when tests fail.
- **Keep and share reviews** — reopen saved runs or export concise Markdown and detailed JSON evidence.
- **Work locally** — use a standalone browser workspace without requiring an IDE extension or model integration.

## Requirements

- .NET 10 SDK and Git.
- A Git repository with committed changes and a clean worktree.
- A locally available branch to compare against.
- For tests and coverage, xUnit v2 test projects configured with `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, and `coverlet.collector`.

Code exploration currently focuses on C#. The graph shows static code relationships to help you investigate possible effects; it does not prove runtime impact. See the [review guide](docs/review-guide.md#requirements) for setup details and [current limitations](docs/review-guide.md#evidence-limits-and-known-limitations).

## Installation

Install the beta from NuGet.org:

```text
dotnet tool install --global DD3.CoverScope.Tool --version 0.2.0-beta.2 --source https://api.nuget.org/v3/index.json
coverscope
```

To update an existing installation to the latest beta:

```text
dotnet tool update --global DD3.CoverScope.Tool --prerelease --source https://api.nuget.org/v3/index.json
```

To build and try a local package, see [development](docs/development.md#try-a-local-package).

## Your first review

1. **Choose a solution or project.** Use the file picker or enter its path.
2. **Select the branch to compare against.** CoverScope compares your checked-out commit with the common ancestor of that branch.
3. **Choose your verification checks and start the review.** Follow progress as the evidence is collected.
4. **Explore the results.** Review the changes, inspect coverage and test results, and follow related code in the graph.

You can also supply a target when launching:

```text
coverscope path/to/MySolution.slnx
```

This opens setup with the target selected. Once a review finishes, return to it through **Runs** or export the evidence to share with a colleague or agent.

## Documentation and feedback

- [Review guide](docs/review-guide.md) — navigation, coverage, formatting, saved runs, and evidence limits.
- [Development](docs/development.md) — building, testing, architecture, and releases.
- [Beta release notes](docs/releases/v0.2.0-beta.2.md) — what's changed and compatibility notes.

Found something confusing or incorrect? Open a repository issue with your version, reproduction steps, and expected/actual behaviour. Screenshots and exported summaries can help explain the problem. Check them for private code, paths, or test output before sharing.

## Development disclosure

CoverScope was developed with the assistance of OpenAI's GPT-6 Astra through Codex. Project direction, requirements, review, testing, and release decisions remain human-led.

## License

CoverScope is licensed under the [Apache License 2.0](LICENSE).
