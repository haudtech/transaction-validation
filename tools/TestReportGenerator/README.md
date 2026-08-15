# TestReportGenerator

Purpose
- Convert a .trx integration test result file into a Markdown summary report.
- Preserve trait-based reporting columns even when trait metadata is not present in the TRX by rebuilding trait mappings from integration test source attributes.

Project files
- Program entry: Program.cs
- Project file: TestReportGenerator.csproj

Inputs
- Argument 1: path to source TRX file.
- Argument 2: path to output Markdown file.

Command usage
```bash
dotnet run --project tools/TestReportGenerator/TestReportGenerator.csproj -- \
  TestResults/integration/integration-tests.trx \
  TestResults/integration/integration-tests-summary.md
```

Output sections
- Integration Test Summary
- Overall
  - Total, Passed, Failed, Skipped, Pass Rate
- By Traits
  - Category, Feature, Total, Passed, Failed, Skipped, Pass Rate
- Integration Test Details
  - Full Description, Category, Feature, Outcome, Duration, Class, Method
- Failed Tests
  - Bullet list of failed test descriptions with trait labels

How trait resolution works
1. Read UnitTest and UnitTestResult nodes from TRX.
2. Build test definition map by testId.
3. Parse integration test source files under:
   - tests/TransactionValidation.Tests/Integration
4. Match xUnit Trait attributes to class + method keys.
5. Enrich each TRX result row with Category and Feature.

Supported trait format
- [Trait("Category", "Integration")]
- [Trait("Feature", "Security")]

Current parser assumptions
- Namespace uses file-scoped format with trailing semicolon.
- Test class declaration is public class (optional sealed).
- Test methods are public async Task or public void.
- Trait attributes appear above the target test method.

If test style changes (for example nested classes, different method signatures, or uncommon attribute layout), update regex patterns in Program.cs.

Task integration
- The VS Code task test:integration:trx runs dotnet test and then calls this tool automatically.
- Task file: .vscode/tasks.json

Troubleshooting
- Error: TRX file not found
  - Confirm the results directory and filename passed to argument 1.
- Empty or missing trait values
  - Verify integration tests contain Trait attributes for Category and Feature.
  - Confirm class and method names in TRX still match source definitions.
- Markdown not updated
  - Re-run the command and check output path write permissions.

Maintenance checklist
- Keep report columns stable for downstream CI parsers.
- Add new output sections only with backward-compatible headings when possible.
- When adding new test outcomes, update pass/fail/skipped bucketing rules.
