using Breb.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    [ObservableProperty]
    private string usuario = "ana.operadora";

    [ObservableProperty]
    private string clave = "Operadora-2026";

    [ObservableProperty]
    private string mensajeError = string.Empty;

    [ObservableProperty]
    private bool estaCargando;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Clave))
        {
            MensajeError = "Ingrese usuario y contraseña.";
            return;
        }

        EstaCargando = true;
        try
        {
            await _auth.LoginAsync(Usuario.Trim(), Clave);
            await Shell.Current.GoToAsync("saldo");
        }
        catch (ApiException ex)
        {
            MensajeError = ex.Message;
        }
        finally
        {
            EstaCargando = false;
        }
    }
}
