using System.Text.Json;

namespace Breb.App.Servicios;

/// <summary>
/// La API contestó, pero rechazó la operación. El código viaja aparte del
/// texto para que la pantalla distinga sesión vencida (401), falta de permiso
/// (403), validación (400) y fallo del servidor (5xx).
/// </summary>
public sealed class ExcepcionDeApi : Exception
{
    public int Codigo { get; }

    /// <summary>"login", "saldo" o "transferencia". Cambia el texto que ve la persona.</summary>
    public string Operacion { get; }

    public string Detalle { get; }

    public ExcepcionDeApi(int codigo, string operacion, string detalle)
        : base(string.IsNullOrWhiteSpace(detalle) ? $"HTTP {codigo}" : detalle)
    {
        Codigo = codigo;
        Operacion = operacion;
        Detalle = detalle ?? string.Empty;
    }

    public static async Task<ExcepcionDeApi> DesdeAsync(HttpResponseMessage respuesta, string operacion)
    {
        var crudo = await respuesta.Content.ReadAsStringAsync();
        return new ExcepcionDeApi((int)respuesta.StatusCode, operacion, Interpretar(crudo));
    }

    /// <summary>
    /// Saca una frase legible del cuerpo. BadRequest(string) llega como JSON
    /// entre comillas ("Saldo insuficiente…"); un ProblemDetails trae title
    /// y errors. Si no es JSON, se muestra el texto tal cual, recortado.
    /// </summary>
    public static string Interpretar(string? crudo)
    {
        var texto = (crudo ?? string.Empty).Trim();
        if (texto.Length == 0) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(texto);
            var raiz = doc.RootElement;

            if (raiz.ValueKind == JsonValueKind.String)
                return Recortar(raiz.GetString());

            if (raiz.ValueKind == JsonValueKind.Object)
            {
                var errores = FrasesDeErrores(raiz);
                if (errores.Count > 0)
                    return Recortar(string.Join(" ", errores));

                foreach (var nombre in new[] { "detail", "title", "mensaje", "message" })
                {
                    if (raiz.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String)
                    {
                        var frase = valor.GetString();
                        if (!string.IsNullOrWhiteSpace(frase))
                            return Recortar(frase);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Cuerpo de texto plano o una página HTML de un proxy.
        }

        return Recortar(texto.Trim('"'));
    }

    private static List<string> FrasesDeErrores(JsonElement raiz)
    {
        var frases = new List<string>();
        if (!raiz.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return frases;

        foreach (var propiedad in errors.EnumerateObject())
        {
            if (propiedad.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in propiedad.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    frases.Add(item.GetString()!);
            }
        }

        return frases;
    }

    private static string Recortar(string? frase)
    {
        var texto = (frase ?? string.Empty).Trim();
        return texto.Length <= 300 ? texto : texto[..300];
    }
}
