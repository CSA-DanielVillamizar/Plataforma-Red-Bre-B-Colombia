using Breb.App.ViewModels;

namespace Breb.App;

// La regla de la Clase 15 es "sin LÓGICA en el code-behind", no "sin una sola
// línea". OnAppearing es un método de la página, no un evento al que el
// ViewModel pueda suscribirse, así que hay que reenviarlo. Estas dos líneas
// no deciden nada: pasan un aviso del ciclo de vida.
public partial class MainPage : ContentPage
{
	private readonly SaldoViewModel _viewModel;

	public MainPage(SaldoViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.AparecerCommand.Execute(null);
	}
}
