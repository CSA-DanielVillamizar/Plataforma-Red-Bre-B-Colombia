using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class SaldoPage : ContentPage
{
    public SaldoPage(SaldoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
