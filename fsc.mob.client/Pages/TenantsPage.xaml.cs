using fsc.mob.client.ViewModels;

namespace fsc.mob.client.Pages;

public partial class TenantsPage : ContentPage
{
    private readonly TenantsViewModel _viewModel;

    public TenantsPage(TenantsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
