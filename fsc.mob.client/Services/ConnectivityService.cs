using Microsoft.Maui.Networking;

namespace fsc.mob.client.Services;

public sealed class ConnectivityService
{
    public bool HasNetworkAccess =>
        Connectivity.Current.NetworkAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet or NetworkAccess.Local;
}
