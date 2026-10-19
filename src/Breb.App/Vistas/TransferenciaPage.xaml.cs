using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class TransferenciaPage : ContentPage
{
    public TransferenciaPage(TransferenciaViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
