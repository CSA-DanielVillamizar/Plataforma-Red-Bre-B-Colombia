using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// El único lugar que conoce rutas, JSON y el encabezado Bearer. Mide cada
/// llamada (estado, milisegundos y X-Trace-Id) y traduce cualquier fallo a un
/// ErrorApi con mensaje para el usuario.
/// </summary>
public sealed class ClienteBrebApi(HttpClient http, AlmacenToken almacen)
{
    public Medicion? Ultima { get; private set; }

    public async Task<RespuestaToken> IniciarSesionAsync(string usuario, string clave)
    {
        using var respuesta = await EnviarAsync(
            HttpMethod.Post, "/token", "POST /token",
            JsonContent.Create(new SolicitudToken(usuario, clave)),
            conToken: false);

        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>()
                    ?? throw new ErrorApi(TipoError.Servidor, "La API devolvió una respuesta vacía al iniciar sesión.");

        await almacen.GuardarAsync(usuario, token);
        return token;
    }

    public async Task<RespuestaSaldo> ConsultarSaldoAsync(Guid cuentaId)
    {
        using var respuesta = await EnviarAsync(
            HttpMethod.Get, $"/cuentas/{cuentaId}/saldo", "GET /saldo",
            cuerpo: null, conToken: true);

        return await respuesta.Content.ReadFromJsonAsync<RespuestaSaldo>()
               ?? throw new ErrorApi(TipoError.Servidor, "La API devolvió un saldo vacío.");
    }

    public async Task<RespuestaRetencion> RetenerAsync(Guid cuentaId, decimal montoUVB)
    {
        var monto = montoUVB.ToString(CultureInfo.InvariantCulture);
        using var respuesta = await EnviarAsync(
            HttpMethod.Post, $"/cuentas/{cuentaId}/retener?montoUVB={monto}", "POST /retener",
            cuerpo: null, conToken: true);

        return await respuesta.Content.ReadFromJsonAsync<RespuestaRetencion>()
               ?? throw new ErrorApi(TipoError.Servidor, "La API devolvió una retención vacía.");
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string ruta, string etiqueta, HttpContent? cuerpo, bool conToken)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = cuerpo };

        if (conToken)
        {
            var token = await almacen.ObtenerTokenAsync()
                        ?? throw new ErrorApi(TipoError.SesionExpirada, "Tu sesión expiró. Inicia sesión de nuevo.");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var reloj = Stopwatch.StartNew();
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(solicitud);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Servidor apagado, sin red o tiempo agotado (HttpClient.Timeout): para
            // el usuario es lo mismo, y no debe ver el texto técnico de la excepción.
            Ultima = new Medicion(etiqueta, 0, reloj.ElapsedMilliseconds, null);
            throw new ErrorApi(TipoError.SinConexion, "Sin conexión con la API. Verifica que esté encendida e inténtalo de nuevo.");
        }

        var traza = respuesta.Headers.TryGetValues("X-Trace-Id", out var valores) ? valores.FirstOrDefault() : null;
        Ultima = new Medicion(etiqueta, (int)respuesta.StatusCode, reloj.ElapsedMilliseconds, traza);

        if (respuesta.IsSuccessStatusCode)
            return respuesta;

        var codigo = respuesta.StatusCode;
        respuesta.Dispose();
        throw Traducir(codigo, esLogin: !conToken);
    }

    private static ErrorApi Traducir(HttpStatusCode codigo, bool esLogin) => codigo switch
    {
        // Mismo mensaje para usuario inexistente y clave equivocada: no regalamos pistas.
        HttpStatusCode.Unauthorized when esLogin =>
            new(TipoError.CredencialesRechazadas, "Inicio de sesión rechazado. Revisa tus credenciales."),
        HttpStatusCode.Unauthorized =>
            new(TipoError.SesionExpirada, "Tu sesión expiró. Inicia sesión de nuevo."),
        HttpStatusCode.Forbidden =>
            new(TipoError.SinPermiso, "Tu rol no tiene permiso para esta operación."),
        HttpStatusCode.NotFound =>
            new(TipoError.NoEncontrado, "La cuenta no existe."),
        _ =>
            new(TipoError.Servidor, $"La API no pudo procesar la solicitud (HTTP {(int)codigo}).")
    };
}
