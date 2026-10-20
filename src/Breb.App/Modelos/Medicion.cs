namespace Breb.App.Modelos;

/// <summary>
/// Lo que la app sabe de su última llamada a la API: qué endpoint, qué estado
/// HTTP, cuánto tardó y el TraceId que el servidor devuelve en X-Trace-Id.
/// Ese mismo TraceId aparece como traza=... en el log del servidor y en Jaeger.
/// </summary>
public sealed record Medicion(string Llamada, int EstadoHttp, long Milisegundos, string? TrazaId)
{
    public override string ToString()
    {
        var estado = EstadoHttp == 0 ? "sin respuesta" : $"HTTP {EstadoHttp}";
        return $"{Llamada} · {estado} · {Milisegundos} ms\ntraza {TrazaId ?? "—"}";
    }
}
