# CI Workflow Guide (`.github/workflows/ci.yml`)

## 1) Command line-by-line explanation

### Step: Checkout
Configuration:
```yaml
uses: actions/checkout@v5
```
Explanation:
- Checks out repository source code into the GitHub runner workspace.
- `@v5` is the current major action version used in this repository.

### Step: Setup .NET SDK
Configuration:
```yaml
uses: actions/setup-dotnet@v5
with:
  dotnet-version: '8.0.x'
  cache: true
  cache-dependency-path: |
    **/*.csproj
    Directory.Packages.props
    global.json
```
Explanation:
- Installs .NET SDK 8.x for the workflow.
- Enables built-in NuGet cache (`cache: true`).
- Cache invalidation keys are based on project/package/sdk files listed in `cache-dependency-path`.

### Step: SDK info
Command:
```bash
dotnet --info
```
Explanation:
- Prints installed SDKs/runtimes and environment details for diagnostics.

### Step: Restore
Command:
```bash
dotnet restore
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `restore`: downloads and resolves NuGet dependencies for all projects in the solution.
- No extra flags here means standard restore behavior using project/solution defaults.

### Step: Build
Command:
```bash
dotnet build --no-restore --configuration Release
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `build`: compiles the solution/projects.
- `--no-restore`: skips restore because restore already happened in the previous step.
- `--configuration Release`: builds with `Release` configuration (optimized build profile).

### Step: Test (unit, with coverage)
Command:
```bash
dotnet test tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj --configuration Release --verbosity normal --filter "Category!=Integration&Category!=E2E" --settings ./coverage.unit.runsettings --results-directory ./coverage
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `test`: runs tests.
- `tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj`: targets the test project explicitly.
- `--configuration Release`: uses the same build configuration as the build step.
- `--verbosity normal`: shows standard test output detail.
- `--filter "Category!=Integration&Category!=E2E"`: runs only unit tests, excluding tests tagged as Integration (run by the separate Integration Tests workflow) and E2E (require Docker Compose services).
- `--settings ./coverage.unit.runsettings`: uses the same coverage settings file as the local `test:coverage:unit` task, which pins the XPlat Code Coverage collector output to Cobertura format.
- `--results-directory ./coverage`: writes results under `./coverage/` so the upload step can find the report.

### Step: Upload coverage to Codecov
Configuration:
```yaml
uses: codecov/codecov-action@v5
with:
  token: ${{ secrets.CODECOV_TOKEN }}
  files: ./coverage/**/coverage.cobertura.xml
  disable_search: true
  fail_ci_if_error: false
```
Explanation:
- Uploads the Cobertura report to Codecov, which computes project/patch coverage and posts `codecov/project` and `codecov/patch` status checks on the PR.
- `token`: the repository upload token (stored as the `CODECOV_TOKEN` secret). Without it Codecov can receive uploads but cannot post status checks back to GitHub.
- `files`: explicit report path.
- `disable_search: true`: prevents the uploader from sweeping up unrelated files (e.g. the `.runsettings` files) as extra reports.
- `fail_ci_if_error: false`: an upload failure never blocks CI; coverage gating is Codecov's job, not the build's.
- The coverage target (`80%` project, `3%` threshold) and exclusion scope are configured in `codecov.yml` at the repo root — kept in version control, not in the GitHub UI. The exclusions mirror the local report filters in `.vscode/tasks.json` (`test:coverage:unit:report`), so the Codecov percentage (~87.5%) matches the locally measured value.
- Prerequisites: the Codecov GitHub App must be installed on the repository for status checks to be posted; a token alone only enables uploads and PR comments.

### Step: Verify formatting
Command:
```bash
dotnet format TransactionValidation.sln --verify-no-changes --verbosity diagnostic
```
Explanation:
- `dotnet format`: runs code formatting checks.
- `TransactionValidation.sln`: scopes formatting checks to the solution.
- `--verify-no-changes`: fails the step if formatting changes would be required.
- `--verbosity diagnostic`: outputs detailed diagnostics when formatting checks fail.

## 2) When it is triggered

Defined triggers in `ci.yml`:
- `push` to branch:
  - `main`
- `pull_request` targeting branch:
  - `main`

Deliberately, `feature/**` pushes do **not** trigger CI: with a PR open, the push run would duplicate the `pull_request` run (same commit, same steps), doubling runner minutes and cluttering the PR check list. Pre-PR feedback is covered by the local `check` task (format verification, build, unit tests).

Practical trigger examples:
- Open/update a PR into `main` => CI runs (this is the required `build` check).
- Push directly to `main` => CI runs (post-merge validation).
- Push a commit to `feature/phase-2-core` with no PR => CI does not run; use the local `check` task instead.

Concurrency behavior:
- The workflow defines a concurrency group keyed on workflow name + PR number (or branch ref) with `cancel-in-progress: true`, so pushing a follow-up commit cancels the obsolete in-progress run instead of queuing a full duplicate.

## 3) How to trigger it manually

Current `ci.yml` does **not** include `workflow_dispatch`, so manual trigger is not available yet.

Options:
- Trigger indirectly by pushing a commit to `feature/**` or `main`.
- Trigger indirectly by creating/updating a PR to `main`.
- If manual trigger is required, add this to `on:`:

```yaml
workflow_dispatch:
```

After adding `workflow_dispatch`, you can run it from GitHub UI:
1. Open repository on GitHub.
2. Go to **Actions**.
3. Select **CI** workflow.
4. Click **Run workflow**.
5. Select branch and run.

Optional GitHub CLI command (after `workflow_dispatch` is added):
```bash
gh workflow run ci.yml --ref main
```
