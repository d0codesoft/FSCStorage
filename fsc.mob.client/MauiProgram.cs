using fsc.mob.client.Api;
using fsc.mob.client.Auth;
using fsc.mob.client.Pages;
using fsc.mob.client.Services;
using fsc.mob.client.ViewModels;
using Microsoft.Extensions.Logging;

namespace fsc.mob.client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ConnectionSessionService>();
        builder.Services.AddSingleton<AlertService>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<ConnectivityService>();

        builder.Services.AddHttpClient<FscAdminApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<ConnectionPage>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<UsersPage>();
        builder.Services.AddSingleton<TenantsPage>();
        builder.Services.AddSingleton<MaintenancePage>();
        builder.Services.AddTransient<UserDetailsPage>();
        builder.Services.AddTransient<TenantDetailsPage>();

        builder.Services.AddSingleton<ConnectionViewModel>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<UsersViewModel>();
        builder.Services.AddSingleton<TenantsViewModel>();
        builder.Services.AddSingleton<MaintenanceViewModel>();
        builder.Services.AddTransient<UserDetailsViewModel>();
        builder.Services.AddTransient<TenantDetailsViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
