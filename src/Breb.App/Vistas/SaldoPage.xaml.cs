using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class SaldoPage : ContentPage
{
    private readonly SaldoViewModel _viewModel;

    public SaldoPage(SaldoViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.PrepararCommand.ExecuteAsync(null);
    }
}
