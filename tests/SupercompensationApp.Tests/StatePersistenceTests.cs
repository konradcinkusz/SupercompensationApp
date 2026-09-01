namespace SupercompensationApp.Tests;

using System.Globalization;
using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// Everything the user types was lost on refresh: the state lives in memory, and a Blazor
/// WebAssembly reload reconstructs the application, so GetDefaultTeam() runs again and the
/// seven default members come back.
///
/// That is a real cost rather than a nicety. The configuration screen is "edycja zespołu
/// (jak Excel)" — seven rows of name, role and weight — and there is no server and no
/// account, so if the browser does not remember, nothing does.
/// </summary>
public class StatePersistenceTests
{
    private static (AppStateService State, InMemoryStateStore Store) NewState()
    {
        var store = new InMemoryStateStore();
        var state = new AppStateService(new SupercompensationService(), store)
        {
            // Tests should not sleep for real time to exercise a debounce.
            SaveDelay = TimeSpan.Zero,
        };
        return (state, store);
    }

    // ── The round trip ───────────────────────────────────────────────────────

    [Fact]
    public async Task ConfigurationAndTeamSurviveAReload()
    {
        var (state, store) = NewState();
        state.Config.SprintDuration = 14;
        state.Config.FatigueDepth = 31.0;
        state.Team[0].Name = "Ada";
        state.Team[0].Weight = 1.25;
        await state.SaveAsync();

        // A reload is a brand new service against the same storage.
        var reloaded = new AppStateService(new SupercompensationService(), store);
        await reloaded.LoadAsync();

        Assert.True(reloaded.Config.SprintDuration == 14, $"Got {reloaded.Config.SprintDuration}.");
        Assert.True(reloaded.Config.FatigueDepth == 31.0, $"Got {reloaded.Config.FatigueDepth}.");
        Assert.True(reloaded.Team[0].Name == "Ada", $"Got {reloaded.Team[0].Name}.");
        Assert.True(reloaded.Team[0].Weight == 1.25, $"Got {reloaded.Team[0].Weight}.");
        Assert.True(!reloaded.RestoreFailed, "A clean payload must not report a failure.");
    }

    [Fact]
    public void TheSerialisedFormDoesNotDependOnTheMachinesCulture()
    {
        // The same reasoning as #10, one layer down. System.Text.Json writes numbers per
        // the JSON grammar rather than per the current culture, so this should hold — and
        // that guarantee is worth a test rather than a comment, because the CSV export
        // carried the same assumption and it was false there.
        static string Under(string culture, Func<string> action)
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

        var config = new SprintConfiguration { BaselineIncrement = 12.5, FatigueDepth = 33.75 };
        var team = TeamMember.GetDefaultTeam();

        var polish = Under("pl-PL", () => StateSerializer.Serialize(config, team));
        var american = Under("en-US", () => StateSerializer.Serialize(config, team));

        Assert.True(
            polish == american,
            $"The stored payload must be byte-identical across cultures.\n" +
            $"pl-PL: {polish}\nen-US: {american}");

        Assert.True(
            polish.Contains("12.5", StringComparison.Ordinal),
            $"and decimals must use a point: {polish}");
    }

    // ── Failing soft ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ACorruptPayloadLoadsDefaultsAndSaysSo()
    {
        var (state, store) = NewState();
        store.Seed(StateSerializer.StorageKey, "{ this is not json");

        await state.LoadAsync();

        Assert.True(state.Team.Count == TeamMember.GetDefaultTeam().Count, "Defaults restored.");
        Assert.True(
            state.RestoreFailed,
            "A corrupt payload must be visible to the user. A blank team with no " +
            "explanation is worse than a lost one with a sentence.");
    }

    [Fact]
    public void APayloadFromAnotherSchemaVersionIsDiscarded()
    {
        var json = $$"""
            {"SchemaVersion":{{StateSerializer.CurrentSchemaVersion + 1}},"Config":{"SprintDuration":9},"Team":[]}
            """;

        var ok = StateSerializer.TryDeserialize(json, out var config, out var team);

        Assert.True(!ok, "A different schema version must not be read.");
        Assert.True(config.SprintDuration == 10, "Defaults are returned instead.");
        Assert.True(team.Count > 0, "including the default team.");
    }

