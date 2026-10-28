using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Modelos;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Clase 18: la pantalla de transferencia, con cola local persistente.
/// <para>
/// Lo que hay que mirar de este ViewModel no es que llame a la API, sino el
/// ORDEN en que hace tres cosas: generar la llave, escribirla en disco, y
/// sólo entonces salir a la red. Ese orden es la diferencia entre la app que
/// duplica la plata o la pierde, y la que no.
/// </para>
/// <para>
/// Las dos reglas que gobiernan todo lo de abajo:
/// </para>
/// <list type="number">
///   <item>La llave nace al tocar el botón, antes de la red, y se reusa.</item>
///   <item>La operación sale de la cola <b>sólo</b> cuando el servidor confirmó.</item>
/// </list>
/// </summary>
public partial class TransferenciaViewModel : ObservableObject
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    // Las mismas dos cuentas de scripts/cuentas-base.sql.
    private static readonly Guid CuentaOrigen = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ClienteBrebApi _api;
    private readonly ColaOperaciones _cola;
    private string? _token;

    public TransferenciaViewModel(ClienteBrebApi api, ColaOperaciones cola)
    {
        _api = api;
        _cola = cola;

        // Retener fondos exige rol operador desde la Semana 8. Con
        // luis.consulta la respuesta es 403, no 401: el servidor sabe
        // quién es y le dice que no puede.
        Usuario = "ana.operadora";
        Clave = "Operadora-2026";
        Monto = "100";
    }

    [ObservableProperty]
    private string usuario = string.Empty;

    [ObservableProperty]
    private string clave = string.Empty;

    [ObservableProperty]
    private string monto = string.Empty;

    [ObservableProperty]
    private string estado = "Sin enviar";

    [ObservableProperty]
    private string llaveTexto = "—";

    [ObservableProperty]
    private string resultado = "—";

    [ObservableProperty]
    private string saldoTexto = "—";

    [ObservableProperty]
    private string pendientesTexto = "Sin operaciones pendientes";

    [ObservableProperty]
    private bool enviando;

    [ObservableProperty]
    private bool hayPendientes;

    public bool NoHayPendientes => !HayPendientes;

    partial void OnHayPendientesChanged(bool value) => OnPropertyChanged(nameof(NoHayPendientes));

    /// <summary>
    /// Se llama al aparecer la pantalla. Dos cosas, en este orden:
    /// mostrar lo que quedó pendiente de una sesión anterior, y suscribirse
    /// a los cambios de conectividad para reenviar cuando vuelva la red.
    /// </summary>
    [RelayCommand]
    private async Task AparecerAsync()
    {
        Connectivity.Current.ConnectivityChanged -= AlCambiarLaRed;
        Connectivity.Current.ConnectivityChanged += AlCambiarLaRed;

        await RefrescarPendientesAsync();

        var pendientes = await _cola.PendientesAsync();
        if (pendientes.Count > 0)
            Estado = $"Quedaron {pendientes.Count} operación(es) de una sesión anterior. " +
                     "Se reenvían solas cuando haya red.";
    }

    /// <summary>Al desaparecer la pantalla se suelta el suscriptor.</summary>
    [RelayCommand]
    private void Desaparecer() => Connectivity.Current.ConnectivityChanged -= AlCambiarLaRed;

    // Cuando vuelve la red, reenviar lo pendiente. Esto es el equivalente en
    // el cliente del conciliador del Issue #46: algo que corre por su cuenta
    // y termina lo que quedó a medias.
    private void AlCambiarLaRed(object? remitente, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
            _ = ReintentarPendientesAsync();
    }

    [RelayCommand]
    private async Task TransferirAsync()
    {
        if (Enviando) return;

        if (!decimal.TryParse(Monto, NumberStyles.Number, Colombia, out var montoUVB) || montoUVB <= 0)
        {
            Estado = "El monto tiene que ser un número mayor que cero.";
            return;
        }

        // ── Paso 1: la llave nace AQUI, antes de todo ───────────────────────
        // Antes de la red y antes del disco. Si se generara al reintentar,
        // cada intento inventaría una identidad nueva y el servidor no
        // tendría forma de saber que son el mismo movimiento: volvería el
        // duplicado que medimos el 26 de octubre.
        var op = new OperacionPendiente(Guid.NewGuid(), CuentaOrigen, montoUVB, DateTime.UtcNow);

        // ── Paso 2: a disco, ANTES de intentar la red ───────────────────────
        // Éste es el paso que convierte la app en una que no pierde la
        // intención del usuario. Si la app se cierra, se queda sin batería o
        // la mata el sistema entre este punto y la respuesta del servidor,
        // la operación ya está a salvo y se reenviará al abrir de nuevo.
        //
        // Guardar DESPUÉS de la llamada no serviría: el hueco peligroso es
        // justamente el de la llamada.
        await _cola.EncolarAsync(op);
        LlaveTexto = op.Llave.ToString();
        await RefrescarPendientesAsync();

        // ── Paso 3: ahora sí, la red ────────────────────────────────────────
        await EnviarAsync(op, esReintento: false);
    }

    /// <summary>
    /// Reenvía todo lo que está en la cola, de lo más viejo a lo más nuevo.
    /// Lo llama el botón de reintentar y también el evento de conectividad.
    /// </summary>
    [RelayCommand]
    private async Task ReintentarPendientesAsync()
    {
        if (Enviando) return;

        var pendientes = await _cola.PendientesAsync();
        if (pendientes.Count == 0)
        {
            Estado = "No hay nada pendiente.";
            return;
        }

        foreach (var op in pendientes)
        {
            LlaveTexto = op.Llave.ToString();
            await EnviarAsync(op, esReintento: true);
        }
    }

    // El envío de UNA operación. Reintento y primer intento son el mismo
    // código a propósito: para el servidor son indistinguibles, porque
    // llevan la misma llave. Si hicieran cosas distintas, la diferencia
    // sería una fuente de bugs sin ninguna ganancia.
    private async Task EnviarAsync(OperacionPendiente op, bool esReintento)
    {
        Enviando = true;
        try
        {
            Estado = esReintento ? "Reenviando operación pendiente…" : "Pidiendo token…";

            var token = await _api.ObtenerTokenAsync(Usuario, Clave);
            _token = token.Token;

            Estado = "Enviando transferencia…";
            var r = await _api.RetenerAsync(op.CuentaId, op.MontoUVB, op.Llave, _token);

            Resultado = r.Repetida
                ? $"Ya estaba hecha. El servidor reconoció la llave y devolvió la retención " +
                  $"original de {r.MontoUVB.ToString("N2", Colombia)} UVB."
                : $"Transferencia aceptada por {r.MontoUVB.ToString("N2", Colombia)} UVB.";

            // ── El servidor confirmó: recién AHORA sale de la cola ──────────
            // Da igual si respondió repetida=true o repetida=false: las dos
            // significan "esto ya está hecho". Por eso el endpoint devuelve
            // 200 y no 409 — un 409 lo leeríamos como fallo, no borraríamos
            // la entrada, y la operación quedaría atrapada reintentándose
            // para siempre algo que ya estaba hecho.
            await _cola.ConfirmarAsync(op.Llave);
            await RefrescarPendientesAsync();

            // La transferencia NO terminó: los fondos quedaron RETENIDOS.
            // Falta que el banco destino confirme el abono. Si no lo hace en
            // 15 segundos, la saga de la Semana 3 compensa y el saldo vuelve.
            var saldo = await _api.ConsultarSaldoAsync(op.CuentaId, _token);
            SaldoTexto = $"{saldo.SaldoDisponible.ToString("N2", Colombia)} UVB disponibles · " +
                         $"{saldo.SaldoRetenido.ToString("N2", Colombia)} retenidos";
            Estado = "Esperando confirmación del banco destino…";
        }
        catch (HttpRequestException)
        {
            // Sin red. La operación SE QUEDA en la cola, en disco.
            await _cola.RegistrarIntentoAsync(op.Llave);
            await RefrescarPendientesAsync();
            Estado = "Sin conexión. La operación quedó guardada y se reenviará " +
                     "con la misma llave cuando vuelva la red.";
        }
        catch (TaskCanceledException)
        {
            // Timeout: el caso MÁS peligroso. No sabemos si el servidor
            // alcanzó a procesarla. Precisamente para esto existe la llave:
            // reintentar es seguro porque el servidor reconocerá la repetida.
            await _cola.RegistrarIntentoAsync(op.Llave);
            await RefrescarPendientesAsync();
            Estado = "Se agotó el tiempo de espera. No sabemos si llegó; queda " +
                     "pendiente y reintentar es seguro.";
        }
        catch (Exception ex)
        {
            // Rechazo del servidor que NO es un problema de red: clave mala,
            // rol equivocado, monto inválido. Reintentar no lo va a arreglar,
            // así que la operación sale de la cola y se le dice al usuario.
            // Dejarla encolada sería reintentar para siempre algo que el
            // servidor nunca va a aceptar.
            await _cola.DescartarAsync(op.Llave);
            await RefrescarPendientesAsync();
            Estado = ex.Message;
            Resultado = "La operación se descartó: reintentarla daría el mismo error.";
        }
        finally
        {
            Enviando = false;
        }
    }

    /// <summary>Descarta TODO lo pendiente, porque el usuario lo pidió.</summary>
    [RelayCommand]
    private async Task DescartarPendientesAsync()
    {
        foreach (var op in await _cola.PendientesAsync())
            await _cola.DescartarAsync(op.Llave);

        await RefrescarPendientesAsync();
        LlaveTexto = "—";
        Estado = "Operaciones pendientes descartadas.";
        Resultado = "—";
    }

    private async Task RefrescarPendientesAsync()
    {
        var pendientes = await _cola.PendientesAsync();
        HayPendientes = pendientes.Count > 0;

        PendientesTexto = pendientes.Count switch
        {
            0 => "Sin operaciones pendientes",
            1 => $"1 operación pendiente · {pendientes[0].MontoUVB.ToString("N2", Colombia)} UVB · " +
                 $"{pendientes[0].Intentos} intento(s)",
            _ => $"{pendientes.Count} operaciones pendientes · " +
                 $"{pendientes.Sum(o => o.MontoUVB).ToString("N2", Colombia)} UVB en total"
        };
    }
}
