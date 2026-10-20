using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class ConsolaPage : ContentPage
{
    public ConsolaPage(ConsolaViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
