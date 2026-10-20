using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Pantalla 2 del Evaluable 3: saldo disponible y retenido de una cuenta.
/// El ViewModel no toca un solo control ni el token: expone propiedades
/// observables y comandos; el binding del XAML hace el resto.
/// </summary>
public partial class SaldoViewModel(ClienteBrebApi api, SesionSegura sesion) : ObservableObject
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    // La misma cuenta de demo de las Semanas 2-9 (scripts/cuentas-base.sql).
    [ObservableProperty]
    private string cuentaId = "11111111-1111-1111-1111-111111111111";

    [ObservableProperty]
    private string saldoDisponible = "—";

    [ObservableProperty]
    private string saldoRetenido = "—";

    [ObservableProperty]
    private string sesionTexto = string.Empty;

    [ObservableProperty]
    private string estado = "Sin consultar";

    [ObservableProperty]
    private bool cargando;

    [RelayCommand]
    private async Task ConsultarSaldoAsync()
    {
        if (Cargando) return;
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
            SaldoDisponible = saldo.SaldoDisponible.ToString("N2", Co) + " UVB";
            SaldoRetenido = saldo.SaldoRetenido.ToString("N2", Co) + " UVB";
            SesionTexto = await sesion.DescribirAsync();
            Estado = $"Actualizado {DateTime.Now:HH:mm:ss}";
        }
        catch (ErrorApi ex)
        {
            SaldoDisponible = SaldoRetenido = "—";
            Estado = ex.Message;
            if (ex.SesionVencida) sesion.Cerrar();
        }
        finally
        {
            Cargando = false;
        }
    }

    [RelayCommand]
    private async Task CerrarSesionAsync()
    {
        sesion.Cerrar();
        await Shell.Current.GoToAsync("//login");
    }
}
