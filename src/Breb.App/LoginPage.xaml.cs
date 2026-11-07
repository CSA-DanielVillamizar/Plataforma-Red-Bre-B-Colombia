using Breb.App.ViewModels;

namespace Breb.App;

// Sin lógica aquí: el ViewModel llega por DI y el binding hace todo el trabajo.
public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
