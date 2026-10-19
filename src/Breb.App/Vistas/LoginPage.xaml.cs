using Breb.App.ViewModels;

namespace Breb.App.Vistas;

// Sin lógica: el ViewModel llega por DI y el binding hace todo el trabajo.
public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
