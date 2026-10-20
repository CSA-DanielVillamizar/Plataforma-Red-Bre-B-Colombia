using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Modelos;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

public partial class TransferenciaViewModel : ViewModelBase
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");
    private readonly AlmacenToken _almacen;

    public TransferenciaViewModel(ClienteBrebApi api, AlmacenToken almacen) : base(api)
    {
        _almacen = almacen;
        _almacen.SesionCambio += AlCambiarSesion;
        AlCambiarSesion();
    }

    [ObservableProperty]
    private string cuentaOrigen = CuentasLaboratorio.Principal.ToString();

    [ObservableProperty]
    private string montoTexto = "100";

    [ObservableProperty]
    private string sesion = "Sin sesión";

    [ObservableProperty]
    private string ultimaTransferencia = "Aún no se ha enviado ninguna.";

    private void AlCambiarSesion()
    {
        Sesion = _almacen.HaySesion ? $"{_almacen.Usuario} · rol {_almacen.Rol}" : "Sin sesión";
        UltimaTransferencia = "Aún no se ha enviado ninguna.";
        Mensaje = string.Empty;
    }

    [RelayCommand]
    private async Task EnviarTransferenciaAsync()
    {
        if (Cargando) return;

        if (!Guid.TryParse(CuentaOrigen?.Trim(), out var cuentaId))
        {
            Fallar("El identificador de la cuenta no es válido.");
            return;
        }

        if (!TryLeerMonto(MontoTexto, out var monto) || monto <= 0)
        {
            Fallar("Escribe un monto mayor que cero.");
            return;
        }

        Cargando = true;
        Mensaje = string.Empty;
        try
        {
            var retencion = await Api.RetenerAsync(cuentaId, monto);

            UltimaTransferencia = $"{retencion.MontoUVB.ToString("N2", Colombia)} UVB · id {retencion.TransferenciaId}";
            Informar("Fondos retenidos. La saga confirma o compensa en unos 15 s: consulta el saldo para verlo.");
        }
        catch (ErrorApi ex)
        {
            Fallar(ex.Tipo == TipoError.Servidor
                ? ex.Message + " Revisa que el monto no supere el saldo disponible."
                : ex.Message);

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

    private static bool TryLeerMonto(string? texto, out decimal monto) =>
        decimal.TryParse(texto, NumberStyles.AllowDecimalPoint, Colombia, out monto) ||
        decimal.TryParse(texto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out monto);
}
