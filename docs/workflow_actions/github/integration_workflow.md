# Integration Workflow Guide (`.github/workflows/integration.yml`)

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
- `restore`: downloads and resolves NuGet dependencies.

### Step: Build
Command:
```bash
dotnet build --no-restore --configuration Release
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `build`: compiles solution/projects.
- `--no-restore`: skips dependency restore because restore was done already.
- `--configuration Release`: compiles with Release configuration.

### Step: Run integration tests
Command:
```bash
dotnet test tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj --configuration Release --verbosity normal --filter "Category=Integration"
```
Explanation:
- `dotnet`: invokes the .NET CLI.
- `test`: runs tests.
- `tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj`: targets the test project explicitly.
- `--configuration Release`: uses the same build configuration as the build step.
- `--verbosity normal`: standard output detail.
- `--filter "Category=Integration"`: runs only tests marked with integration category trait.

Related test annotation expectation:
```csharp
[Trait("Category", "Integration")]
```

## 2) When it is triggered

Defined triggers in `integration.yml`:
- `push` to branch:
  - `main`
- `pull_request` targeting branch:
  - `main`
- `workflow_dispatch`:
  - manual trigger from GitHub Actions UI or CLI.

Additional behavior:
- Job uses `environment: integration`, so repository environment protection rules (required reviewers, wait timers, etc.) can gate execution.

Practical trigger examples:
- Push to `main` => integration workflow runs.
- Open/update PR into `main` => integration workflow runs.
- Manually click **Run workflow** => integration workflow runs.

## 3) How to trigger it manually

### GitHub UI
1. Open repository on GitHub.
2. Go to **Actions**.
3. Select **Integration Tests** workflow.
4. Click **Run workflow**.
5. Choose branch/ref and run.
6. If environment protections are enabled for `integration`, approve when prompted.

### GitHub CLI
```bash
gh workflow run integration.yml --ref main
```

### Re-run existing run
From the workflow run page:
- Click **Re-run all jobs** (or re-run failed jobs).
