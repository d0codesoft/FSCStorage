using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using scp.filestorage.webui;
using scp.filestorage.webui.Auth;
using scp.filestorage.webui.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<ApiTokenStore>();
builder.Services.AddScoped<ApiTokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<ApiTokenAuthenticationStateProvider>());
builder.Services.AddScoped<ApiTokenAuthorizationMessageHandler>();

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<ApiTokenAuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();

    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    };
});

builder.Services.AddScoped<AdminApiClient>();

await builder.Build().RunAsync();
