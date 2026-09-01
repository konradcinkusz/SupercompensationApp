namespace SupercompensationApp.Services;

using System.Globalization;
using System.Text;
using SupercompensationApp.Models;

/// <summary>
/// Renders the day-by-day table as RFC 4180 CSV.
///
/// This lives here rather than inline in Pages/Chart.razor for one reason: an event
/// handler in a .razor file cannot be reached by a unit test, and the defect this class
/// exists to fix is one that only a test under a second culture can catch.
///
/// The defect: the export used string interpolation, which formats a double with
/// CultureInfo.CurrentCulture. Blazor WebAssembly takes its culture from the browser,
/// this application ships &lt;html lang="pl"&gt; and its entire UI is Polish, so the
/// overwhelmingly likely current culture is pl-PL — whose decimal separator is a COMMA.
/// A row that should read
///
///     1,1,1,Fatigue,656.25,0,656.25
///
/// was written as
///
///     1,1,1,Fatigue,656,25,0,656,25
///
/// Three numeric fields became six, the header declared seven columns and the rows
/// carried ten, and every spreadsheet opened it without complaint and misaligned the
/// data. A CSV's field separator and its decimal point are not negotiable per user.
/// </summary>
public static class CsvExporter
{
    /// <summary>
    /// RFC 4180 specifies CRLF, and it is what Excel expects.
    /// </summary>
    private const string LineEnding = "\r\n";

    private static readonly string[] Header =
    [
        "Day", "Sprint", "DayInSprint", "Phase", "Baseline", "Deviation", "Performance",
    ];

    public static string ToCsv(IReadOnlyList<SprintDataPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        // StringBuilder rather than `csv +=` in a loop, which is quadratic. The UI's own
        // maximums allow 20 sprints x 30 days, so this runs over up to 600 rows.
        var builder = new StringBuilder();

        builder.Append(string.Join(',', Header)).Append(LineEnding);

        foreach (var point in points)
        {
            builder
                .Append(Number(point.Day)).Append(',')
                .Append(Number(point.SprintNumber)).Append(',')
                .Append(Number(point.DayInSprint)).Append(',')
                .Append(Field(point.Phase)).Append(',')
                .Append(Number(point.Baseline)).Append(',')
                .Append(Number(point.Deviation)).Append(',')
                .Append(Number(point.Performance))
                .Append(LineEnding);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats a number for the file rather than for a reader. InvariantCulture is the
    /// point of this whole class.
    /// </summary>
    private static string Number(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Quotes a text field if it contains anything that would otherwise break the row.
    ///
    /// Phase is a controlled vocabulary today — Fatigue, Recovery, Supercompensation —
    /// so nothing here needs quoting yet. It is done anyway because a CSV writer that
    /// cannot quote a field is one string change away from the same class of bug this
    /// class was written to fix, and the escaping is four lines.
    /// </summary>
    private static string Field(string? value)
    {
        var text = value ?? string.Empty;

        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return text;
        }

        return string.Concat("\"", text.Replace("\"", "\"\"", StringComparison.Ordinal), "\"");
    }
}
