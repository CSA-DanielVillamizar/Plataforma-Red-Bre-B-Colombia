using Breb.App.ViewModels;

namespace Breb.App.Vistas;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    // Único código aquí: al abrir, si hay un JWT vigente en SecureStorage, entra directo.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.EntrarSiHaySesionAsync();
    }
}
