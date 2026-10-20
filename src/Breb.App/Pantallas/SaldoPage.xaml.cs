using Breb.App.ViewModels;

namespace Breb.App.Pantallas;

// Sin lógica aquí: el ViewModel llega por DI y el binding hace todo el trabajo.
public partial class SaldoPage : ContentPage
{
	public SaldoPage(SaldoViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
