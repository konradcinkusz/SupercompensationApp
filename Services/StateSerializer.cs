namespace SupercompensationApp.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using SupercompensationApp.Models;

/// <summary>
/// The persisted payload. Versioned on purpose — see StateSerializer.
/// </summary>
public sealed class PersistedState
{
    public int SchemaVersion { get; set; }

    public SprintConfiguration? Config { get; set; }

    public List<TeamMember>? Team { get; set; }
}

/// <summary>
/// Turns the configuration and team into a string and back.
///
/// Static and browser-free on purpose: this is where the interesting behaviour is, so it
/// is the part that has to be testable. IStateStore is the thin adapter around it.
///
/// What is NOT persisted: ChartDataSet and the table rows. They are derived, they are the
/// largest objects in the application (600 rows at the UI's maximums), and a stored copy
/// is a second source of truth that can disagree with the configuration that produced it
/// — which is the defect #12 had just finished making visible. They are regenerated.
/// </summary>
public static class StateSerializer
{
    /// <summary>
    /// Bumped whenever the stored shape changes incompatibly. A payload written by a
    /// different version is discarded rather than half-read: the alternative is a
    /// TeamMember with a field that no longer exists, deserialised into a partly-empty
    /// object, which fails somewhere far away from the cause.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public const string StorageKey = "supercompensation.state.v1";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // System.Text.Json writes numbers per the JSON grammar rather than per the
        // current culture, so a decimal point is guaranteed here in a way it was NOT
        // guaranteed in the CSV export before #10. The round-trip is asserted under
        // pl-PL and en-US anyway: the guarantee is worth having a test behind rather
        // than a comment.
        WriteIndented = false,
    };

    public static string Serialize(SprintConfiguration config, IReadOnlyList<TeamMember> team)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(team);

        return JsonSerializer.Serialize(
            new PersistedState
            {
                SchemaVersion = CurrentSchemaVersion,
                Config = config,
                Team = [.. team],
            },
            Options);
    }

    /// <summary>
    /// Reads a payload back, or reports that it could not.
    ///
    /// Fails soft in every case, and the caller shows the defaults instead. A stored blob
    /// written by an older shape of TeamMember must not throw on load: the user would get
    /// a blank application with no explanation, which is worse than losing the saved team.
    /// </summary>
    public static bool TryDeserialize(
        string? json,
        out SprintConfiguration config,
        out List<TeamMember> team)
    {
        config = new SprintConfiguration();
        team = TeamMember.GetDefaultTeam();

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        PersistedState? stored;
        try
        {
            stored = JsonSerializer.Deserialize<PersistedState>(json, Options);
        }
        catch (JsonException)
        {
            return false;
        }

        if (stored is null || stored.SchemaVersion != CurrentSchemaVersion)
        {
            return false;
        }

        if (stored.Config is null || stored.Team is null || stored.Team.Count == 0)
        {
            return false;
        }

        // A payload can be well-formed and still unusable. Anything written before #11
        // added validation — or edited by hand in devtools — could carry a SprintDuration
        // of 0, and loading it would put the application straight back into the NaN state
        // that issue removed.
        if (stored.Config.Validate().Count > 0)
        {
            return false;
        }

        // An id is what keys the team table rows (#13). A stored payload with duplicates
        // would make Blazor's diff match two rows to one element.
        var ids = stored.Team.Select(m => m.Id).ToList();
        if (ids.Exists(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            return false;
        }

        config = stored.Config;
        team = stored.Team;
        return true;
    }
}
