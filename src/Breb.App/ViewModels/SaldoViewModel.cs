using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// La pantalla no toca un solo control. Expone propiedades observables y
/// comandos; el binding del XAML hace el resto, incluidos los botones
/// apagados mientras hay una petición en curso (CanExecute = !Cargando).
/// </summary>
public partial class SaldoViewModel : ObservableObject
{
    // La misma cuenta de demo de las Semanas 2-9 (scripts/cuentas-base.sql).
    private static readonly Guid CuentaDemo = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    private readonly ClienteBrebApi _api;
    private string? _token;
    private string? _rol;
    private string? _usuarioDelToken;

    public SaldoViewModel(ClienteBrebApi api)
    {
        _api = api;
        Usuario = "luis.consulta";
        Clave = "Consulta-2026";
        MontoTexto = "10";
    }

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string clave = string.Empty;

    [ObservableProperty]
    private string montoTexto = string.Empty;

    [ObservableProperty]
    private string saldoTexto = "—";

    [ObservableProperty]
    private string saldoRetenidoTexto = "—";

    [ObservableProperty]
    private string resultadoTransferencia = string.Empty;

    [ObservableProperty]
    private string estado = "Sin consultar";

    /// <summary>"neutro", "ok" o "error". La vista solo cambia el color.</summary>
    [ObservableProperty]
    private string tono = "neutro";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConsultarSaldoCommand))]
    [NotifyCanExecuteChangedFor(nameof(TransferirCommand))]
    private bool cargando;

    public bool HayResultado => !string.IsNullOrWhiteSpace(ResultadoTransferencia);

    partial void OnResultadoTransferenciaChanged(string value) =>
        OnPropertyChanged(nameof(HayResultado));

    private bool NoEstaCargando() => !Cargando;

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task ConsultarSaldoAsync() => EjecutarAsync(async () =>
    {
        Estado = "Pidiendo token…";
        var token = await _api.ObtenerTokenAsync(Usuario.Trim(), Clave);
        _token = token.Token;
        _rol = token.Rol;
        _usuarioDelToken = Usuario.Trim();

        Estado = "Consultando saldo…";
        await MostrarSaldoAsync();
        Tono = "ok";
        Estado = $"OK · rol {_rol} · retenido {SaldoRetenidoTexto}";
    }, limpiarSaldoSiFalla: true);

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private Task TransferirAsync() => EjecutarAsync(async () =>
    {
        if (!TryParseMonto(MontoTexto, out var monto))
        {
            MarcarError("Escriba un monto mayor que cero. Por ejemplo: 10.");
            return;
        }

        ResultadoTransferencia = string.Empty;

        // Mismo token de la consulta si el usuario no cambió: así un token
        // vencido (dura 15 minutos) se ve como 401 al transferir. Si cambió
        // el usuario, se pide otro: el rol lo decide el servidor, no la app.
        if (_token is null || !string.Equals(_usuarioDelToken, Usuario.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Estado = "Pidiendo token…";
            var token = await _api.ObtenerTokenAsync(Usuario.Trim(), Clave);
            _token = token.Token;
            _rol = token.Rol;
            _usuarioDelToken = Usuario.Trim();
        }

        Estado = "Transfiriendo…";
        var retencion = await _api.RetenerAsync(CuentaDemo, monto, _token);

        ResultadoTransferencia =
            $"Transferencia aceptada. Se apartaron {FormatoUvb(retencion.MontoUVB)}. Número: {retencion.TransferenciaId}.";

        Estado = "Actualizando saldo…";
        try
        {
            await MostrarSaldoAsync();
            Tono = "ok";
            Estado = $"Listo · rol {_rol} · el saldo ya refleja la transferencia.";
        }
        catch (Exception ex) when (ex is ExcepcionDeApi or HttpRequestException or TaskCanceledException)
        {
            if (ex is ExcepcionDeApi { Codigo: 401 })
                OlvidarToken();

            // El dinero ya se apartó. No pintar esto como un fallo de la transferencia.
            Tono = "ok";
            Estado = "La transferencia sí quedó registrada, pero no pude refrescar el saldo. Pulse Consultar saldo.";
        }
    }, limpiarSaldoSiFalla: false);

    private async Task MostrarSaldoAsync()
    {
        var saldo = await _api.ConsultarSaldoAsync(CuentaDemo, _token!);
        SaldoTexto = FormatoUvb(saldo.SaldoDisponible);
        SaldoRetenidoTexto = FormatoUvb(saldo.SaldoRetenido);
    }

    private async Task EjecutarAsync(Func<Task> accion, bool limpiarSaldoSiFalla)
    {
        if (Cargando) return;
        Cargando = true;
        Tono = "neutro";
        try
        {
            await accion();
        }
        catch (ExcepcionDeApi ex)
        {
            if (ex.Codigo == 401 && ex.Operacion != "login")
                OlvidarToken();
            if (limpiarSaldoSiFalla) LimpiarSaldos();
            MarcarError(MensajeDe(ex));
        }
        catch (TaskCanceledException)
        {
            // HttpClient.Timeout (10 s en MauiProgram) llega como cancelación,
            // no como un código HTTP: la API no alcanzó a contestar.
            if (limpiarSaldoSiFalla) LimpiarSaldos();
            MarcarError("La API no respondió a tiempo (más de 10 segundos). Compruebe que sigue encendida e intente otra vez.");
        }
        catch (HttpRequestException ex)
        {
            if (limpiarSaldoSiFalla) LimpiarSaldos();
            MarcarError("No hay conexión con la API. En el emulador la dirección es 10.0.2.2 puerto 5080, no localhost. " + ex.Message);
        }
        catch (Exception ex)
        {
            if (limpiarSaldoSiFalla) LimpiarSaldos();
            MarcarError("Ocurrió un error inesperado: " + ex.Message);
        }
        finally
        {
            Cargando = false;
        }
    }

    private static string MensajeDe(ExcepcionDeApi ex) => ex.Codigo switch
    {
        401 when ex.Operacion == "login" =>
            "Usuario o clave incorrectos.",
        401 =>
            "La sesión venció o el token ya no es válido. Vuelva a consultar el saldo para entrar otra vez.",
        403 when ex.Operacion == "transferencia" =>
            "No tiene permiso para transferir. Hace falta el rol operador: entre con ana.operadora. luis.consulta solo puede ver el saldo.",
        403 =>
            "No tiene permiso para esta operación.",
        404 =>
            string.IsNullOrWhiteSpace(ex.Detalle) ? "No se encontró la cuenta." : ex.Detalle,
        >= 400 and < 500 when ex.Operacion == "transferencia" =>
            string.IsNullOrWhiteSpace(ex.Detalle)
                ? $"No se pudo transferir (HTTP {ex.Codigo})."
                : $"No se pudo transferir: {ex.Detalle}",
        >= 400 and < 500 =>
            string.IsNullOrWhiteSpace(ex.Detalle) ? $"La API rechazó la operación (HTTP {ex.Codigo})." : ex.Detalle,
        >= 500 =>
            $"La API falló del lado del servidor (HTTP {ex.Codigo}). No se completó la operación. Intente más tarde.",
        _ => string.IsNullOrWhiteSpace(ex.Detalle) ? $"Error HTTP {ex.Codigo}." : ex.Detalle
    };

    private void MarcarError(string mensaje)
    {
        Tono = "error";
        Estado = mensaje;
    }

    private void LimpiarSaldos()
    {
        SaldoTexto = "—";
        SaldoRetenidoTexto = "—";
    }

    private void OlvidarToken()
    {
        _token = null;
        _rol = null;
        _usuarioDelToken = null;
    }

    private static string FormatoUvb(decimal valor) =>
        valor.ToString("N2", Cultura) + " UVB";

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
