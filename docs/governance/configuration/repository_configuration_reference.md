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

### Agenda

- [Root-level configuration files](#root-level-configuration-files)
- [How and when these files matter](#how-and-when-these-files-matter)
- [Required rules for repository configuration artifacts](#required-rules-for-repository-configuration-artifacts)
- [Ownership boundary](#ownership-boundary)
- [Related governance documents](#related-governance-documents)

## Root-level configuration files

| File | Purpose | Applied when | Typical consumer | Governing rule |
|---|---|---|---|---|
| [../../../global.json](../../../global.json) | Pins the required .NET SDK version and roll-forward behavior. | `dotnet restore`, `dotnet build`, `dotnet test`, and SDK selection in tools. | .NET CLI and CI agents | RCFG-BASELINE-01 |
| [../../../Directory.Build.props](../../../Directory.Build.props) | Defines repository-wide MSBuild defaults such as target framework, nullable, implicit usings, and deterministic builds. | Every build and restore for SDK-style projects. | MSBuild and all project builds | RCFG-BASELINE-01 |
| [../../../Directory.Packages.props](../../../Directory.Packages.props) | Centralizes package version definitions grouped by package responsibility. | Restore and package resolution. | NuGet restore and all projects | RCFG-BASELINE-01; RCFG-DEPENDENCY-01 |
| Project files (`**/*.csproj`) | Declare project package and project dependencies in distinct item groups. | Project evaluation, restore, and build. | MSBuild and NuGet | RCFG-DEPENDENCY-01 |
| [../../../docker-compose.yml](../../../docker-compose.yml) | Defines the local multi-service runtime topology used for development and Docker-backed E2E verification. | Local Compose startup and E2E workflows that require the repository service topology. | Docker Compose and contributors | RCFG-COMPOSE-01 |
| [../../../.editorconfig](../../../.editorconfig) | Defines coding style, indentation, newline rules, and analyzer severity. | During editing, explicit formatting checks, and build-time analyzer checks. | IDEs, `dotnet format`, and build analyzers | RCFG-STYLE-01 |
| [../../../.github/workflows](../../../.github/workflows) | Defines automated CI, integration, infrastructure, and application-delivery behavior. | GitHub workflow events and protected deployment environments. | GitHub Actions | RCFG-WORKFLOW-01; TXV-DEL-01/02/03 where applicable |
| [../../../.vscode/tasks.json](../../../.vscode/tasks.json) | Registers the repo's common development, build, and test workflows as VS Code tasks. | Local development, debugging, and quality verification from the editor. | VS Code task runner and contributors | RCFG-VSCODE-01 |
| [../../../.vscode/launch.json](../../../.vscode/launch.json) | Defines debug profiles for the API, mock endpoints, and browser-driven local debugging flows. | Local debugging and inspection of the application runtime. | VS Code debugger | CS-DEBUG-01; RCFG-VSCODE-01 |
| [../../../.vscode/settings.json](../../../.vscode/settings.json) | Stores workspace defaults that make the repo work predictably in VS Code, including the default solution selection. | Editor startup and workspace behavior. | VS Code and contributors | RCFG-VSCODE-01 |
| [../../../coverage.unit.runsettings](../../../coverage.unit.runsettings) | Defines the coverage collection settings for the unit-test run. | Local unit coverage and CI-aligned unit measurement tasks. | Coverlet / XPlat coverage | RCFG-COVER-UNIT-01 |
| [../../../coverage.integration.runsettings](../../../coverage.integration.runsettings) | Defines coverage collection settings for the integration-host run. | Local integration coverage tasks. | Coverlet / XPlat coverage | RCFG-COVER-INT-01 |
| [../../../codecov.yml](../../../codecov.yml) | Defines Codecov thresholds, ignored paths, and status policy. | CI upload and branch protection checks. | Codecov and GitHub checks | RCFG-CODECOV-01 |

## How and when these files matter

### .NET SDK and project baseline

`global.json`, `Directory.Build.props`, and `Directory.Packages.props` establish the repository baseline before work starts.

- `global.json` selects the SDK and ensures all contributors and CI jobs use the same .NET version.
- `Directory.Build.props` establishes common project defaults, so the same target framework and compiler behavior are used everywhere.
- `Directory.Packages.props` keeps dependency versions consistent across the solution and makes change control easier.

These files are part of the build contract. They should be changed only when the repository intentionally updates its SDK baseline, build conventions, or dependency strategy.

### Code style and quality rules

`.editorconfig` defines the formatting and analyzer expectations for C# code. It is the editor and tooling boundary for consistent style, whitespace, naming, and severity enforcement. `EnforceCodeStyleInBuild` enables build-time code-style analyzers, and `CodeAnalysisTreatWarningsAsErrors` promotes analyzer warnings to build errors without changing ordinary compiler-warning handling. Formatting preferences are checked separately by the repository's `dotnet format` verification task and CI step; `dotnet build` does not run a whitespace-format check. Suggestion-level preferences remain advisory.

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

The files under [.vscode](../../../.vscode) are repository-scoped editor configuration rather than application runtime configuration. They are important because they provide a consistent local developer experience across machines and keep the command set discoverable from within VS Code.

- [.vscode/tasks.json](../../../.vscode/tasks.json) captures the standard build, test, and coverage commands used by the repository.
- [.vscode/launch.json](../../../.vscode/launch.json) defines debugger startup profiles for the API and mock endpoints, plus browser launches for local verification.
- [.vscode/settings.json](../../../.vscode/settings.json) keeps workspace defaults aligned with the repo, such as selecting the solution file automatically.

These files should be treated as part of the repo's contributor ergonomics contract. They are intentionally lightweight and do not replace the actual build, test, and deployment logic defined elsewhere in the repository.

## Required rules for repository configuration artifacts

The rules below govern the listed machine-readable files in this repository. They are a reusable governance shape, but exact SDK versions, build defaults, thresholds, services, task labels, and cloud providers are repository decisions. When a configuration file changes, apply its mapped rule, verify the consuming workflow, and record any approved exception through the [feature-delivery exception process](../feature_delivery_workflow.md#exception-record).

### RCFG-BASELINE-01 — Keep SDK, build defaults, and package versions centrally governed

- **Applicability** - an SDK-style .NET repository uses `global.json`, `Directory.Build.props`, or central package management.
- **Requirement** - `global.json` MUST pin the tested SDK baseline and declare intentional roll-forward behavior. Shared MSBuild defaults MUST live in `Directory.Build.props`; project-level overrides require a documented technical reason. Package versions MUST be centrally managed in `Directory.Packages.props`; project `PackageReference` entries MUST NOT repeat versions unless a documented exception is required by tooling.
- **Rationale** - contributors and CI should restore/build against a reproducible toolchain, compiler policy, and dependency graph.
- **Approved pattern** - keep common defaults/version declarations at repository scope and keep project files focused on package identity and project-specific properties.
- **Configuration example**:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="Example.Library" Version="1.2.3" />

<!-- project file -->
<PackageReference Include="Example.Library" />
```

- **Verification** - confirm `dotnet --version` selects the pinned SDK, then run restore/build; review project files for duplicated package versions and unexplained overrides.
- **Exception process** - document the affected project, reason, scope, approver, and future review condition; update CI/local tooling consistently.

### RCFG-DEPENDENCY-01 — Group package and project references by responsibility

- **Applicability** - a .NET project declares `PackageReference` or `ProjectReference` items, or the repository centrally manages package versions in `Directory.Packages.props`.
- **Requirement** - project files MUST keep `PackageReference` and `ProjectReference` items in separate `ItemGroup` elements; an item group MUST NOT mix the two reference kinds. `Directory.Packages.props` MUST organize package versions into clearly labeled responsibility/domain groups using XML comments. Categories SHOULD reflect the repository's actual dependencies (for example, API/hosting, validation, observability, messaging/state, and testing), rather than forcing packages into unsuitable generic labels.
- **Rationale** - distinct dependency groups make project composition and package ownership easier to scan and review while retaining central version governance.
- **Approved pattern** - put each reference kind in its own item group, and add a short comment before each responsibility group in the central package-version file.
- **Configuration example**:

```xml
<!-- Project.csproj -->
<ItemGroup>
	<!-- Runtime package dependencies -->
	<PackageReference Include="Example.Library" />
</ItemGroup>

<ItemGroup>
	<!-- Internal project dependencies -->
	<ProjectReference Include="../Example.Core/Example.Core.csproj" />
</ItemGroup>

<!-- Directory.Packages.props -->
<!-- Observability -->
<PackageVersion Include="Example.Telemetry" Version="1.2.3" />
```

- **Verification** - inspect each changed project file to confirm package and project references occupy separate item groups; confirm `Directory.Packages.props` retains one centrally governed version per package and that each entry appears under an appropriate responsibility comment; run `dotnet restore` or `dotnet build` to validate MSBuild/NuGet evaluation.
- **Exception process** - if a project or package cannot fit this grouping without obscuring its role, document the affected file and rationale in review; do not duplicate or locally override centrally managed package versions as a workaround.

### RCFG-COMPOSE-01 — Use Compose as the local multi-service E2E topology

- **Applicability** - a local E2E scenario depends on repository-owned services or their container/network configuration.
- **Requirement** - `docker-compose.yml` MUST describe the required local service topology. E2E tests for that topology MUST prefer the Compose stack and exercise real service-to-service networking and dependencies relevant to the assertion, rather than replacing them with mocks. Compose defaults and examples MUST NOT contain production secrets.
- **Rationale** - containerized E2E verifies packaging, environment wiring, service discovery, and real protocols that an in-memory host cannot prove.
- **Approved pattern** - launch the required API, Mock, broker, and state services through the repository Compose workflow; wait for readiness and run the E2E category. Use an external environment only for cloud-specific behavior unavailable locally and document its prerequisites.
- **Configuration example**:

```yaml
services:
	api:
		depends_on:
			redis:
				condition: service_healthy
			broker:
				condition: service_healthy
```

- **Verification** - validate the resolved Compose model, run the relevant E2E scenario against the stack, and check that teardown removes only the resources intended by the task.
- **Exception process** - document why the behavior cannot be represented locally, identify the external environment and owners, and define cleanup and reproducibility controls.

### RCFG-COVER-UNIT-01 — Keep unit coverage collection isolated

- **Applicability** - a repository collects unit-test coverage with Coverlet or an equivalent collector.
- **Requirement** - `coverage.unit.runsettings` MUST configure coverage collection/format only for the unit coverage run. Unit test selection and result directories MUST be controlled by the test task/workflow, and unit coverage MUST exclude integration/E2E categories when those boundaries are collected separately.
- **Rationale** - unit coverage should measure fast, isolated behavior and remain comparable over time.
- **Approved pattern** - collect Cobertura in the unit coverage task using the unit runsettings and a unit-only test filter.
- **Configuration example**:

```xml
<DataCollector friendlyName="XPlat Code Coverage">
	<Configuration><Format>cobertura</Format></Configuration>
</DataCollector>
```

- **Verification** - inspect the unit task's filter/settings/result directory and confirm the generated Cobertura input contains only the intended unit run.
- **Exception process** - document changes to unit/integration boundaries and keep local and CI measurement semantics aligned.

### RCFG-COVER-INT-01 — Keep integration coverage isolated from unit coverage

- **Applicability** - a repository collects host/integration coverage separately from unit coverage.
- **Requirement** - `coverage.integration.runsettings` MUST remain a separate settings input for the Integration category and MUST write to a result location separate from unit coverage. Its collector settings MAY currently mirror the unit settings; the file and run remain distinct so future collector changes do not merge test scopes. Integration filters and output paths MUST be owned by the test task/workflow, not inferred from a report file's contents.
- **Rationale** - separate inputs preserve boundary-level signal and make combined reports reproducible.
- **Approved pattern** - run the Integration filter with integration runsettings and a dedicated results directory, then merge reports only in the reporting step.
- **Configuration example** - use the Cobertura collector shape shown in RCFG-COVER-UNIT-01, consumed by the integration-only test task.
- **Verification** - inspect the integration task's filter/settings/output path and verify its Cobertura files are distinct from unit results.
- **Exception process** - document any change to category boundaries, output paths, or report merging and update local/CI consumers together.

### RCFG-CODECOV-01 — Keep coverage status policy version-controlled

- **Applicability** - a repository uses Codecov or another external coverage status provider.
- **Requirement** - coverage targets, exclusions, and patch/project status behavior MUST be source-controlled in `codecov.yml` (or the chosen provider's config). The configured scope MUST match the coverage artifact uploaded by CI. The provider's check is a merge gate only when repository branch protection requires it.
- **Rationale** - reviewable policy and explicit enforcement prevent silent divergence between local reports, uploaded data, and merge requirements.
- **Approved pattern** - keep provider thresholds/exclusions in the config file, upload the intended report explicitly, and document where the status is enforced.
- **Configuration example**:

```yaml
coverage:
	status:
		project:
			default:
				target: 80%
```

- **Verification** - review the provider config, CI upload paths, and branch-protection requirements together.
- **Exception process** - coverage threshold/exclusion changes require a reviewed rationale and must not be hidden in provider UI-only settings.

### RCFG-STYLE-01 — Use `.editorconfig` as the repository style authority

- **Applicability** - contributors use IDEs, formatters, or analyzers that honor EditorConfig.
- **Requirement** - shared formatting, naming, and analyzer severities MUST be declared in the root `.editorconfig`. Suppressions or project-specific overrides MUST be narrow, justified, and reviewable. Build-blocking analyzer rules MUST be validated by `dotnet build`; formatting preferences MUST be checked by the repository's explicit format-verification workflow; advisory preferences MAY remain suggestion-level.
- **Rationale** - one machine-readable style authority reduces tool and contributor drift.
- **Approved pattern** - set common editor rules at the root and enable `EnforceCodeStyleInBuild` and `CodeAnalysisTreatWarningsAsErrors` for analyzer enforcement during builds. Run `dotnet format --verify-no-changes` as a separate formatting verification step.
- **Configuration example**:

```ini
[*.cs]
end_of_line = lf
insert_final_newline = true
dotnet_diagnostic.CA1848.severity = error
```

- **Verification** - run `dotnet build` and the configured format verification task separately; confirm analyzer diagnostics fail the build at the configured severity and formatting differences fail format verification.
- **Exception process** - justify any severity downgrade or scoped suppression in review and preserve coverage of equivalent checks.

### RCFG-WORKFLOW-01 — Keep GitHub workflow responsibilities explicit and least-privileged

- **Applicability** - `.github/workflows` automates CI, tests, infrastructure, or deployment.
- **Requirement** - workflows MUST declare narrow triggers, permissions, and environments for their responsibilities. CI validation, integration checks, infrastructure changes, and application deployment SHOULD remain separate when they have different dependencies, permissions, or blast radius. Workflow files are the execution authority; operations docs explain how to invoke/support them.
- **Rationale** - explicit boundaries reduce accidental privileged execution and make checks/release behavior reviewable.
- **Approved pattern** - use separate jobs/workflows for build/test, infrastructure preview/apply, and application rollout as their trust boundaries require; use federated identity instead of long-lived cloud credentials where supported.
- **Configuration example** - see the least-privilege OIDC and environment examples in the [Infrastructure and Delivery feature](../../features/infrastructure-and-delivery/README.md).
- **Verification** - review trigger/path filters, permissions, secret references, environment approvals, and the workflow's actual status as a required check.
- **Exception process** - changes that broaden permissions, triggers, or protected-environment access require security/platform-owner review and documented compensating controls.

### RCFG-VSCODE-01 — Keep workspace launch, settings, and tasks portable

- **Applicability** - a repository commits `.vscode/launch.json`, `.vscode/settings.json`, or `.vscode/tasks.json` for shared contributor workflows.
- **Requirement** - committed workspace settings MUST be portable across contributor machines and MUST NOT include user-specific paths, credentials, or secrets. Launch profiles MUST use safe development defaults. Tasks MUST expose repository-supported build/test/quality commands and preserve required sequencing; GitHub workflows remain the CI execution authority.
- **Rationale** - editor integration should improve discoverability without becoming a hidden runtime configuration or a competing build system.
- **Approved pattern** - launch projects by workspace-relative paths with Development-only non-secret values; use `${workspaceFolder}` in tasks/settings; delegate tests/build to the same CLI commands used by repository quality policy.
- **Configuration example** - see the repository [debug profiles](../../../.vscode/launch.json), [workspace settings](../../../.vscode/settings.json), and [task definitions](../../../.vscode/tasks.json).
- **Verification** - open each shared launch profile/task in VS Code, verify workspace-relative paths and command parity, and scan committed editor files for secrets or machine-specific values.
- **Exception process** - keep personal editor preferences in user settings; document any intentional repository-only override and its contributor impact.

## Ownership boundary

These files are governed by repository standards, not by feature or operational documentation.

- Feature behavior belongs under [../../features/README.md](../../features/README.md).
- Runtime and operational execution belong under [../../operations/README.md](../../operations/README.md).
- This reference belongs under [../README.md](../README.md) as the repo-level configuration contract.

## Related governance documents

- [documentation_standards.md](../documentation/documentation_standards.md)
- [repository_architecture_rules.md](../coding/repository_architecture_rules.md)
- [../../features/testing-and-quality/README.md](../../features/testing-and-quality/README.md)
- [../../operations/testing/test/test_execution_and_coverage_guide.md](../../operations/testing/test/test_execution_and_coverage_guide.md)
