namespace SupercompensationApp.Tests;

using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// Every test here constructs AppStateService directly. That is the point of the issue
/// rather than a detail of it: the state used to be four `public static` properties on
/// Pages/Index.razor, and static mutable state is shared across the whole process while
/// xUnit runs test classes in parallel — so a test touching Index.Config could corrupt
/// an unrelated one, and none of this could be written at all.
/// </summary>
public class AppStateServiceTests
{
    private static AppStateService NewState() => new(new SupercompensationService());

    [Fact]
    public void ItStartsWithTheDefaultTeamAndNoResults()
    {
        var state = NewState();

        Assert.True(state.Team.Count > 0, "A fresh state should carry the default team.");
        Assert.True(state.Problems.Count == 0, "The default configuration must be valid.");
        Assert.True(!state.HasResults, "Nothing has been generated yet.");
        Assert.True(!state.IsStale, "Nothing generated cannot be stale.");
    }

    [Fact]
    public void GenerateProducesResultsThatAreNotStale()
    {
        var state = NewState();

        Assert.True(state.Generate(), "The default configuration must generate.");
        Assert.True(state.HasResults, "Generate must populate both the chart and the table.");
        Assert.True(
            !state.IsStale,
            "Results are fresh the instant they are generated; anything else means the " +
            "signature captured at generation does not match the one computed from the " +
            "same inputs.");
    }

    [Fact]
    public void GenerateRefusesAnInvalidConfigurationAndChangesNothing()
    {
        var state = NewState();
        state.Generate();
        var before = state.LastChartData;

        state.Config.SprintDuration = 0;

        Assert.True(!state.Generate(), "An unusable configuration must not generate.");
        Assert.True(
            ReferenceEquals(before, state.LastChartData),
            "A refused Generate must leave the previous results untouched rather than " +
            "half-replacing them.");
    }

    // ── Staleness: item 4 of the issue, made visible ──────────────────────────

    [Fact]
    public void EditingTheConfigurationAfterGeneratingMarksTheResultsStale()
    {
        var state = NewState();
        state.Generate();

        state.Config.FatigueDepth += 1.0;

        Assert.True(
            state.IsStale,
            "The chart on screen now describes a fatigue depth the user has already " +
            "changed, and nothing else would say so.");
    }

    [Fact]
    public void EditingATeamMemberMarksTheResultsStale()
    {
        var state = NewState();
        state.Generate();

        state.Team[0].Weight += 0.1;

        Assert.True(
            state.IsStale,
            "Team weight scales every number on the chart, so a changed weight makes the " +
            "displayed results describe a different team.");
    }

    [Fact]
    public void ResettingTheTeamMarksTheResultsStale()
    {
        // This is the exact defect the issue describes: ResetTeam() used to reassign the
        // list while LastChartData still described the previous team, and the two were
        // silently out of step until the user pressed generate again.
        var state = NewState();
        state.Team[0].Weight = 1.75;
        state.Generate();

        state.ResetTeam();

        Assert.True(
            state.IsStale,
            "Resetting the team must not leave a chart describing the old one without " +
            "saying so.");
    }

    [Fact]
    public void RegeneratingClearsStaleness()
    {
        var state = NewState();
        state.Generate();
        state.Config.NumberOfSprints += 1;
        Assert.True(state.IsStale, "Precondition for this test.");

        state.Generate();

        Assert.True(!state.IsStale, "Regenerating must bring the results back into step.");
        Assert.True(
            state.LastChartData!.Summaries.Count == state.Config.NumberOfSprints,
            "and the new results must reflect the edited configuration.");
    }

    // ── Team membership ──────────────────────────────────────────────────────

    [Fact]
    public void TheLastTeamMemberCannotBeRemoved()
    {
        // A team of zero makes CalculateTeamWeight return its sentinel 1.0, which is a
        // different model rather than an empty one.
        var state = NewState();
        while (state.CanRemoveMember)
        {
            state.RemoveMember(state.Team[0]);
        }

        Assert.True(state.Team.Count == 1, $"Expected one member left, got {state.Team.Count}.");

        state.RemoveMember(state.Team[0]);

        Assert.True(state.Team.Count == 1, "Removing the last member must be refused.");
        Assert.True(!state.CanRemoveMember, "and the UI must be able to see that in advance.");
    }

    [Fact]
    public void RemovingTakesTheMemberNotAPosition()
    {
        // Passing an index into a callback that fires after a re-render is stale by
        // construction — the same reasoning behind @key in #13.
        var state = NewState();
        var target = state.Team[2];
        var before = state.Team.Count;

        state.RemoveMember(target);

        Assert.True(state.Team.Count == before - 1, "Exactly one member should be gone.");

        // List<T>.Exists rather than Enumerable.Any: xUnit's analyzer flags Any() inside
        // Assert.True (xUnit2012) and TreatWarningsAsErrors turns that into a build
        // failure, while Assert.DoesNotContain would lose the message.
        var stillPresent = state.Team.Exists(m => m.Id == target.Id);
        Assert.True(
            !stillPresent,
            "and it should be the one that was passed in, identified by Id.");
    }

    // ── Change notification ──────────────────────────────────────────────────

    [Fact]
    public void SubscribersAreNotifiedOfEveryStateChange()
    {
        var state = NewState();
        var count = 0;
        void Handler() => count++;

        state.OnChange += Handler;

        state.AddMember();
        state.Generate();
        state.ResetTeam();
        state.RemoveMember(state.Team[0]);

        Assert.True(
            count == 4,
            $"Each of AddMember, Generate, ResetTeam and RemoveMember should notify once; " +
            $"got {count}. A page rendered while state changes elsewhere has no other way " +
            $"to find out.");
    }

    [Fact]
    public void UnsubscribingStopsDelivery()
    {
        // The Dispose half. A component that subscribes without unsubscribing leaks and
        // re-renders a tree that has already been disposed.
        var state = NewState();
        var count = 0;
        void Handler() => count++;

        state.OnChange += Handler;
        state.AddMember();
        var afterSubscribe = count;

        state.OnChange -= Handler;
        state.AddMember();

        Assert.True(afterSubscribe == 1, $"Expected one notification while subscribed, got {afterSubscribe}.");
        Assert.True(count == 1, $"No further notifications after unsubscribing; got {count}.");
    }
}
