using fsc.mob.client.ViewModels;

namespace fsc.mob.client.Pages;

public partial class TenantDetailsPage : ContentPage, IQueryAttributable
{
    private readonly TenantDetailsViewModel _viewModel;

    public TenantDetailsPage(TenantDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        await _viewModel.ApplyQueryAsync(query);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ClearSensitiveTokenDisplay();
    }
}
