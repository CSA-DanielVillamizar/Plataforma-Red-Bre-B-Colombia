using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

public partial class InicioSesionViewModel : ViewModelBase
{
    public InicioSesionViewModel(ClienteBrebApi api) : base(api)
    {
#if DEBUG
        // Usuario de laboratorio documentado en el README del servidor; solo en Debug.
        Usuario = "ana.operadora";
        Clave = "Operadora-2026";
#endif
    }

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string clave = string.Empty;

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        if (Cargando) return;

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrEmpty(Clave))
        {
            Fallar("Escribe tu usuario y tu clave.");
            return;
        }

        Cargando = true;
        Mensaje = string.Empty;
        try
        {
            await Api.IniciarSesionAsync(Usuario.Trim(), Clave);
#if !DEBUG
            Clave = string.Empty;
#endif
            await Shell.Current.GoToAsync("//principal/saldo");
        }
        catch (ErrorApi ex)
        {
            Fallar(ex.Message);
        }
        catch (Exception)
        {
            Fallar("Ocurrió un error inesperado. Inténtalo de nuevo.");
        }
        finally
        {
            Cargando = false;
            MostrarUltimaLlamada();
        }
    }
}
