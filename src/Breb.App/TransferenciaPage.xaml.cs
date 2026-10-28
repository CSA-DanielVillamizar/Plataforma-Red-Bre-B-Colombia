using Breb.App.ViewModels;

namespace Breb.App;

// Clase 15 y sigue valiendo: el code-behind no tiene lógica. Dos líneas.
// Si aquí apareciera un if, un try o un acceso a un control por su nombre,
// el MVVM se rompió y la nota del criterio de arquitectura se va con él.
public partial class TransferenciaPage : ContentPage
{
    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
