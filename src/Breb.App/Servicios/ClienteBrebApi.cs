using System.Globalization;
using System.Net.Http.Json;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Envuelve el HttpClient registrado en MauiProgram.cs. Un solo lugar que
/// sabe hablar con Breb.Cuentas; el ViewModel no conoce rutas ni JSON.
/// </summary>
public sealed class ClienteBrebApi(HttpClient http)
{
    public async Task<RespuestaToken> ObtenerTokenAsync(string usuario, string clave)
    {
        var respuesta = await http.PostAsJsonAsync("/token", new SolicitudToken(usuario, clave));

        // 401 aquí es un camino esperado (clave mala), no una excepción de red.
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Login rechazado: HTTP {(int)respuesta.StatusCode}");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaToken>()
               ?? throw new InvalidOperationException("Respuesta de /token vacía.");
    }

    public async Task<RespuestaSaldo> ConsultarSaldoAsync(Guid cuentaId, string token)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"/cuentas/{cuentaId}/saldo");
        solicitud.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Saldo rechazado: HTTP {(int)respuesta.StatusCode}");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaSaldo>()
               ?? throw new InvalidOperationException("Respuesta de /saldo vacía.");
    }

    /// <summary>
    /// Clase 18: dispara la transferencia reteniendo los fondos.
    /// <para>
    /// La <paramref name="transferenciaId"/> NO es opcional aquí a propósito.
    /// El endpoint la acepta como opcional para no romper llamadores viejos,
    /// pero esta app siempre la manda: si la dejara en nulo, el servidor
    /// inventaría una nueva en cada reintento y volvería el duplicado de la
    /// Semana 14. Quien decide la identidad de la operación es el cliente,
    /// porque es donde nace la intención del usuario.
    /// </para>
    /// </summary>
    public async Task<RespuestaRetencion> RetenerAsync(
        Guid cuentaId, decimal montoUVB, Guid transferenciaId, string token)
    {
        var ruta = $"/cuentas/{cuentaId}/retener?montoUVB={montoUVB.ToString(CultureInfo.InvariantCulture)}" +
                   $"&transferenciaId={transferenciaId}";

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, ruta);
        solicitud.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var respuesta = await http.SendAsync(solicitud);

        // 403 es el camino esperado si el token no es de rol operador
        // (Semana 8). Se distingue del resto porque el mensaje tiene que
        // decirle al usuario algo accionable, no "error 403".
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException(
                "Este usuario no puede retener fondos: hace falta el rol operador.");

        if (!respuesta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Retención rechazada: HTTP {(int)respuesta.StatusCode}");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaRetencion>()
               ?? throw new InvalidOperationException("Respuesta de /retener vacía.");
    }
}
