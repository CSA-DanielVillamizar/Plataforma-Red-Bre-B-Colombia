using Breb.App.ViewModels;

namespace Breb.App.Views;

public partial class SaldoPage : ContentPage
{
    public SaldoPage(SaldoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
