using CasinoTracker.ViewModels;

namespace CasinoTracker.Views;

public partial class CasinoEditPage : ContentPage
{
    private readonly CasinoEditViewModel _viewModel;

    public CasinoEditPage(CasinoEditViewModel viewModel)
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
