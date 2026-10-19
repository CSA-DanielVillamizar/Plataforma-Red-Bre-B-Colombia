using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla 2: Saldo. Consulta /cuentas/{id}/saldo con el token que ya está
/// guardado en SecureStorage (hay que iniciar sesión primero, en Login).
/// </summary>
public partial class SaldoViewModel(ClienteBrebApi api, SesionSegura sesion, ConsolaLog consola) : ObservableObject
{
    // Cuenta de demo de las Semanas 2-9 (scripts/cuentas-base.sql).
    [ObservableProperty] private string cuentaId = "11111111-1111-1111-1111-111111111111";
    [ObservableProperty] private string saldoTexto = "—";
    [ObservableProperty] private string retenidoTexto = "—";
    [ObservableProperty] private string estado = "Sin consultar";
    [ObservableProperty] private bool cargando;

    [RelayCommand]
    private async Task ConsultarSaldoAsync()
    {
        if (Cargando) return;
        if (!sesion.HaySesion)
        {
            Estado = "No hay sesión. Ve a Login e inicia sesión primero.";
            return;
        }
        if (!Guid.TryParse(CuentaId, out var id))
        {
            Estado = "El id de cuenta no es un GUID válido.";
            return;
        }

        Cargando = true;
        Estado = "Consultando saldo…";
        try
        {
            var saldo = await api.ConsultarSaldoAsync(id);
            var co = CultureInfo.GetCultureInfo("es-CO");
            SaldoTexto = saldo.SaldoDisponible.ToString("N2", co) + " UVB";
            RetenidoTexto = saldo.SaldoRetenido.ToString("N2", co) + " UVB";
            Estado = $"OK · rol {sesion.Rol}";
        }
        catch (HttpRequestException ex)
        {
            Estado = $"Sin conexión con la API: {ex.Message}";
            consola.Error($"red: {ex.Message}");
            SaldoTexto = "—";
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            consola.Error(ex.Message);
            SaldoTexto = "—";
        }
        finally { Cargando = false; }
    }
}
