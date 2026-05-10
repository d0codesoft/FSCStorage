using fsc.mob.client.ViewModels;

namespace fsc.mob.client.Pages;

public partial class MaintenancePage : ContentPage
{
    private readonly MaintenanceViewModel _viewModel;

    public MaintenancePage(MaintenanceViewModel viewModel)
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
