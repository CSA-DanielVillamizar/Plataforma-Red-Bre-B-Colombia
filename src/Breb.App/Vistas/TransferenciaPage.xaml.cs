using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class TransferenciaPage : ContentPage
{
    private readonly TransferenciaViewModel _viewModel;

    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.DispatchAsync(() => _viewModel.PrepararCommand.ExecuteAsync(null));
    }
}
