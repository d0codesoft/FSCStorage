using fsc.mob.client.ViewModels;

namespace fsc.mob.client.Pages;

public partial class UserDetailsPage : ContentPage, IQueryAttributable
{
    private readonly UserDetailsViewModel _viewModel;

    public UserDetailsPage(UserDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        await _viewModel.ApplyQueryAsync(query);
    }
}
