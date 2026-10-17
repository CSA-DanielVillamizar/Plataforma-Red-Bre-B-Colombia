using Breb.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class SaldoViewModel : ObservableObject
{
    private readonly IBrebApiService _api;

    [ObservableProperty]
    private string cuentaId = "11111111-1111-1111-1111-111111111111";

    [ObservableProperty]
    private string saldoDisponible = "—";

    [ObservableProperty]
    private string saldoRetenido = "—";

    [ObservableProperty]
    private string mensajeError = string.Empty;

    [ObservableProperty]
    private bool estaCargando;

    public SaldoViewModel(IBrebApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task ConsultarAsync()
    {
        MensajeError = string.Empty;

        if (!Guid.TryParse(CuentaId.Trim(), out var id))
        {
            MensajeError = "El Id de cuenta no es un GUID válido.";
            return;
        }

        EstaCargando = true;
        try
        {
            var saldo = await _api.ObtenerSaldoAsync(id);
            SaldoDisponible = saldo.SaldoDisponible.ToString("N2");
            SaldoRetenido = saldo.SaldoRetenido.ToString("N2");
        }
        catch (ApiException ex)
        {
            SaldoDisponible = "—";
            SaldoRetenido = "—";
            MensajeError = ex.Message;
        }
        finally
        {
            EstaCargando = false;
        }
    }

    [RelayCommand]
    private async Task IrATransferenciaAsync()
    {
        MensajeError = string.Empty;

        if (!Guid.TryParse(CuentaId.Trim(), out _))
        {
            MensajeError = "Ingrese un Id de cuenta válido antes de transferir.";
            return;
        }

        await Shell.Current.GoToAsync($"transferencia?cuentaId={Uri.EscapeDataString(CuentaId.Trim())}");
    }
}
