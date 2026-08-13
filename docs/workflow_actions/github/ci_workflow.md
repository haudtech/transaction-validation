# CI Workflow Guide (`.github/workflows/ci.yml`)

## 1) Command line-by-line explanation

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

### Step: Test (unit only)
Command:
```bash
dotnet test --no-build --verbosity normal --filter "Category!=Integration"
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `test`: runs tests.
- `--no-build`: skips build because build already happened in the previous step.
- `--verbosity normal`: shows standard test output detail.
- `--filter "Category!=Integration"`: excludes tests tagged as Integration and runs non-integration tests (unit/default tests).

### Step: Install dotnet-format
Commands:
```bash
dotnet tool install -g dotnet-format || true
echo "$HOME/.dotnet/tools" >> $GITHUB_PATH
```
Explanation:
- `dotnet tool install -g dotnet-format`: installs `dotnet-format` as a global tool in the runner.
- `|| true`: prevents workflow failure if the tool is already installed.
- `echo "$HOME/.dotnet/tools" >> $GITHUB_PATH`: appends global tool location to `PATH` for subsequent steps.

### Step: Verify formatting
Command:
```bash
dotnet format --verify-no-changes
```
Explanation:
- `dotnet format`: runs code formatting checks.
- `--verify-no-changes`: fails the step if formatting changes would be required.

## 2) When it is triggered

Defined triggers in `ci.yml`:
- `push` to branches:
  - `main`
  - `feature/**` (any branch under `feature/`)
- `pull_request` targeting branch:
  - `main`

Practical trigger examples:
- Push a commit to `feature/phase-2-core` => CI runs.
- Open/update PR from feature branch into `main` => CI runs.
- Push directly to `main` => CI runs.

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
