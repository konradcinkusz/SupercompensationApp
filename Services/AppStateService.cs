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
    private readonly IStateStore _store;

    /// <summary>
    /// Increments on every edit. A debounced save captures the value, waits, and only
    /// writes if nothing has superseded it. Blazor WebAssembly is single-threaded, so a
    /// plain counter is correct here and is a great deal easier to read than a
    /// CancellationTokenSource that has to be cancelled and disposed in the right order.
    /// </summary>
    private int _editSequence;

    /// <summary>
    /// The signature of the configuration and team that produced the current results.
    /// Null when nothing has been generated yet.
    /// </summary>
    private string? _resultsSignature;

    public AppStateService(SupercompensationService service, IStateStore store)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// How long an edit waits before being written. Settable so a test does not have to
    /// sleep for real time to exercise the debounce.
    /// </summary>
    public TimeSpan SaveDelay { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// True when a stored payload was found and could not be used, so the application is
    /// showing defaults instead. Surfaced in the UI: an empty team with no explanation is
    /// worse than a lost one with a sentence.
    /// </summary>
    public bool RestoreFailed { get; private set; }

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

    // ── Persistence ──────────────────────────────────────────────────────────

    /// <summary>
    /// Restores the configuration and team from browser storage.
    ///
    /// Called once from MainLayout, which wraps every page, so it runs before anything is
    /// shown regardless of which route the user landed on.
    /// </summary>
    public async Task LoadAsync()
    {
        var json = await _store.ReadAsync(StateSerializer.StorageKey);

        if (json is null)
        {
            // Nothing stored. A first visit, or storage is unavailable. Neither is a
            // failure and neither should say anything to the user.
            RestoreFailed = false;
            return;
        }

        if (StateSerializer.TryDeserialize(json, out var config, out var team))
        {
            Config = config;
            Team = team;
            RestoreFailed = false;
        }
        else
        {
            // Corrupt, hand-edited, or written by an older schema. Keep the defaults
            // already in place and say so rather than failing silently.
            RestoreFailed = true;
        }

        NotifyChanged();
    }

    /// <summary>
    /// Writes the current configuration and team immediately.
    /// </summary>
    public async Task SaveAsync()
    {
        await _store.WriteAsync(
            StateSerializer.StorageKey,
            StateSerializer.Serialize(Config, Team));
    }

    /// <summary>
    /// Call from a binding's @bind:after, or after a team change. Notifies subscribers
    /// and schedules a debounced write.
    ///
    /// Debounced rather than saved per keystroke because @bind:after fires on every
    /// committed edit across six numeric fields and a row per team member; and there is
    /// deliberately no Save button, because a persistence problem should not be solved by
    /// adding an affordance the UI does not otherwise have.
    /// </summary>
    public async Task OnEditedAsync()
    {
        NotifyChanged();

        // Interlocked/Volatile rather than a bare ++ and ==. Blazor WebAssembly is
        // single-threaded so this is not needed in production, but a Task.Delay
        // continuation resumes on a pool thread under a test runner, and a persistence
        // test that fails one run in fifty is worse than a slightly noisier line.
        var mine = Interlocked.Increment(ref _editSequence);

        if (SaveDelay > TimeSpan.Zero)
        {
            await Task.Delay(SaveDelay);
        }

        if (mine != Volatile.Read(ref _editSequence))
        {
            // A later edit arrived while this one was waiting; that one will write.
            return;
        }

        await SaveAsync();
    }

    public async Task AddMemberAsync()
    {
        AddMember();
        await SaveAsync();
    }

    public async Task RemoveMemberAsync(TeamMember member)
    {
        RemoveMember(member);
        await SaveAsync();
    }

    /// <summary>
    /// Restores the default team AND clears the stored copy. Without the second half the
    /// next reload would undo the reset, which is the kind of thing that reads as the
    /// application ignoring you.
    /// </summary>
    public async Task ResetTeamAsync()
    {
        ResetTeam();
        RestoreFailed = false;
        await _store.RemoveAsync(StateSerializer.StorageKey);
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