    [Fact]
    public void AStoredConfigurationOutsideTheAllowedRangeIsRejected()
    {
        // Anything written before #11 added validation, or edited by hand in devtools,
        // could carry SprintDuration = 0 — and loading it would put the application
        // straight back into the NaN state that issue removed.
        var config = new SprintConfiguration();
        var json = StateSerializer.Serialize(config, TeamMember.GetDefaultTeam())
            .Replace("\"SprintDuration\":10", "\"SprintDuration\":0", StringComparison.Ordinal);

        var ok = StateSerializer.TryDeserialize(json, out var loaded, out _);

        Assert.True(!ok, "A payload that fails validation must not be loaded.");
        Assert.True(loaded.Validate().Count == 0, "and the fallback must itself be valid.");
    }

    [Fact]
    public void APayloadWithDuplicateMemberIdsIsRejected()
    {
        // Id is what keys the team table rows (#13); duplicates would make Blazor's diff
        // match two rows to one element.
        var team = TeamMember.GetDefaultTeam();
        team[1].Id = team[0].Id;
        var json = StateSerializer.Serialize(new SprintConfiguration(), team);

        var ok = StateSerializer.TryDeserialize(json, out _, out var loaded);

        Assert.True(!ok, "Duplicate ids must be rejected.");
        var ids = loaded.Select(m => m.Id).ToList();
        Assert.True(
            ids.Distinct(StringComparer.Ordinal).Count() == ids.Count,
            "and the fallback team must have unique ids.");
    }

    [Fact]
    public async Task AnEmptyStoreIsNotAFailure()
    {
        var (state, _) = NewState();

        await state.LoadAsync();

        Assert.True(
            !state.RestoreFailed,
            "A first visit, or storage being unavailable, is not a failure and must not " +
            "say anything to the user.");
    }

    [Fact]
    public async Task UnavailableStorageDegradesToInMemoryStateRatherThanThrowing()
    {
        var (state, store) = NewState();
        store.Unavailable = true;

        await state.LoadAsync();
        state.Config.SprintDuration = 12;
        await state.SaveAsync();
        await state.AddMemberAsync();

        Assert.True(state.Config.SprintDuration == 12, "The session still works in memory.");
        Assert.True(!state.RestoreFailed, "Storage being off is not a corrupt payload.");
    }

    // ── Derived data is not persisted ────────────────────────────────────────

    [Fact]
    public async Task GeneratedResultsAreNotWrittenToStorage()
    {
        var (state, store) = NewState();
        state.Generate();
        await state.SaveAsync();

        var payload = store.Peek(StateSerializer.StorageKey);

        Assert.True(payload is not null, "Something should have been written.");
        Assert.True(
            !payload!.Contains("Summaries", StringComparison.Ordinal) &&
            !payload.Contains("Performance", StringComparison.Ordinal),
            $"Derived data must not be persisted: it is the largest object in the app, " +
            $"and a stored copy is a second source of truth that can disagree with the " +
            $"configuration that produced it. Payload: {payload}");
    }

    // ── Reset clears the stored copy ─────────────────────────────────────────

    [Fact]
    public async Task ResettingTheTeamClearsWhatWasStored()
    {
        var (state, store) = NewState();
        state.Team[0].Name = "Ada";
        await state.SaveAsync();
        Assert.True(store.Peek(StateSerializer.StorageKey) is not null, "Precondition.");

        await state.ResetTeamAsync();

        Assert.True(
            store.Peek(StateSerializer.StorageKey) is null,
            "Without clearing the stored copy the next reload would undo the reset, " +
            "which reads as the application ignoring you.");
    }

    // ── The debounce ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RapidEditsCollapseIntoASingleWrite()
    {
        var (state, store) = NewState();
        state.SaveDelay = TimeSpan.FromMilliseconds(60);

        // Three edits started in quick succession: the first two should be superseded.
        var first = state.OnEditedAsync();
        var second = state.OnEditedAsync();
        var third = state.OnEditedAsync();
        await Task.WhenAll(first, second, third);

        Assert.True(
            store.Writes == 1,
            $"Three rapid edits should collapse into one write, got {store.Writes}. " +
            $"@bind:after fires on every committed edit across six numeric fields plus a " +
            $"row per team member.");
    }
}
