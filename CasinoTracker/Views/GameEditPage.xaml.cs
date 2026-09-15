using CasinoTracker.ViewModels;

namespace CasinoTracker.Views;

public partial class GameEditPage : ContentPage
{
    private readonly GameEditViewModel _viewModel;

    public GameEditPage(GameEditViewModel viewModel)
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
