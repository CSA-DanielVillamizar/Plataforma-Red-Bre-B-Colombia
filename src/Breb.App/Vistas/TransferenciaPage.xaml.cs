using Breb.App.ViewModels;

namespace Breb.App.Vistas;

// Sin lógica aquí: el ViewModel llega por DI y el binding hace todo el trabajo.
public partial class TransferenciaPage : ContentPage
{
    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
