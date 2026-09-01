namespace SupercompensationApp.Tests;

using System.Globalization;
using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// The export used to be string interpolation inside a .razor event handler, which
/// formats a double with CultureInfo.CurrentCulture. Blazor WebAssembly takes its
/// culture from the browser and this application ships &lt;html lang="pl"&gt;, so the
/// likely current culture is pl-PL — whose decimal separator is a comma.
///
/// These tests run the exporter under three cultures. Nothing else in this repository
/// could have caught the defect: it is invisible in a diff, invisible under en-US, and
/// the resulting file opens in every spreadsheet without an error.
/// </summary>
public class CsvExporterTests
{
    /// <summary>
    /// pl-PL is the app's own audience, de-DE is a second comma-decimal culture so no
    /// assertion can accidentally depend on Polish specifically, and en-US is the
    /// point-decimal control.
    /// </summary>
    private static readonly string[] Cultures = ["pl-PL", "de-DE", "en-US"];

    private static List<SprintDataPoint> Sample() =>
    [
        new()
        {
            Day = 1, SprintNumber = 1, DayInSprint = 1, Phase = "Fatigue",
            Baseline = 656.25, Deviation = -0.5, Performance = 655.75, TeamWeight = 6.35,
        },
        new()
        {
            Day = 2, SprintNumber = 1, DayInSprint = 2, Phase = "Supercompensation",
            Baseline = 656.25, Deviation = 114.3, Performance = 770.55, TeamWeight = 6.35,
        },
    ];

    /// <summary>
    /// Runs an action with CurrentCulture forced, and always puts it back. Culture is
    /// ambient state and xUnit runs test classes in parallel, so leaking it would make
    /// an unrelated test fail somewhere else.
    /// </summary>
    private static T UnderCulture<T>(string culture, Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void TheCultureSwitchInTheseTestsActuallyDoesSomething()
    {
        // Guards the test harness rather than the code under test, and it is not
        // ceremony. If the runtime were built without ICU — or with
        // InvariantGlobalization — then new CultureInfo("pl-PL") silently yields
        // invariant behaviour, every assertion below would pass, and the suite would
        // report that a culture bug is fixed while never having exercised a second
        // culture at all. A check that cannot fail is not a check.
        var polish = UnderCulture("pl-PL", () => 1.5.ToString(CultureInfo.CurrentCulture));

        Assert.True(
            polish == "1,5",
            $"Under pl-PL a bare double should format with a decimal COMMA, but this " +
            $"runtime produced \"{polish}\". Globalization is not active here, so every " +
            $"other test in this class is passing vacuously — fix the runtime, do not " +
            $"relax this assertion.");
    }

    [Fact]
    public void TheFileIsIdenticalUnderEveryCulture()
    {
        var reference = UnderCulture("en-US", () => CsvExporter.ToCsv(Sample()));

        foreach (var culture in Cultures)
        {
            var actual = UnderCulture(culture, () => CsvExporter.ToCsv(Sample()));

            Assert.True(
                actual == reference,
                $"The exported CSV must not depend on the machine's culture, but under " +
                $"{culture} it differs from en-US.\n--- {culture} ---\n{actual}\n" +
                $"--- en-US ---\n{reference}");
        }
    }

    [Fact]
    public void EveryRowHasExactlyAsManyFieldsAsTheHeader()
    {
        // This is the assertion that would have caught the original defect. Under pl-PL
        // the three double fields each became two, so the header declared seven columns
        // and every row carried ten — and no spreadsheet complains about that.
        foreach (var culture in Cultures)
        {
            var csv = UnderCulture(culture, () => CsvExporter.ToCsv(Sample()));
            var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var headerFields = lines[0].Split(',').Length;

            Assert.True(
                headerFields == 7,
                $"[{culture}] The header must declare 7 columns; it declared {headerFields}.");

            for (var i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',').Length;
                Assert.True(
                    fields == headerFields,
                    $"[{culture}] Row {i} has {fields} fields against the header's " +
                    $"{headerFields}. A decimal comma in a comma-separated file splits " +
                    $"its own row, and the result opens without an error in every " +
                    $"spreadsheet.\nRow: {lines[i]}");
            }
        }
    }

    [Fact]
    public void DecimalsUseAPointEvenUnderACommaDecimalCulture()
    {
        var csv = UnderCulture("pl-PL", () => CsvExporter.ToCsv(Sample()));

        Assert.True(
            csv.Contains("656.25", StringComparison.Ordinal),
            $"[pl-PL] A decimal must be written with a point in a comma-separated file. " +
            $"Got:\n{csv}");
    }

    [Fact]
    public void RowsAreTerminatedWithCrLfAsRfc4180Requires()
    {
        var csv = CsvExporter.ToCsv(Sample());

        Assert.True(
            csv.EndsWith("\r\n", StringComparison.Ordinal),
            "RFC 4180 specifies CRLF, and it is what Excel expects.");

        var bareNewlines = csv.Replace("\r\n", string.Empty, StringComparison.Ordinal)
                              .Count(c => c == '\n');
        Assert.True(
            bareNewlines == 0,
            $"Found {bareNewlines} bare LF line endings; every row must end CRLF.");
    }

    [Fact]
    public void AFieldContainingASeparatorIsQuoted()
    {
        // Phase is a controlled vocabulary today, so this cannot happen yet. The test
        // exists because a CSV writer that cannot quote is one string change away from
        // the same class of defect this class was written to fix.
        var points = new List<SprintDataPoint>
        {
            new()
            {
                Day = 1, SprintNumber = 1, DayInSprint = 1,
                Phase = "Fatigue, deep", Baseline = 1.5, Deviation = 0.0, Performance = 1.5,
            },
        };

        var csv = CsvExporter.ToCsv(points);
        var row = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];

        Assert.True(
            row.Contains("\"Fatigue, deep\"", StringComparison.Ordinal),
            $"A field containing the separator must be quoted, or it splits its own row. " +
            $"Got: {row}");

        Assert.True(
            row.Split(',').Length == 8,
            $"Sanity check on the test itself: the quoted field is expected to make a " +
            $"naive split report 8 pieces for 7 columns, which is why a naive split is " +
            $"not how a CSV is read. Got {row.Split(',').Length}.");
    }

    [Fact]
    public void AnEmptyTableStillProducesItsHeader()
    {
        var csv = CsvExporter.ToCsv([]);

        Assert.True(
            csv == "Day,Sprint,DayInSprint,Phase,Baseline,Deviation,Performance\r\n",
            $"An export with no rows must still be a valid CSV with its header. Got:\n{csv}");
    }
}
