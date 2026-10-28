using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Breb.App.Servicios;

namespace Breb.App.ViewModels;

/// <summary>
/// Clase 18: la pantalla de transferencia.
/// <para>
/// Lo que hay que mirar de este ViewModel no es que llame a la API, sino
/// DÓNDE y CUÁNDO nace <see cref="_llavePendiente"/>. Es la diferencia entre
/// la app que duplica la plata y la que no.
/// </para>
/// </summary>
public partial class TransferenciaViewModel : ObservableObject
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    // Las mismas dos cuentas de scripts/cuentas-base.sql.
    private static readonly Guid CuentaOrigen = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ClienteBrebApi _api;
    private string? _token;

    /// <summary>
    /// La llave de idempotencia de la operación EN CURSO.
    /// <para>
    /// Esta variable es toda la lección de hoy. Se llena cuando el usuario
    /// toca el botón —antes de que salga el primer byte a la red— y NO se
    /// borra si la llamada falla. Por eso, cuando el usuario vuelve a tocar
    /// después de un error de red, se reenvía la MISMA llave y el servidor
    /// responde <c>repetida=true</c> en vez de mover la plata otra vez.
    /// </para>
    /// <para>
    /// Se borra en un solo caso: cuando el servidor confirmó. Ese es el
    /// mismo criterio que usa una cola local persistente para sacar una
    /// operación de la cola, y es el 30 % de la rúbrica del E4.
    /// </para>
    /// </summary>
    private Guid? _llavePendiente;

    public TransferenciaViewModel(ClienteBrebApi api)
    {
        _api = api;

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
    private bool enviando;

    /// <summary>
    /// true cuando hay una operación con llave viva que no ha sido
    /// confirmada. La vista lo usa para cambiar el texto del botón de
    /// "Transferir" a "Reintentar", que es información honesta: no es una
    /// transferencia nueva, es la misma.
    /// </summary>
    public bool HayPendiente => _llavePendiente is not null;

    /// <summary>Lo contrario de <see cref="HayPendiente"/>, para que el XAML
    /// no necesite un convertidor sólo para negar un booleano.</summary>
    public bool NoHayPendiente => _llavePendiente is null;

    [RelayCommand]
    private async Task TransferirAsync()
    {
        if (Enviando) return;

        if (!decimal.TryParse(Monto, NumberStyles.Number, Colombia, out var montoUVB) || montoUVB <= 0)
        {
            Estado = "El monto tiene que ser un número mayor que cero.";
            return;
        }

        // ── AQUÍ nace la llave, y este orden es el que importa ──────────────
        // Se genera ANTES de la llamada, no al reintentar. Si se generara en
        // el reintento, cada toque inventaría una identidad nueva y el
        // servidor no tendría forma de saber que son el mismo movimiento:
        // volvería exactamente el duplicado que medimos el 26 de octubre.
        _llavePendiente ??= Guid.NewGuid();
        var llave = _llavePendiente.Value;

        LlaveTexto = llave.ToString();
        NotificarPendiente();

        Enviando = true;
        try
        {
            Estado = "Pidiendo token…";
            var token = await _api.ObtenerTokenAsync(Usuario, Clave);
            _token = token.Token;

            Estado = "Enviando transferencia…";
            var retencion = await _api.RetenerAsync(CuentaOrigen, montoUVB, llave, _token);

            Resultado = retencion.Repetida
                ? $"Ya estaba hecha. El servidor reconoció la llave y devolvió " +
                  $"la retención original de {retencion.MontoUVB.ToString("N2", Colombia)} UVB."
                : $"Transferencia aceptada por {retencion.MontoUVB.ToString("N2", Colombia)} UVB.";

            // El servidor confirmó: la operación sale de la cola. Esta línea
            // es la razón por la que el endpoint responde 200 y no 409 — un
            // 409 dejaría la llave viva para siempre, reintentando algo que
            // ya estaba hecho.
            _llavePendiente = null;
            NotificarPendiente();

            // La transferencia NO terminó: los fondos quedaron RETENIDOS.
            // Falta que el banco destino confirme el abono. Si no lo hace en
            // 15 segundos, la saga de la Semana 3 compensa y el saldo vuelve.
            // Eso es consistencia eventual, y en una pantalla se ve así:
            var saldo = await _api.ConsultarSaldoAsync(CuentaOrigen, _token);
            SaldoTexto = $"{saldo.SaldoDisponible.ToString("N2", Colombia)} UVB disponibles · " +
                         $"{saldo.SaldoRetenido.ToString("N2", Colombia)} retenidos";
            Estado = "Esperando confirmación del banco destino…";
        }
        catch (HttpRequestException ex)
        {
            // Sin red. La llave NO se borra: queda viva para el reintento.
            // Decirle la verdad al usuario es el 10 % de la rúbrica del E3 y
            // la mitad del comportamiento offline del E4.
            Estado = "Sin conexión. La operación quedó pendiente y se " +
                     "reenviará con la misma llave.";
            Resultado = ex.Message;
            NotificarPendiente();
        }
        catch (TaskCanceledException)
        {
            // Timeout: el caso MÁS peligroso. No sabemos si el servidor
            // alcanzó a procesarla. Precisamente para esto existe la llave:
            // reintentar es seguro porque el servidor reconocerá la repetida.
            Estado = "Se agotó el tiempo de espera. No sabemos si llegó: " +
                     "reintente, la llave hace que sea seguro.";
            NotificarPendiente();
        }
        catch (Exception ex)
        {
            Estado = ex.Message;
            NotificarPendiente();
        }
        finally
        {
            Enviando = false;
        }
    }

    /// <summary>
    /// Descarta la operación pendiente sin enviarla. Existe para que el
    /// usuario no quede atrapado con una llave viva que no quiere reenviar.
    /// </summary>
    /// <summary>Avisa a la vista de los dos lados del mismo hecho.</summary>
    private void NotificarPendiente()
    {
        OnPropertyChanged(nameof(HayPendiente));
        OnPropertyChanged(nameof(NoHayPendiente));
    }

    [RelayCommand]
    private void Descartar()
    {
        _llavePendiente = null;
        LlaveTexto = "—";
        Estado = "Operación descartada.";
        Resultado = "—";
        NotificarPendiente();
    }
}
