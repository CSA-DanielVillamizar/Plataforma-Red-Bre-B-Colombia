using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Error que el usuario puede leer. Todo fallo de red o de la API sale de
/// aquí convertido en una de estas, así los ViewModels solo atrapan una.
/// </summary>
public sealed class ErrorApi(string mensaje, bool sesionVencida = false) : Exception(mensaje)
{
    public bool SesionVencida { get; } = sesionVencida;
}

/// <summary>
/// Envuelve el HttpClient registrado en MauiProgram.cs. Un solo lugar que
/// sabe hablar con Breb.Cuentas; los ViewModels no conocen rutas, JSON ni el token.
/// </summary>
public sealed class ClienteBrebApi(HttpClient http, SesionSegura sesion)
{
    public async Task<RespuestaToken> ObtenerTokenAsync(string usuario, string clave)
    {
        var respuesta = await EnviarAsync(() =>
            http.PostAsJsonAsync("/token", new SolicitudToken(usuario, clave)));

        // 401 aquí es un camino esperado (clave mala), no una sesión vencida.
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            throw new ErrorApi("Usuario o clave incorrectos.");

        return await LeerAsync<RespuestaToken>(respuesta, "/token");
    }

    public async Task<RespuestaSaldo> ConsultarSaldoAsync(Guid cuentaId)
    {
        using var solicitud = await ConTokenAsync(HttpMethod.Get, $"/cuentas/{cuentaId}/saldo");
        var respuesta = await EnviarAsync(() => http.SendAsync(solicitud));
        return await LeerAsync<RespuestaSaldo>(respuesta, "/saldo");
    }

    public async Task<RespuestaRetencion> TransferirAsync(Guid cuentaOrigen, decimal montoUVB)
    {
        // La API recibe el monto en la query string: siempre con punto decimal.
        var monto = montoUVB.ToString(CultureInfo.InvariantCulture);
        using var solicitud = await ConTokenAsync(HttpMethod.Post, $"/cuentas/{cuentaOrigen}/retener?montoUVB={monto}");
        var respuesta = await EnviarAsync(() => http.SendAsync(solicitud));
        return await LeerAsync<RespuestaRetencion>(respuesta, "/retener");
    }

    private async Task<HttpRequestMessage> ConTokenAsync(HttpMethod metodo, string ruta)
    {
        var token = await sesion.ObtenerTokenAsync()
                    ?? throw new ErrorApi("La sesión expiró. Inicie sesión de nuevo.", sesionVencida: true);

        var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    private async Task<HttpResponseMessage> EnviarAsync(Func<Task<HttpResponseMessage>> enviar)
    {
        try
        {
            return await enviar();
        }
        catch (HttpRequestException ex)
        {
            // Lo que aparece si la API está apagada, o si la URL dice
            // localhost en vez de 10.0.2.2 (Clase 14, trampa 3).
            throw new ErrorApi($"Sin conexión con la API ({http.BaseAddress}). {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            // El Timeout de 10 s de MauiProgram.cs se manifiesta así, no como HttpRequestException.
            throw new ErrorApi($"La API no respondió en 10 s ({http.BaseAddress}).");
        }
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, string ruta)
    {
        if (!respuesta.IsSuccessStatusCode)
        {
            var codigo = (int)respuesta.StatusCode;
            throw respuesta.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new ErrorApi("La API rechazó el token (401). Inicie sesión de nuevo.", sesionVencida: true),
                HttpStatusCode.Forbidden => new ErrorApi("Su rol no tiene permiso para esta operación (403)."),
                HttpStatusCode.NotFound => new ErrorApi("La cuenta no existe (404)."),
                _ => new ErrorApi($"La API respondió HTTP {codigo} en {ruta}. Revise la ventana de la API.")
            };
        }

        return await respuesta.Content.ReadFromJsonAsync<T>()
               ?? throw new ErrorApi($"Respuesta vacía de {ruta}.");
    }
}
