using Breb.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Breb.App.ViewModels;

public partial class TransferenciaViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBrebApiService _api;

    [ObservableProperty]
    private string cuentaId = "11111111-1111-1111-1111-111111111111";

    [ObservableProperty]
    private string montoUVB = "10";

    [ObservableProperty]
    private string resultado = string.Empty;

    [ObservableProperty]
    private string mensajeError = string.Empty;

    [ObservableProperty]
    private bool estaCargando;

    public TransferenciaViewModel(IBrebApiService api)
    {
        _api = api;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("cuentaId", out var valor) && valor is not null)
            CuentaId = Uri.UnescapeDataString(valor.ToString() ?? string.Empty);
    }

    [RelayCommand]
    private async Task TransferirAsync()
    {
        MensajeError = string.Empty;
        Resultado = string.Empty;

        if (!Guid.TryParse(CuentaId.Trim(), out var id))
        {
            MensajeError = "El Id de cuenta no es un GUID válido.";
            return;
        }

        if (!decimal.TryParse(
                MontoUVB.Trim(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var monto) || monto <= 0)
        {
            MensajeError = "Ingrese un monto UVB mayor que cero (use punto decimal).";
            return;
        }

        EstaCargando = true;
        try
        {
            var retencion = await _api.RetenerAsync(id, monto);
            Resultado =
                $"Transferencia iniciada.\nId: {retencion.TransferenciaId}\nMonto: {retencion.MontoUVB:N2} UVB";
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

    [RelayCommand]
    private async Task VolverAsync() => await Shell.Current.GoToAsync("..");
}
