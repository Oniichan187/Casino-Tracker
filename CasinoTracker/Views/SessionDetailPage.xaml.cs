using CasinoTracker.ViewModels;

namespace CasinoTracker.Views;

public partial class SessionDetailPage : ContentPage
{
    private readonly SessionDetailViewModel _viewModel;

    public SessionDetailPage(SessionDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
