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
}
