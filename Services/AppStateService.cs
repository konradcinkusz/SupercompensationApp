namespace SupercompensationApp.Services;

using System.Globalization;
using System.Text;
using SupercompensationApp.Models;

/// <summary>
/// The application's state: the sprint configuration, the team, and the last generated
/// results.
///
/// It replaces four `public static` properties on Pages/Index.razor. The comment on
/// those said "stored in a simple static holder so they persist across navigations",
/// and the motive was sound — state must survive navigation. The mechanism cost four
/// things:
///
///   1. A page component became a data dependency. Chart and Data reached into
///      `Index.LastChartData` and could not be rendered, tested or reused without it,
///      for reasons that have nothing to do with routing.
///   2. It was untestable. Static mutable state is shared across the whole process and
///      xUnit runs test classes in parallel, so any test touching Index.Config could
///      corrupt another.
///   3. It could not be persisted — see #14, which is blocked on this.
///   4. ResetTeam() reassigned the list while LastChartData still described the previous
///      team, and the two were silently out of step until the user pressed generate
///      again.
///
/// Registered as a singleton in Program.cs. Blazor WebAssembly is single-user and
/// SupercompensationService sets the precedent; AddScoped would behave identically here
/// but would say something untrue about lifetime.
/// </summary>
public class AppStateService
{
    private readonly SupercompensationService _service;

    /// <summary>
    /// The signature of the configuration and team that produced the current results.
    /// Null when nothing has been generated yet.
    /// </summary>
    private string? _resultsSignature;

    public AppStateService(SupercompensationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Raised when the state changes in a way another page should notice. A page
    /// rendered while state changes elsewhere has no other way to find out.
    /// </summary>
    public event Action? OnChange;

    public SprintConfiguration Config { get; private set; } = new();

    public List<TeamMember> Team { get; private set; } = TeamMember.GetDefaultTeam();

    public ChartDataSet? LastChartData { get; private set; }

    public List<SprintDataPoint>? LastTableData { get; private set; }

    public bool HasResults => LastChartData is not null && LastTableData is not null;

    /// <summary>
    /// True when results exist but the configuration or team has changed since they were
    /// generated, so the chart on screen describes inputs the user has already edited.
    ///
    /// This is item 4 above, made visible rather than left silent. It is computed by
    /// comparing signatures rather than by a flag the mutating code has to remember to
    /// set — a flag can be forgotten by the next person who adds a field, and the
    /// failure mode of forgetting is a chart that quietly describes the wrong team.
    /// </summary>
    public bool IsStale => HasResults && Signature() != _resultsSignature;

    /// <summary>
    /// The configuration problems, if any. Empty means the state can be generated from.
    /// </summary>
    public IReadOnlyList<string> Problems => Config.Validate();

    public double TeamWeight => _service.CalculateTeamWeight(Team);

    /// <summary>
    /// Regenerates the chart and table from the current configuration and team.
    /// Returns false, changing nothing, when the configuration is not usable.
    /// </summary>
    public bool Generate()
    {
        if (Problems.Count > 0)
        {
            return false;
        }

        LastChartData = _service.GenerateChartData(Config, Team);
        LastTableData = _service.GenerateTableData(Config, Team);
        _resultsSignature = Signature();

        NotifyChanged();
        return true;
    }

    public void AddMember()
    {
        Team.Add(new TeamMember
        {
            Name = $"Nowy {Team.Count + 1}",
            Role = "Developer",
            Weight = 1.0,
        });

        NotifyChanged();
    }

    /// <summary>
    /// A team of zero makes CalculateTeamWeight return its sentinel 1.0, which is a
    /// different model rather than an empty one — hence the floor of one member. The UI
    /// disables the button rather than ignoring the click; see #13.
    /// </summary>
    public bool CanRemoveMember => Team.Count > 1;

    public void RemoveMember(TeamMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (!CanRemoveMember)
        {
            return;
        }

        if (Team.Remove(member))
        {
            NotifyChanged();
        }
    }

    public void ResetTeam()
    {
        Team = TeamMember.GetDefaultTeam();
        NotifyChanged();
    }

    /// <summary>
    /// Call after editing Config or a TeamMember through a two-way binding, which
    /// cannot raise the event itself.
    /// </summary>
    public void NotifyChanged() => OnChange?.Invoke();

    /// <summary>
    /// Everything the results depend on, in one string.
    ///
    /// InvariantCulture throughout: this is compared against a value captured earlier in
    /// the same session, so a culture-dependent format would be consistent — but the
    /// same reasoning was true of the CSV export before #10, and a signature that
    /// changes meaning with the browser's locale is a trap waiting for the day it is
    /// persisted (#14).
    /// </summary>
    private string Signature()
    {
        var builder = new StringBuilder();
        var invariant = CultureInfo.InvariantCulture;

        builder
            .Append(Config.SprintDuration.ToString(invariant)).Append('|')
            .Append(Config.NumberOfSprints.ToString(invariant)).Append('|')
            .Append(Config.BaselineIncrement.ToString(invariant)).Append('|')
            .Append(Config.InitialBaseline.ToString(invariant)).Append('|')
            .Append(Config.FatigueDepth.ToString(invariant)).Append('|')
            .Append(Config.SupercompensationPeak.ToString(invariant)).Append("||");

        foreach (var member in Team)
        {
            builder
                .Append(member.Id).Append(':')
                .Append(member.Name).Append(':')
                .Append(member.Role).Append(':')
                .Append(member.Weight.ToString(invariant)).Append(';');
        }

        return builder.ToString();
    }
}
