using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SupercompensationApp;
using SupercompensationApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<SupercompensationService>();

// The application's state, replacing four `public static` properties that used to live
// on Pages/Index.razor. Singleton rather than scoped: Blazor WebAssembly is single-user,
// and scoped would behave identically here while saying something untrue about lifetime.
builder.Services.AddSingleton<AppStateService>();

await builder.Build().RunAsync();
