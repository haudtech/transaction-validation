# Repository Configuration Reference

This document explains the root-level configuration files that govern the repository as a whole. These are not feature docs or runbooks; they are the machine-readable contract for the build, package, quality, and code-style system.

## Purpose

The files in the repository root define the baseline behavior used across all projects and workflows:

- which .NET SDK version is required
- common build defaults for SDK-style projects
- centralized package versions
- formatting and analyzer enforcement
- coverage scope and policy for Codecov
- the test coverage configuration used by local and CI tasks

## Root-level configuration files

| File | Purpose | Applied when | Typical consumer |
|---|---|---|---|
| [../../global.json](../../global.json) | Pins the required .NET SDK version and roll-forward behavior. | `dotnet restore`, `dotnet build`, `dotnet test`, and SDK selection in tools. | .NET CLI and CI agents |
| [../../Directory.Build.props](../../Directory.Build.props) | Defines repository-wide MSBuild defaults such as target framework, nullable, implicit usings, and deterministic builds. | Every build and restore for SDK-style projects. | MSBuild and all project builds |
| [../../Directory.Packages.props](../../Directory.Packages.props) | Centralizes package version definitions for the whole solution. | Restore and package resolution. | NuGet restore and all projects |
| [../../.editorconfig](../../.editorconfig) | Enforces coding style, indentation, newline rules, and analyzer severity. | During editing, formatting, and analyzer execution. | IDEs, formatters, and build analyzers |
| [../../.vscode/tasks.json](../../.vscode/tasks.json) | Registers the repo's common development, build, and test workflows as VS Code tasks. | Local development, debugging, and quality verification from the editor. | VS Code task runner and contributors |
| [../../.vscode/launch.json](../../.vscode/launch.json) | Defines debug profiles for the API, mock endpoints, and browser-driven local debugging flows. | Local debugging and inspection of the application runtime. | VS Code debugger |
| [../../.vscode/settings.json](../../.vscode/settings.json) | Stores workspace defaults that make the repo work predictably in VS Code, including the default solution selection. | Editor startup and workspace behavior. | VS Code and contributors |
| [../../coverage.unit.runsettings](../../coverage.unit.runsettings) | Defines the coverage collection settings for the unit-test run. | Local unit coverage and CI-aligned unit measurement tasks. | Coverlet / XPlat coverage |
| [../../coverage.integration.runsettings](../../coverage.integration.runsettings) | Defines coverage collection settings for the integration-host run. | Local integration coverage tasks. | Coverlet / XPlat coverage |
| [../../codecov.yml](../../codecov.yml) | Defines Codecov thresholds, ignored paths, and status policy. | CI upload and branch protection checks. | Codecov and GitHub checks |

## How and when these files matter

### .NET SDK and project baseline

`global.json`, `Directory.Build.props`, and `Directory.Packages.props` establish the repository baseline before work starts.

- `global.json` selects the SDK and ensures all contributors and CI jobs use the same .NET version.
- `Directory.Build.props` establishes common project defaults, so the same target framework and compiler behavior are used everywhere.
- `Directory.Packages.props` keeps dependency versions consistent across the solution and makes change control easier.

These files are part of the build contract. They should be changed only when the repository intentionally updates its SDK baseline, build conventions, or dependency strategy.

### Code style and quality rules

`.editorconfig` defines the formatting and analyzer expectations for C# code. It is the editor and tooling boundary for consistent style, whitespace, naming, and severity enforcement.

The file is not a feature doc and not a runtime configuration file. It is a repository-wide engineering standard that applies during authoring and analysis.

### Coverage configuration

The two runsettings files define the coverage collector behavior for different test scopes:

- unit coverage is focused on business and application behavior without integration or E2E boundaries
- integration coverage isolates the host and cross-component behavior

They are consumed by the repository test tasks and help keep coverage meaningful rather than mixing unrelated environment paths into one aggregate number.

### Codecov policy

`codecov.yml` is the repository-wide gating policy for external coverage status reporting. It defines:

- which files are excluded from the unit coverage gate
- the target threshold for project coverage
- the patch policy used by the status checks
- the status layout and required coverage conditions

This is not an operational runbook. It is a policy file that governs quality gate expectations and should be reviewed when the quality model changes.

### Workspace and local developer experience

The files under [.vscode](../../.vscode) are repository-scoped editor configuration rather than application runtime configuration. They are important because they provide a consistent local developer experience across machines and keep the command set discoverable from within VS Code.

- [.vscode/tasks.json](../../.vscode/tasks.json) captures the standard build, test, and coverage commands used by the repository.
- [.vscode/launch.json](../../.vscode/launch.json) defines debugger startup profiles for the API and mock endpoints, plus browser launches for local verification.
- [.vscode/settings.json](../../.vscode/settings.json) keeps workspace defaults aligned with the repo, such as selecting the solution file automatically.

These files should be treated as part of the repo's contributor ergonomics contract. They are intentionally lightweight and do not replace the actual build, test, and deployment logic defined elsewhere in the repository.

## Ownership boundary

These files are governed by repository standards, not by feature or operational documentation.

- Feature behavior belongs under [../features/README.md](../features/README.md).
- Runtime and operational execution belong under [../operations/README.md](../operations/README.md).
- This reference belongs under [README.md](README.md) as the repo-level configuration contract.

## Related governance documents

- [documentation_standards.md](documentation_standards.md)
- [repository_architecture_rules.md](repository_architecture_rules.md)
- [../features/testing-and-quality/README.md](../features/testing-and-quality/README.md)
- [../operations/testing/test/test_execution_and_coverage_guide.md](../operations/testing/test/test_execution_and_coverage_guide.md)
