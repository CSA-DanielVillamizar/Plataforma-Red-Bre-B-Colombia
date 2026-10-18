using System.Net;

namespace Breb.App.Tests.Apoyo;

internal sealed class HandlerFalso : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _pasos = new();
    public List<SolicitudVista> Solicitudes { get; } = new();
    public Exception? Falla { get; set; }

    public void Encolar(HttpStatusCode codigo, string cuerpo = "", string? contentType = "application/json")
    {
        _pasos.Enqueue(_ => Task.FromResult(Respuesta(codigo, cuerpo, contentType)));
    }

    public void Encolar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _pasos.Enqueue(req => Task.FromResult(responder(req)));
    }

    public void EncolarEspera(TaskCompletionSource<HttpResponseMessage> puerta)
    {
        _pasos.Enqueue(_ => puerta.Task);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Solicitudes.Add(new SolicitudVista(
            request.Method,
            request.RequestUri?.PathAndQuery ?? "",
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            request.Headers.Contains("Idempotency-Key"),
            cuerpo));

        if (Falla is not null)
            throw Falla;

        if (_pasos.Count == 0)
            throw new InvalidOperationException($"No quedaban respuestas falsas para {request.RequestUri}");

        return await _pasos.Dequeue()(request);
    }

    internal static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo, string? contentType)
    {
        var mensaje = new HttpResponseMessage(codigo)
        {
            Content = new StringContent(cuerpo)
        };
        if (contentType is not null)
            mensaje.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return mensaje;
    }
}

internal sealed record SolicitudVista(
    HttpMethod Metodo,
    string Ruta,
    string? Esquema,
    string? Token,
    bool TraeClaveDeIdempotencia,
    string? Cuerpo);

internal static class ClienteFalso
{
    public static (Breb.App.Servicios.ClienteBrebApi Api, HandlerFalso Handler) Crear(HandlerFalso? handler = null)
    {
        handler ??= new HandlerFalso();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://10.0.2.2:5080") };
        return (new Breb.App.Servicios.ClienteBrebApi(http), handler);
    }
}
