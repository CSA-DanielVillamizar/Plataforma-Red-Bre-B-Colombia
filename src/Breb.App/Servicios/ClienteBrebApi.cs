using System.Globalization;
using System.Net.Http.Headers;
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
        using var respuesta = await http.PostAsJsonAsync("/token", new SolicitudToken(usuario, clave));

        // 401 aquí es un camino esperado (clave mala), no una excepción de red.
        if (!respuesta.IsSuccessStatusCode)
            throw await ExcepcionDeApi.DesdeAsync(respuesta, "login");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaToken>()
               ?? throw new InvalidOperationException("Respuesta de /token vacía.");
    }

    public async Task<RespuestaSaldo> ConsultarSaldoAsync(Guid cuentaId, string token)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"/cuentas/{cuentaId}/saldo");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
            throw await ExcepcionDeApi.DesdeAsync(respuesta, "saldo");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaSaldo>()
               ?? throw new InvalidOperationException("Respuesta de /saldo vacía.");
    }

    /// <summary>
    /// Dispara la transferencia: POST /cuentas/{id}/retener?montoUVB=.
    ///
    /// Contrato real de Breb.Cuentas:
    ///   · el monto va en la query, no en el cuerpo;
    ///   · la política es Operador, o sea el claim de rol "operador"
    ///     (ana.operadora). Un token "consulta" responde 403, no 401;
    ///   · no hay clave de idempotencia. El servidor inventa el
    ///     transferenciaId. Llamar dos veces aparta el dinero dos veces.
    ///     La idempotencia desde el cliente es de la semana 14.
    /// </summary>
    public async Task<RespuestaRetencion> RetenerAsync(Guid cuentaId, decimal montoUvb, string token)
    {
        var monto = Uri.EscapeDataString(montoUvb.ToString(CultureInfo.InvariantCulture));
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            $"/cuentas/{cuentaId}/retener?montoUVB={monto}");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
            throw await ExcepcionDeApi.DesdeAsync(respuesta, "transferencia");

        return await respuesta.Content.ReadFromJsonAsync<RespuestaRetencion>()
               ?? throw new InvalidOperationException("Respuesta de /retener vacía.");
    }
}
