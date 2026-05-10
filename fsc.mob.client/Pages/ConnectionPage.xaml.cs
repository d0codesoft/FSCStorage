using fsc.mob.client.ViewModels;

namespace fsc.mob.client.Pages;

public partial class ConnectionPage : ContentPage
{
    private readonly ConnectionViewModel _viewModel;

    public ConnectionPage(ConnectionViewModel viewModel)
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
