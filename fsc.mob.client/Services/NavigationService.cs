namespace fsc.mob.client.Services;

public sealed class NavigationService
{
    public Task GoToDashboardAsync() => Shell.Current.GoToAsync("//dashboard");
    public Task GoToConnectionAsync() => Shell.Current.GoToAsync("//connection");
    public Task GoToUsersAsync() => Shell.Current.GoToAsync("//users");
    public Task GoToTenantsAsync() => Shell.Current.GoToAsync("//tenants");

    public Task GoToUserDetailsAsync(Guid? userId = null)
    {
        return userId.HasValue
            ? Shell.Current.GoToAsync($"user-details?userId={userId.Value:D}")
            : Shell.Current.GoToAsync("user-details");
    }

    public Task GoToTenantDetailsAsync(Guid? tenantId = null, Guid? ownerUserId = null)
    {
        var queryParts = new List<string>();
        if (tenantId.HasValue)
            queryParts.Add($"tenantId={tenantId.Value:D}");

        if (ownerUserId.HasValue)
            queryParts.Add($"ownerUserId={ownerUserId.Value:D}");

        var route = queryParts.Count == 0
            ? "tenant-details"
            : $"tenant-details?{string.Join("&", queryParts)}";

        return Shell.Current.GoToAsync(route);
    }
}
