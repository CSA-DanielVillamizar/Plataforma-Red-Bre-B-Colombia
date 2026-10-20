using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla 3 del Evaluable 3: disparar una transferencia y ver su resultado.
/// En la API, transferir = POST /cuentas/{id}/retener (rol operador): retiene
/// los fondos y arranca la saga (Semana 3), que espera la confirmación del
/// banco destino y, si no llega en 15 s, compensa.
/// </summary>
public partial class TransferenciaViewModel(ClienteBrebApi api, SesionSegura sesion) : ObservableObject
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    [ObservableProperty]
    private string cuentaOrigen = "11111111-1111-1111-1111-111111111111";

    [ObservableProperty]
    private string monto = "100";

    [ObservableProperty]
    private string resultado = string.Empty;

    [ObservableProperty]
    private string estado = string.Empty;

    [ObservableProperty]
    private bool cargando;

    [RelayCommand]
    private async Task TransferirAsync()
    {
        if (Cargando) return;
        if (!Guid.TryParse(CuentaOrigen, out var origen))
        {
            Estado = "La cuenta origen no es un GUID válido.";
            return;
        }
        // Acepta "1500,50" y "1500.50". Sin separador de miles: en es-CO el
        // punto es de miles y "100.5" se leería como 1005.
        if (!decimal.TryParse(Monto?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                              CultureInfo.InvariantCulture, out var valor))
        {
            Estado = "El monto no es un número (ejemplo: 1500,50).";
            return;
        }
        if (valor <= 0)
        {
            Estado = "El monto debe ser mayor que cero.";
            return;
        }

        Cargando = true;
        Resultado = string.Empty;
        Estado = "Enviando transferencia…";
        try
        {
            var r = await api.TransferirAsync(origen, valor);
            var saldo = await api.ConsultarSaldoAsync(origen);

            Resultado =
                $"Transferencia {r.TransferenciaId}\n" +
                $"Retenidos: {r.MontoUVB.ToString("N2", Co)} UVB\n" +
                $"Disponible ahora: {saldo.SaldoDisponible.ToString("N2", Co)} UVB\n" +
                $"Retenido ahora: {saldo.SaldoRetenido.ToString("N2", Co)} UVB";
            Estado = "Fondos retenidos. La saga espera la confirmación del banco destino; " +
                     "si no llega en 15 s, compensa y el saldo vuelve (véalo en Saldo).";
        }
        catch (ErrorApi ex)
        {
            Estado = ex.Message;
            if (ex.SesionVencida) sesion.Cerrar();
        }
        finally
        {
            Cargando = false;
        }
    }
}
