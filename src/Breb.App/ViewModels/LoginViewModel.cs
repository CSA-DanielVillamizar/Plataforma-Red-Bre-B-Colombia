using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>Pantalla 1 del Evaluable 3: autenticarse contra /token y guardar el JWT.</summary>
public partial class LoginViewModel(ClienteBrebApi api, SesionSegura sesion) : ObservableObject
{
    // Usuarios de laboratorio (DirectorioUsuarios.cs). El operador puede
    // consultar saldo Y transferir; luis.consulta solo consulta.
    [ObservableProperty]
    private string usuario = "ana.operadora";

    [ObservableProperty]
    private string clave = string.Empty;

    [ObservableProperty]
    private string estado = string.Empty;

    [ObservableProperty]
    private bool cargando;

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        if (Cargando) return;
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Clave))
        {
            Estado = "Escriba usuario y clave.";
            return;
        }

        Cargando = true;
        Estado = "Pidiendo token…";
        try
        {
            var token = await api.ObtenerTokenAsync(Usuario.Trim(), Clave);
            await sesion.GuardarAsync(Usuario.Trim(), token);
            Clave = string.Empty;
            Estado = string.Empty;
            await Shell.Current.GoToAsync("//saldo");
        }
        catch (ErrorApi ex)
        {
            Estado = ex.Message;
        }
        finally
        {
            Cargando = false;
        }
    }

    /// <summary>Si quedó un token vigente en SecureStorage, entra sin pedir clave.</summary>
    public async Task EntrarSiHaySesionAsync()
    {
        if (await sesion.ObtenerTokenAsync() is not null)
            await Shell.Current.GoToAsync("//saldo");
    }
}
