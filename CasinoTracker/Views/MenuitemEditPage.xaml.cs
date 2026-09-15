using CasinoTracker.ViewModels;

namespace CasinoTracker.Views;

public partial class MenuitemEditPage : ContentPage
{
    private readonly MenuitemEditViewModel _viewModel;

    public MenuitemEditPage(MenuitemEditViewModel viewModel)
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
