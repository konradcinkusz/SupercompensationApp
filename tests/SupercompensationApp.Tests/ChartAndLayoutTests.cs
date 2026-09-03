namespace SupercompensationApp.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using SupercompensationApp.Layout;
using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// The behaviour a coverage run showed had nothing behind it.
///
/// Two of these guard single points of failure. MainLayout.OnInitializedAsync is the ONLY
/// call to AppStateService.LoadAsync in the application, so deleting that one line loses
/// persistence entirely and nothing fails but a ninety-second browser round. And
/// Chart.RenderChart hands the whole payload to renderSupercompChart, which is what the
/// chart is actually drawn from — the boundary arithmetic in it is the same quantity that
/// was wrong in #9.
///
/// Every test here was confirmed red by breaking the thing it guards.
/// </summary>
public class ChartAndLayoutTests : BunitContext
{
    private readonly InMemoryStateStore _store = new();

    private AppStateService NewState()
    {
        var state = new AppStateService(new SupercompensationService(), _store);
        Services.AddSingleton(state);
        return state;
    }

    // ── MainLayout is the only thing that restores ──────────────────────────────

    [Fact]
    public void RenderingTheLayoutRestoresTheStoredConfiguration()
    {
        var state = NewState();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var stored = new SprintConfiguration { SprintDuration = 21, NumberOfSprints = 5 };
        _store.Seed(StateSerializer.StorageKey,
            StateSerializer.Serialize(stored, TeamMember.GetDefaultTeam()));

        Render<MainLayout>();

        Assert.Equal(21, state.Config.SprintDuration);
        Assert.Equal(5, state.Config.NumberOfSprints);
    }

    // ── What the chart is actually drawn from ───────────────────────────────────

    /// <summary>
    /// The arguments of the last renderSupercompChart call, in the order Chart.razor
    /// passes them: days, performance, baselines, phases, sprint boundaries, show phases,
    /// show baseline, sprint duration.
    /// </summary>
    private IReadOnlyList<object?> LastChartCall() =>
        JSInterop.Invocations["renderSupercompChart"][^1].Arguments;

    private AppStateService GeneratedState()
    {
        var state = NewState();
        JSInterop.SetupVoid("renderSupercompChart", _ => true);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Assert.True(state.Generate(), "the default configuration must be generatable");
        return state;
    }

    [Fact]
    public void TheChartIsDrawnFromTheGeneratedSeries()
    {
        var state = GeneratedState();

        Render<SupercompensationApp.Pages.Chart>();

        var args = LastChartCall();
        var days = Assert.IsType<double[]>(args[0]);
        var performance = Assert.IsType<double[]>(args[1]);

        Assert.Equal(state.LastChartData!.Days.Count, days.Length);
        Assert.Equal(state.LastChartData.Performance.Count, performance.Length);
        Assert.Equal(state.Config.SprintDuration, Assert.IsType<int>(args[7]));
    }

    [Fact]
    public void TheSprintBoundariesAreTheJoinsAndNeitherEnd()
    {
        var state = GeneratedState();   // defaults: 3 sprints of 10 days

        Render<SupercompensationApp.Pages.Chart>();

        var boundaries = Assert.IsType<double[]>(LastChartCall()[4]);

        // Two joins for three sprints. Day 0 is not a join and day 30 is the end of the
        // last sprint, so an off-by-one at either end of that loop puts a phase band
        // where no sprint changes — which is the shape of the #9 defect.
        Assert.Equal(new double[] { 10, 20 }, boundaries);
    }

    /// <summary>
    /// Both toggles carry the same class and are told apart by order, so the count is
    /// asserted: adding a third would otherwise silently retarget these tests at the
    /// wrong button and they would go on passing.
    /// </summary>
    private static AngleSharp.Dom.IElement Toggle(
        Bunit.IRenderedComponent<SupercompensationApp.Pages.Chart> cut, int index)
    {
        var toggles = cut.FindAll(".btn-toggle");
        Assert.Equal(2, toggles.Count);
        return toggles[index];
    }

    [Fact]
    public void TogglingThePhaseBandsChangesWhatTheChartIsToldToDraw()
    {
        GeneratedState();
        var cut = Render<SupercompensationApp.Pages.Chart>();

        Assert.True(Assert.IsType<bool>(LastChartCall()[5]), "phases start shown");

        Toggle(cut, 0).Click();

        Assert.False(Assert.IsType<bool>(LastChartCall()[5]));
    }

    [Fact]
    public void TogglingTheBaselineChangesWhatTheChartIsToldToDraw()
    {
        GeneratedState();
        var cut = Render<SupercompensationApp.Pages.Chart>();

        Assert.True(Assert.IsType<bool>(LastChartCall()[6]), "the baseline starts shown");

        Toggle(cut, 1).Click();

        Assert.False(Assert.IsType<bool>(LastChartCall()[6]));
    }
}
