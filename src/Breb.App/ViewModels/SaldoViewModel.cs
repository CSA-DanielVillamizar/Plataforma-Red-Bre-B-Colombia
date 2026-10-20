using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Clase 15: el ViewModel no toca un solo control. Expone propiedades
/// observables y comandos; el binding del XAML hace el resto.
/// </summary>
public partial class SaldoViewModel : ObservableObject
{
    // La misma cuenta de demo de las Semanas 2-9 (scripts/cuentas-base.sql).
    private static readonly Guid CuentaDemo = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ClienteBrebApi _api;
    private string? _token;

    public SaldoViewModel(ClienteBrebApi api)
    {
        _api = api;
        Usuario = "luis.consulta";
        Clave = "Consulta-2026";
    }

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string clave = string.Empty;

    [ObservableProperty]
    private string saldoTexto = "—";

    [ObservableProperty]
    private string estado = "Sin consultar";

    [ObservableProperty]
    private bool cargando;

    [RelayCommand]
    private async Task ConsultarSaldoAsync()
    {
        if (Cargando) return;
        Cargando = true;
        Estado = "Pidiendo token…";
        try
        {
            // Paso 1: login contra /token (Semana 8). El token se guarda en
            // memoria del ViewModel, nunca en un campo de la vista.
            var tokenRespuesta = await _api.ObtenerTokenAsync(Usuario, Clave);
            _token = tokenRespuesta.Token;

            Estado = "Consultando saldo…";

            // Paso 2: /cuentas/{id}/saldo con el Bearer recién emitido.
            var saldo = await _api.ConsultarSaldoAsync(CuentaDemo, _token);

            SaldoTexto = saldo.SaldoDisponible.ToString("N2", CultureInfo.GetCultureInfo("es-CO")) + " UVB";
            Estado = $"OK · rol {tokenRespuesta.Rol} · retenido {saldo.SaldoRetenido:N2} UVB";
        }
        catch (HttpRequestException ex)
        {
            // Este es el error que aparece cuando el HttpClient apunta a
            // localhost en vez de 10.0.2.2 (Clase 14, trampa 3).
            Estado = $"Sin conexión con la API: {ex.Message}";
            SaldoTexto = "—";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            SaldoTexto = "—";
        }
        finally
        {
            Cargando = false;
        }
    }
}
