# Review guide

Detailed guidance for CoverScope `0.2.0-beta.2`. See the [README](../README.md) for an introduction and installation.

## Requirements

- .NET 10 SDK and Git on `PATH`. The source checkout's `global.json` requests SDK `10.0.100` with `latestFeature` roll-forward.
- A clean Git worktree, a locally available target branch/reference, and exactly one common ancestor with the reviewed commit.
- For tests and coverage: the current profile is xUnit v2 through VSTest. Configure `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, and `coverlet.collector` in the test projects.
- Project dependencies must be restorable in the environment running CoverScope. Source indexing loads C# projects through Roslyn/MSBuild and may restore dependencies for both revisions, even when tests are disabled.

## Start a review

Launch the installed executable with no arguments to use the picker, or supply a solution/project path:

```text
coverscope
coverscope path/to/SolutionExample.slnx
coverscope path/to/Example.Tests.csproj --no-browser
coverscope --port 5080
coverscope --help
```

1. Choose a solution or project using **Browse**, folder navigation, and quick locations, or enter its path and select **Load repository**. Loading indicators appear while the picker and repository configuration are resolved.
2. Choose a local target reference. Fetch missing references through your Git client.
3. Select **Tests and coverage** and **Formatting analysis** independently, and set the build configuration. Git comparison and C# indexing are included.
4. Select **Start run**. The preparation screen appears before repository validation completes, then displays the active phase and elapsed time. A preparation failure returns to setup with an error.

Supplying a command-line target opens setup; it does not automatically execute a review. Build and tests run when **Tests and coverage** is enabled. Formatting analyses findings without applying edits. Baseline tests are never executed.

CoverScope checks for staged, unstaged, unmerged, and untracked changes. Ignored generated outputs do not block collection. It records the checked-out commit, target commit, and common ancestor, then compares the ancestor with the checked-out commit. It does not fetch, switch branches, stash, commit, or fix source files. Builds, restores, and tests can still produce their normal outputs and side effects.

## Explore the evidence

### Overview and source

**Overview** shows change counts, verification results, the relationship graph, and changed types. The adjacent graph and type panels share a height, with the type list scrolling internally.

**Changes** provides a searchable hierarchy and a shared source pane. Opening a changed file defaults to **Diff**; **Reviewed** and **Baseline** views show the captured full file. Selecting a member opens its file at the relevant line. Partial declarations appear as one logical type with their source files.

The diff hides raw Git metadata, separates distant changed sections, and shows baseline/reviewed line numbers. Added and removed code has green/red background highlighting limited to the code area. The neutral gutter shows coverage separately: **● covered**, **○ uncovered**, and **◐ partial branches**. Hover for recorded hits and branch details. Removed lines have no reviewed coverage; an empty coverage gutter does not mean a line is uncovered.

### Relationship graph

- **Changed objects only** is enabled by default. Turn it off to include related unchanged types and their interface/implementation groups.
- **Show tests** is enabled by default. Turn it off to hide types with indexed test definitions and their relationships. Helpers without test definitions remain visible.
- **Find class or interface** highlights visible matches by name or namespace, ignoring case. Typing leaves the camera in place. Use **Next**/**Previous** (or Enter/Shift+Enter) to center on a match; **Clear** or Escape removes the highlights. Search follows the active type filters.
- Pan, zoom, use **Fit all**, or center the selected type. The graph initially centers on the type with the most distinct connections in the displayed graph. Selecting a node does not replace the graph.
- Single-click a node to show its side-panel details; double-click to open its type. Keyboard selection and the side panel's **Open type** button are also available.
- Interfaces and implementations are grouped while retaining separate nodes and exact relationship endpoints. A concrete reference points to that concrete type; an interface reference is not rewritten into an assumed dependency-injection binding.
- Types containing recorded test definitions have a double outline and a **Tests** caption. Change-status colours remain separate from that distinction.
- Arrows point from the referencing type to the referenced type. Added, removed, and retained relationships are distinguishable. Select an edge for its kind, revision, and recorded source locations; use the revision selector for the comparison, reviewed, or baseline relationships.

### Verification and coverage

**Verification** includes build output, individual test executions and failures, coverage, and formatting findings. Failed tests do not prevent independently available coverage and formatting evidence from appearing.

Coverage offers these code ranges, defaulting to **Changed files** when source consistency is established:

| Range | Display and totals |
| --- | --- |
| **All code** | All source files reported by the selected collection scope. |
| **Changed files** | Full-file coverage for changed files with collector evidence; deleted files are excluded. |
| **Changed lines** | Added or modified executable lines, with three surrounding source lines for context. Context and deleted lines are excluded from totals. Method totals are unavailable at this granularity. |

Line hits and per-line branch details are shown when the run recorded them. The scope breakdown separates C#, Razor, generated/build-output paths, and other sources. It is a path-based classification: `bin`/`obj` identifies build output, so generated files elsewhere may appear under their source language. Collector coverage and C# declaration counts cover different scopes.

Identical coverage attachment contents are counted once, with their provenance retained. Reports are combined when their reported module/source/method/line/branch identities are compatible. Coverage is a union; line hits use the maximum reported count. Binary checksums are not supplied by these reports. Individual collection scopes remain inspectable; incompatible or ambiguous overlaps keep combined coverage unavailable.

### Formatting

Formatting defaults to **Changed files**, groups findings by rule and file, and hides line-ending findings with an explicit hidden count. Include line endings or switch to **All files** or **Changed lines** as needed. A finding on a changed file or line is not proof that the branch introduced it.

Formatting uses the formatter's default project configuration; the run's build configuration applies to tests and indexing. Discovered configuration files are labelled **candidates**, not proof that each file governed the result. Discovery follows ancestor directories of the target and files with findings instead of scanning unrelated package trees.

### Saved runs and exports

**Runs** reopens saved evidence. Historical views use captured source, not today's working files.

- **Markdown summary**: a compact comparison and verification summary with availability, limitations, test failures, and formatting information.
- **Full JSON evidence**: declarations, source snapshots, diffs, relationships, coverage details, diagnostics, and captured tool output for deeper inspection or agent use.

### Temporary-file cleanup

Completed and failed runs remove their temporary baseline checkout, including restored packages, archive, and intermediate source indexes after saving evidence. Saved source, review history, and verification reports remain available.

Use **Runs → Clean temporary files** to reclaim these files from earlier completed attempts or retry a cleanup failure. The action applies to the selected repository and cannot run while another process owns collection. Failures remain visible in the affected run. Unresolved attempts are skipped until the existing recovery flow confirms external processes have stopped. There is no automatic deletion of saved review history.

## Evidence limits and known limitations

- File and line changes cover the Git repository comparison. Type/member counts and graph nodes cover indexed C# source in the selected target's loaded projects. Razor markup, source-generator output, and SDK test entry points are outside the declaration scope.
- Changed-member counts exclude whitespace/comment-only edits. A theory is one test definition even when it produces multiple executions. Test-node labels follow recorded test definitions, not class-name conventions.
- Missing references, compilation/generator failures, excluded external or symlinked source, or an unavailable baseline can make indexing partial or unavailable. Recognised package-audit workspace diagnostics remain visible without independently invalidating the index; they are not fixed by that classification.
- Unknown, unavailable, disabled, and not-applicable results are distinct from zero findings. With an incomplete index, the changed-only graph may omit types whose change status could not be determined; turn the filter off to explore available evidence.
- Static relationships do not establish runtime dispatch, resolved dependency-injection bindings, or individual test-to-member coverage. Coverage records execution, not assertion quality or UI correctness.
- Source changes during collection flag consistency problems. Coverage overlays and branch coverage ranges require established source consistency. Older snapshots without richer coverage or captured source cannot supply those missing details.
- The current test profile is xUnit v2/VSTest/Coverlet. Other runners, test frameworks, and non-C# declaration indexing are outside the validated scope.
- Uncommitted comparisons, migration from legacy reports, manual report import, mutation testing with Stryker, and a direct agent API are outside this beta.
- CI checks Windows, Linux, and macOS, but does not establish every interactive scenario. See [validation scope](development.md#development-and-validation).

## Cancellation and recovery

Once an active run exists, **Cancel run** abandons the attempt. CoverScope stops its owned external process group and discards completed and partial analysis, raw reports, captured output, source snapshots, and derived data. The remaining record contains run metadata and Cancelled status; cancelled exports contain no analysis. Initial preparation checks occur before the run record exists and do not offer the active-run cancel control.

Process execution uses an owned Unix session/process group or Windows Job Object. Reusable build servers are disabled for owned invocations. Cancellation does not undo external side effects already caused by tests, and tools that deliberately detach from the owned process group cannot be treated as ordinary managed children.

If stopping, cancellation cleanup, or persistence cannot be confirmed, the condition remains visible and conflicting collection is blocked. After an interrupted application process, history offers recovery after the reviewer confirms that external work has stopped. Recovery discards the interrupted analysis; only acknowledge it after checking the remaining processes.

## Local data

Run records, settings, and analysis are stored beneath the platform's local application-data directory in `CoverScope/reviews-v2`, keyed by repository/worktree path. They are outside the reviewed repository. Saved evidence and exports can contain source code, test output, and local paths; review that content before sharing it.

This version does not import or migrate earlier `.coverscope/reports` runs. Those files are left untouched. Earlier snapshots within the current workflow remain readable with whatever evidence they originally recorded.

## Reporting problems

Open an issue in the repository with the CoverScope version (`--version`), OS, SDK version (`dotnet --info`), selected target type and run settings, reproduction steps, and expected/actual behaviour. Include the exact error and a Markdown summary when useful. Share full JSON evidence or source-containing screenshots only after checking their contents; a minimal reproduction is preferable when the original repository is private.
