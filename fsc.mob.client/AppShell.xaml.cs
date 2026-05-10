namespace fsc.mob.client;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("user-details", typeof(Pages.UserDetailsPage));
        Routing.RegisterRoute("tenant-details", typeof(Pages.TenantDetailsPage));
    }
}
