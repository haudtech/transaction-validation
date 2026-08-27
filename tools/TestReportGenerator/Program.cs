using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// This tool converts TRX results into a Markdown report with stable sections.
// Traits are rebuilt from test source attributes because TRX often omits them.

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/TestReportGenerator/TestReportGenerator.csproj -- <trxPath> <outputMarkdownPath>");
    return 1;
}

var trxPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(trxPath))
{
    Console.Error.WriteLine($"TRX file not found: {trxPath}");
    return 1;
}

var repoRoot = FindRepositoryRoot(Path.GetDirectoryName(trxPath) ?? Environment.CurrentDirectory);
var traitMap = BuildTraitMap(Path.Combine(repoRoot, "tests", "TransactionValidation.Tests", "Integration"));

var doc = XDocument.Load(trxPath);
var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

var definitions = doc
    .Descendants(ns + "UnitTest")
    .Select(unitTest =>
    {
        var testId = (string?)unitTest.Attribute("id");
        var testName = (string?)unitTest.Attribute("name") ?? string.Empty;
        var testMethod = unitTest.Element(ns + "TestMethod");
        var className = (string?)testMethod?.Attribute("className") ?? string.Empty;
        var methodName = (string?)testMethod?.Attribute("name") ?? string.Empty;

        return new TestDefinition(testId ?? string.Empty, testName, className, methodName);
    })
    .Where(x => !string.IsNullOrWhiteSpace(x.TestId))
    .ToDictionary(x => x.TestId, x => x, StringComparer.OrdinalIgnoreCase);

var results = doc
    .Descendants(ns + "UnitTestResult")
    .Select(result =>
    {
        var testId = (string?)result.Attribute("testId") ?? string.Empty;
        var outcome = (string?)result.Attribute("outcome") ?? "Unknown";
        var testName = (string?)result.Attribute("testName") ?? string.Empty;
        var duration = (string?)result.Attribute("duration") ?? string.Empty;

        definitions.TryGetValue(testId, out var definition);

        var traits = definition is null
            ? TestTraits.Empty
            : traitMap.TryGetValue((definition.ClassName, definition.MethodName), out var mapped)
                ? mapped
                : traitMap.TryGetValue((definition.ClassName, string.Empty), out var classMapped)
                    ? classMapped
                    : TestTraits.Empty;

        return new TestResultRow(
            testId,
            testName,
            outcome,
            duration,
            traits.Category,
            traits.Feature,
            definition?.ClassName ?? string.Empty,
            definition?.MethodName ?? string.Empty);
    })
    .ToList();

