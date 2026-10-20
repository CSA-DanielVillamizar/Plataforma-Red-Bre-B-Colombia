using System.Text;
using System.Text.Json;

namespace Breb.App.Servicios;

/// <summary>
/// Lee la fecha "exp" del JWT sin comprobar la firma. Sirve para no abrir
/// Saldo con un token ya vencido. Quien decide de verdad sigue siendo la API:
/// si el token no sirve, responde 401.
/// </summary>
public static class VigenciaDelToken
{
    public static bool SigueVigente(string? token, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        var partes = token.Split('.');
        if (partes.Length != 3 || partes.Any(string.IsNullOrEmpty)) return false;

        try
        {
            var json = Encoding.UTF8.GetString(Base64Url(partes[1]));
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var exp)) return false;
            if (!TrySegundos(exp, out var segundos)) return false;
            return DateTimeOffset.FromUnixTimeSeconds(segundos) > ahora;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TrySegundos(JsonElement exp, out long segundos)
    {
        if (exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out segundos))
            return true;
        if (exp.ValueKind == JsonValueKind.String && long.TryParse(exp.GetString(), out segundos))
            return true;
        segundos = 0;
        return false;
    }

    private static byte[] Base64Url(string texto)
    {
        var s = texto.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            0 => s,
            _ => throw new FormatException()
        };
        return Convert.FromBase64String(s);
    }
}
