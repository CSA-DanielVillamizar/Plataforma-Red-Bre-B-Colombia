using Breb.App.ViewModels;

namespace Breb.App.Pantallas;

// Sin lógica aquí: el ViewModel llega por DI y el binding hace todo el trabajo.
public partial class InicioSesionPage : ContentPage
{
	public InicioSesionPage(InicioSesionViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
