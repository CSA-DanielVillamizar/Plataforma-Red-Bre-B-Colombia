using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Modelos;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

public partial class SaldoViewModel : ViewModelBase
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");
    private readonly AlmacenToken _almacen;

    public SaldoViewModel(ClienteBrebApi api, AlmacenToken almacen) : base(api)
    {
        _almacen = almacen;
        _almacen.SesionCambio += AlCambiarSesion;
        AlCambiarSesion();
    }

    [ObservableProperty]
    private string cuenta = CuentasLaboratorio.Principal.ToString();

    [ObservableProperty]
    private string sesion = "Sin sesión";

    [ObservableProperty]
    private string disponibleTexto = "—";

    [ObservableProperty]
    private string retenidoTexto = "—";

    [ObservableProperty]
    private string actualizadoTexto = "Sin consultar";

    private void AlCambiarSesion()
    {
        Sesion = _almacen.HaySesion ? $"{_almacen.Usuario} · rol {_almacen.Rol}" : "Sin sesión";
        DisponibleTexto = "—";
        RetenidoTexto = "—";
        ActualizadoTexto = "Sin consultar";
        Mensaje = string.Empty;
    }

    [RelayCommand]
    private async Task ConsultarSaldoAsync()
    {
        if (Cargando) return;

        if (!Guid.TryParse(Cuenta?.Trim(), out var cuentaId))
        {
            Fallar("El identificador de la cuenta no es válido.");
            return;
        }

        Cargando = true;
        Mensaje = string.Empty;
        try
        {
            var saldo = await Api.ConsultarSaldoAsync(cuentaId);

            DisponibleTexto = saldo.SaldoDisponible.ToString("N2", Colombia) + " UVB";
            RetenidoTexto = saldo.SaldoRetenido.ToString("N2", Colombia) + " UVB";
            ActualizadoTexto = $"Actualizado {DateTime.Now:HH:mm:ss}";
            Informar("Saldo consultado con éxito.");
        }
        catch (ErrorApi ex)
        {
            DisponibleTexto = "—";
            RetenidoTexto = "—";
            Fallar(ex.Message);

            if (ex.Tipo == TipoError.SesionExpirada)
                await Shell.Current.GoToAsync("//login");
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

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        _almacen.Cerrar();
        await Shell.Current.GoToAsync("//login");
    }
}
