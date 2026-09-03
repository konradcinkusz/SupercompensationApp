namespace SupercompensationApp.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SupercompensationApp.Services;
using Xunit;

// System.Index is in scope through ImplicitUsings, so the page has to be aliased.
using IndexPage = SupercompensationApp.Pages.Index;

/// <summary>
/// Tests for the four .razor components, which had none.
///
/// The 48 tests beside this file cover the model, the exporter, the state service and
/// persistence, and every one of them passed throughout while two component defects
/// shipped: a Generate button that navigated outside the application on the deployed
/// site, and a restored configuration that reached AppStateService and never reached the
/// input. Both were caught by the browser smoke test, which is the right instrument but
/// the expensive one — it needs a publish, a static server and a WebAssembly boot, so a
/// round is about ninety seconds and the second defect took three of them.
///
/// The same two defects are asserted here in tens of milliseconds. Both were re-introduced
/// and confirmed red before this file was committed; a test that has not been seen to fail
/// is not evidence, which this repository has now paid for five times.
///
/// This does NOT replace the browser test. bUnit renders to an HTML representation with no
/// layout, no CSS and no real JavaScript, so it cannot see a title painted in its own
/// background colour, a phase band on the wrong pixel, an SRI hash the browser rejects, or
/// a caret lost with its DOM node. It covers the layer between the services and the page.
/// </summary>
public class PageComponentTests : BunitContext
{
    /// <summary>
    /// A NavigationManager whose base URI carries a PATH, which the default one does not.
    ///
    /// That is the whole point of it. Under a base of "http://localhost/", NavigateTo("chart")
    /// and NavigateTo("/chart") both resolve to http://localhost/chart, so a test against that
    /// base cannot tell the correct call from the defect and would pass either way. Under a
    /// project-page base they differ, which is exactly why the defect existed on GitHub Pages
    /// and not under `dotnet run`.
    /// </summary>
    private sealed class ProjectPageNavigation : NavigationManager
    {
        public const string Base = "http://localhost/SupercompensationApp/";

        public ProjectPageNavigation() => Initialize(Base, Base);

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            Uri = ToAbsoluteUri(uri).ToString();
    }

    private readonly InMemoryStateStore _store = new();

    private AppStateService NewState()
    {
        var state = new AppStateService(new SupercompensationService(), _store);
        Services.AddSingleton(state);
        // Chart calls into renderSupercompChart from OnAfterRenderAsync; there is no
        // JavaScript here and the call is not what is under test.
        JSInterop.Mode = JSRuntimeMode.Loose;
        return state;
    }

    // ── The restore, which is the defect that cost three browser rounds ──────────

    [Fact]
    public async Task AConfigurationRestoredAfterTheFirstRenderReachesTheInput()
    {
        var state = NewState();
        var cut = Render<IndexPage>();

        // MainLayout restores in an awaited OnInitializedAsync, so the first paint
        // necessarily shows the defaults. This is that first paint.
        Assert.Equal("10", cut.Find(".param-card input[type=number]").GetAttribute("value"));

        // ...and this is the restore arriving afterwards, which is all LoadAsync does.
        state.Config.SprintDuration = 14;
        await cut.InvokeAsync(state.NotifyChanged);

        Assert.Equal("14", cut.Find(".param-card input[type=number]").GetAttribute("value"));
    }

    // Stated per page rather than as one theory, because a page that stops subscribing
    // should name itself in the failure.
    [Fact]
    public Task TheConfigurationPageRerendersWhenTheStateChanges() => AssertRerenders<IndexPage>();

    [Fact]
    public Task TheChartPageRerendersWhenTheStateChanges() => AssertRerenders<SupercompensationApp.Pages.Chart>();

    [Fact]
    public Task TheDataPageRerendersWhenTheStateChanges() => AssertRerenders<SupercompensationApp.Pages.Data>();

    /// <summary>
    /// The general form of the defect: AppStateService raises OnChange for exactly this,
    /// and a page that does not subscribe goes on showing what it rendered first.
    /// </summary>
    private async Task AssertRerenders<TPage>()
        where TPage : IComponent
    {
        var state = NewState();
        var cut = Render<TPage>();
        var before = cut.RenderCount;

        await cut.InvokeAsync(state.NotifyChanged);

        Assert.True(cut.RenderCount > before,
            $"{typeof(TPage).Name} did not re-render when AppStateService.OnChange fired, so a " +
            $"change made anywhere else — a restore, or an edit on another page — is invisible on it");
    }

    // ── Navigation, the other defect ────────────────────────────────────────────

    [Fact]
    public void GenerateNavigatesRelativeToTheBaseHref()
    {
        var nav = new ProjectPageNavigation();
        Services.AddSingleton<NavigationManager>(nav);
        NewState();

        Render<IndexPage>().Find(".btn-generate").Click();

        Assert.Equal($"{ProjectPageNavigation.Base}chart", nav.Uri);
    }

    // ── What the markup promises about state it does not own ────────────────────

    [Fact]
    public void GenerateIsDisabledWhileTheConfigurationIsUnusable()
    {
        var state = NewState();
        state.Config.SprintDuration = 0;   // the #11 case: every deviation becomes NaN

        var cut = Render<IndexPage>();

        Assert.True(cut.Find(".btn-generate").HasAttribute("disabled"));
        Assert.NotEmpty(cut.FindAll(".validation-errors li"));
    }

    [Fact]
    public async Task TheRemoveButtonIsDisabledAtTheLastMember()
    {
        var state = NewState();
        while (state.CanRemoveMember)
        {
            await state.RemoveMemberAsync(state.Team[^1]);
        }

        var cut = Render<IndexPage>();

        var remove = cut.FindAll(".btn-remove");
        Assert.Single(remove);
        Assert.True(remove[0].HasAttribute("disabled"),
            "a team of zero makes CalculateTeamWeight return its sentinel 1.0, which is a " +
            "different model rather than an empty one, so the floor has to be visible");
    }

    [Fact]
    public async Task TheRestoreFailureNoticeAppearsWhenAStoredPayloadIsRejected()
    {
        var state = NewState();
        _store.Seed(StateSerializer.StorageKey, "{ not json at all");
        await state.LoadAsync();

        var cut = Render<IndexPage>();

        Assert.True(state.RestoreFailed);
        Assert.NotEmpty(cut.FindAll(".stale-notice"));
    }
}