var grouped = results
    .GroupBy(r => new { r.Category, r.Feature })
    .OrderBy(g => g.Key.Category, StringComparer.OrdinalIgnoreCase)
    .ThenBy(g => g.Key.Feature, StringComparer.OrdinalIgnoreCase)
    .Select(g => new
    {
        Category = string.IsNullOrWhiteSpace(g.Key.Category) ? "(none)" : g.Key.Category,
        Feature = string.IsNullOrWhiteSpace(g.Key.Feature) ? "(none)" : g.Key.Feature,
        Total = g.Count(),
        Passed = g.Count(x => string.Equals(x.Outcome, "Passed", StringComparison.OrdinalIgnoreCase)),
        Failed = g.Count(x => string.Equals(x.Outcome, "Failed", StringComparison.OrdinalIgnoreCase)),
        Skipped = g.Count(x => string.Equals(x.Outcome, "NotExecuted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Outcome, "Skipped", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Outcome, "Inconclusive", StringComparison.OrdinalIgnoreCase))
    })
    .ToList();

var total = results.Count;
var passedTotal = results.Count(x => string.Equals(x.Outcome, "Passed", StringComparison.OrdinalIgnoreCase));
var failedTotal = results.Count(x => string.Equals(x.Outcome, "Failed", StringComparison.OrdinalIgnoreCase));
var skippedTotal = results.Count(x => string.Equals(x.Outcome, "NotExecuted", StringComparison.OrdinalIgnoreCase)
    || string.Equals(x.Outcome, "Skipped", StringComparison.OrdinalIgnoreCase)
    || string.Equals(x.Outcome, "Inconclusive", StringComparison.OrdinalIgnoreCase));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var sb = new StringBuilder();
sb.AppendLine("# Integration Test Summary");
sb.AppendLine();
sb.AppendLine($"- Source TRX: `{Path.GetRelativePath(repoRoot, trxPath)}`");
sb.AppendLine($"- Generated: `{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}`");
sb.AppendLine();
sb.AppendLine("## Overall");
sb.AppendLine();
sb.AppendLine("| Total | Passed | Failed | Skipped | Pass Rate |");
sb.AppendLine("|---:|---:|---:|---:|---:|");
sb.AppendLine($"| {total} | {passedTotal} | {failedTotal} | {skippedTotal} | {FormatRate(passedTotal, total)} |");
sb.AppendLine();
sb.AppendLine("## By Traits");
sb.AppendLine();
sb.AppendLine("| Category | Feature | Total | Passed | Failed | Skipped | Pass Rate |");
sb.AppendLine("|---|---|---:|---:|---:|---:|---:|");

foreach (var row in grouped)
{
    sb.AppendLine($"| {EscapePipe(row.Category)} | {EscapePipe(row.Feature)} | {row.Total} | {row.Passed} | {row.Failed} | {row.Skipped} | {FormatRate(row.Passed, row.Total)} |");
}

sb.AppendLine();
sb.AppendLine("## Integration Test Details");
sb.AppendLine();
sb.AppendLine("| Full Description | Category | Feature | Outcome | Duration | Class | Method |");
sb.AppendLine("|---|---|---|---|---:|---|---|");

foreach (var detail in results
    .OrderBy(x => x.Feature, StringComparer.OrdinalIgnoreCase)
    .ThenBy(x => x.TestName, StringComparer.OrdinalIgnoreCase))
{
    var category = string.IsNullOrWhiteSpace(detail.Category) ? "(none)" : detail.Category;
    var feature = string.IsNullOrWhiteSpace(detail.Feature) ? "(none)" : detail.Feature;
    var duration = string.IsNullOrWhiteSpace(detail.Duration) ? "-" : detail.Duration;
    var className = string.IsNullOrWhiteSpace(detail.ClassName) ? "-" : detail.ClassName;
    var methodName = string.IsNullOrWhiteSpace(detail.MethodName) ? "-" : detail.MethodName;

    sb.AppendLine($"| {EscapePipe(detail.TestName)} | {EscapePipe(category)} | {EscapePipe(feature)} | {EscapePipe(detail.Outcome)} | {EscapePipe(duration)} | {EscapePipe(className)} | {EscapePipe(methodName)} |");
}

sb.AppendLine();
sb.AppendLine("## Failed Tests");
sb.AppendLine();

var failedTests = results
    .Where(x => string.Equals(x.Outcome, "Failed", StringComparison.OrdinalIgnoreCase))
    .OrderBy(x => x.TestName, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (failedTests.Count == 0)
{
    sb.AppendLine("- None");
}
else
{
    foreach (var failed in failedTests)
    {
        sb.AppendLine($"- {failed.TestName} (Category={failed.Category}, Feature={failed.Feature})");
    }
}

File.WriteAllText(outputPath, sb.ToString());
Console.WriteLine($"Markdown report generated: {outputPath}");
return 0;

static string EscapePipe(string input) => input.Replace("|", "\\|");

static string FormatRate(int passed, int total)
{
    if (total <= 0)
    {
        return "0.00%";
    }

    var rate = (double)passed / total * 100d;
    return rate.ToString("0.00", CultureInfo.InvariantCulture) + "%";
}

static string FindRepositoryRoot(string startDirectory)
{
    var current = new DirectoryInfo(startDirectory);

    while (current is not null)
    {
        var sln = Path.Combine(current.FullName, "TransactionValidation.sln");
        if (File.Exists(sln))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static Dictionary<(string ClassName, string MethodName), TestTraits> BuildTraitMap(string integrationTestsRoot)
{
    // Parse integration test source to map [Trait("Category", ...)] and
    // [Trait("Feature", ...)] onto class+method keys used by TRX definitions.
    var map = new Dictionary<(string ClassName, string MethodName), TestTraits>();

    if (!Directory.Exists(integrationTestsRoot))
    {
        return map;
    }

    var namespaceRegex = new Regex("^\\s*namespace\\s+([A-Za-z0-9_.]+)\\s*;", RegexOptions.Compiled);
    var classRegex = new Regex("^\\s*public\\s+(?:sealed\\s+)?class\\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    var methodRegex = new Regex("^\\s*public\\s+async\\s+Task\\s+([A-Za-z0-9_]+)\\s*\\(|^\\s*public\\s+void\\s+([A-Za-z0-9_]+)\\s*\\(", RegexOptions.Compiled);
    var traitRegex = new Regex("\\[Trait\\(\\\"([^\\\"]+)\\\",\\s*\\\"([^\\\"]+)\\\"\\)\\]", RegexOptions.Compiled);

    foreach (var file in Directory.EnumerateFiles(integrationTestsRoot, "*.cs", SearchOption.AllDirectories))
    {
        string? @namespace = null;
        string? className = null;
        var pendingTraits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadLines(file))
        {
            var line = rawLine.Trim();

            var nsMatch = namespaceRegex.Match(line);
            if (nsMatch.Success)
            {
                @namespace = nsMatch.Groups[1].Value;
                continue;
            }

            var classMatch = classRegex.Match(line);
            if (classMatch.Success)
            {
                className = classMatch.Groups[1].Value;
                continue;
            }

            var traitMatch = traitRegex.Match(line);
            if (traitMatch.Success)
            {
                pendingTraits[traitMatch.Groups[1].Value] = traitMatch.Groups[2].Value;
                continue;
            }

            var methodMatch = methodRegex.Match(line);
            if (!methodMatch.Success)
            {
                continue;
            }

            var methodName = methodMatch.Groups[1].Success
                ? methodMatch.Groups[1].Value
                : methodMatch.Groups[2].Value;

            var fullClassName = string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(className)
                ? string.Empty
                : $"{@namespace}.{className}";

            if (!string.IsNullOrWhiteSpace(fullClassName))
            {
                pendingTraits.TryGetValue("Category", out var category);
                pendingTraits.TryGetValue("Feature", out var feature);

                map[(fullClassName, methodName)] = new TestTraits(
                    category ?? string.Empty,
                    feature ?? string.Empty);

                if (!map.ContainsKey((fullClassName, string.Empty)))
                {
                    map[(fullClassName, string.Empty)] = new TestTraits(
                        category ?? string.Empty,
                        feature ?? string.Empty);
                }
            }

            pendingTraits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    return map;
}

internal sealed record TestDefinition(string TestId, string TestName, string ClassName, string MethodName);
internal sealed record TestResultRow(string TestId, string TestName, string Outcome, string Duration, string Category, string Feature, string ClassName, string MethodName);
internal sealed record TestTraits(string Category, string Feature)
{
    public static readonly TestTraits Empty = new(string.Empty, string.Empty);
}
