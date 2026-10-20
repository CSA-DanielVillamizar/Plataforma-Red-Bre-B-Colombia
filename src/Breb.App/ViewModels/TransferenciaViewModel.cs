using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Modelos;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Dispara POST /cuentas/{id}/retener con el token guardado. No confirma el
/// abono: eso es del rol banco. A los 15 s, si nadie confirma, la saga devuelve
/// la plata. Esta pantalla lo avisa.
/// </summary>
public partial class TransferenciaViewModel : ViewModelBase
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    private readonly ClienteBrebApi _api;
    private readonly IAlmacenSesion _almacen;
    private readonly INavegacion _nav;

    public TransferenciaViewModel(ClienteBrebApi api, IAlmacenSesion almacen, INavegacion nav)
    {
        _api = api;
        _almacen = almacen;
        _nav = nav;
        MontoTexto = "10";
        Estado = "Indique el monto y pulse Transferir.";
        SesionTexto = MensajesDeApi.SinSesion;
    }

    [ObservableProperty]
    private string montoTexto = string.Empty;

    [ObservableProperty]
    private string resultadoTransferencia = string.Empty;

    [ObservableProperty]
    private string saldoTexto = "—";

    [ObservableProperty]
    private string saldoRetenidoTexto = "—";

    [ObservableProperty]
    private string sesionTexto = string.Empty;

    public bool HayResultado => !string.IsNullOrWhiteSpace(ResultadoTransferencia);

    partial void OnResultadoTransferenciaChanged(string value) =>
        OnPropertyChanged(nameof(HayResultado));

    protected override void AlCambiarCargando()
    {
        TransferirCommand.NotifyCanExecuteChanged();
        IrASaldoCommand.NotifyCanExecuteChanged();
        CerrarSesionCommand.NotifyCanExecuteChanged();
    }

    protected override async Task AlRechazarAsync(ExcepcionDeApi ex)
    {
        if (ex.Codigo != 401 || ex.Operacion == "login") return;
        await _almacen.BorrarAsync();
        SesionTexto = MensajesDeApi.SinSesion;
        await _nav.IrAsync(Rutas.Login);
    }

    [RelayCommand]
    private async Task PrepararAsync()
    {
        try
        {
            var sesion = await GuardiaDeSesion.ParaPantallaAsync(_almacen, _nav, DateTimeOffset.UtcNow);
            if (sesion is null) return;
            SesionTexto = $"{sesion.Usuario} · rol {sesion.Rol}";
        }
        catch (ExcepcionAlmacenSeguro ex)
        {
            MarcarError(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task TransferirAsync() => EjecutarAsync(async () =>
    {
        if (!TryParseMonto(MontoTexto, out var monto))
        {
            MarcarError("Escriba un monto mayor que cero. Por ejemplo: 10.");
            return;
        }

        var sesion = await _almacen.LeerAsync();
        if (sesion is null)
        {
            MarcarError(MensajesDeApi.SinSesion);
            await _nav.IrAsync(Rutas.Login);
            return;
        }

        ResultadoTransferencia = string.Empty;
        Estado = "Transfiriendo…";
        var retencion = await _api.RetenerAsync(CuentaDemo.Id, monto, sesion.Token);

        ResultadoTransferencia =
            $"Transferencia aceptada. Se apartaron {FormatoUvb(retencion.MontoUVB)}. Número: {retencion.TransferenciaId}. Si el banco destino no confirma, este dinero vuelve solo en unos 15 segundos.";

        Estado = "Actualizando saldo…";
        try
        {
            var saldo = await _api.ConsultarSaldoAsync(CuentaDemo.Id, sesion.Token);
            SaldoTexto = FormatoUvb(saldo.SaldoDisponible);
            SaldoRetenidoTexto = FormatoUvb(saldo.SaldoRetenido);
            Tono = "ok";
            Estado = $"Listo · rol {sesion.Rol} · la plata quedó apartada. Aún no salió de la cuenta.";
        }
        catch (Exception ex) when (ex is ExcepcionDeApi or HttpRequestException or TaskCanceledException)
        {
            if (ex is ExcepcionDeApi { Codigo: 401 })
            {
                await _almacen.BorrarAsync();
                await _nav.IrAsync(Rutas.Login);
                return;
            }

            Tono = "ok";
            Estado = "La transferencia sí quedó registrada, pero no pude refrescar el saldo. Use Volver a saldo para verlo.";
        }
    });

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task IrASaldoAsync() => _nav.IrAsync(Rutas.Atras);

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task CerrarSesionAsync() => EjecutarAsync(async () =>
    {
        await _almacen.BorrarAsync();
        SesionTexto = MensajesDeApi.SinSesion;
        await _nav.IrAsync(Rutas.Login);
    });

    private static bool TryParseMonto(string? texto, out decimal monto)
    {
        monto = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        const NumberStyles estilos = NumberStyles.Number;
        var ok = decimal.TryParse(texto, estilos, Cultura, out monto)
                 || decimal.TryParse(texto, estilos, CultureInfo.InvariantCulture, out monto);
        return ok && monto > 0;
    }
}
