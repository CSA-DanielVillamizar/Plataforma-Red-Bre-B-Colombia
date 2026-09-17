using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Breb.Cuentas.Seguridad;

/// <summary>
/// Nombres de las políticas de autorización (Semana 8, segunda sesión).
///
/// Autenticación responde "¿quién eres?" y falla con 401.
/// Autorización responde "¿puedes hacer esto?" y falla con 403.
/// </summary>
public static class Politicas
{
    /// <summary>Mover plata: retener fondos.</summary>
    public const string Operador = "Operador";

    /// <summary>
    /// Confirmar un abono. NO es trabajo de un operador humano: lo dice el
    /// core bancario del banco destino. Es una identidad de SERVICIO.
    /// </summary>
    public const string BancoDestino = "BancoDestino";

    /// <summary>Leer saldos: operador y consulta.</summary>
    public const string LecturaCuentas = "LecturaCuentas";
}

/// <summary>
/// Emite tokens. Ya no conoce la clave por una constante: la recibe al
/// construirse, y la clave llega desde la configuración.
/// </summary>
public sealed class EmisorTokens
{
    public const string Emisor    = "breb-auth";
    public const string Audiencia = "breb-api";
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(15);

    private readonly SigningCredentials _credenciales;

    public EmisorTokens(SymmetricSecurityKey claveActual) =>
        _credenciales = new SigningCredentials(claveActual, SecurityAlgorithms.HmacSha256);

    /// <summary>Identificador de la clave con la que firma (viaja como "kid").</summary>
    public string Kid => _credenciales.Key.KeyId;

    public (string token, DateTime expira) Emitir(string usuario, string rol)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.Add(Duracion);

        var afirmaciones = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario),
            new Claim(ClaimTypes.Role, rol),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // iat: cuándo se emitió. La Semana 8 lo midió ausente, y sin él no
            // se puede decir "rechazar todo token de este usuario emitido antes
            // de las 10:00" — la mitigación natural para un despido.
            new Claim(JwtRegisteredClaimNames.Iat,
                      EpochTime.GetIntDate(ahora).ToString(), ClaimValueTypes.Integer64)
        };

        var jwt = new JwtSecurityToken(Emisor, Audiencia, afirmaciones,
                                       notBefore: ahora, expires: expira,
                                       signingCredentials: _credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}

public static class Autenticacion
{
    /// <summary>
    /// La clave de DESARROLLO. Es pública a propósito: está en
    /// appsettings.Development.json, en el repositorio, para que cualquiera
    /// pueda clonar y correr el laboratorio sin configurar nada.
    ///
    /// Aparece aquí por una sola razón: para NEGARSE a arrancar si alguien la
    /// usa fuera de Development. No es un secreto; es una lista negra de uno.
    /// </summary>
    public const string ClaveDeDesarrollo = "clave-de-laboratorio-no-usar-en-produccion-32+";

    public static void AgregarAutenticacionJwt(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var ambiente = builder.Environment;

        // ── La clave sale de la CONFIGURACIÓN, no del código ─────────────────
        // IConfiguration la busca, en orden, en:
        //   appsettings.json → appsettings.{Ambiente}.json → user-secrets (solo
        //   Development) → variables de entorno (Jwt__ClaveFirma) → línea de comandos
        // El código no cambia entre ambientes: cambia de dónde sale el valor.
        var actual = CargarClave(config["Jwt:ClaveFirma"], "Jwt:ClaveFirma", ambiente, obligatoria: true)!;

        // ── Rotación sin sacar a nadie ───────────────────────────────────────
        // Fase 1: ClaveFirma = nueva, ClaveAnterior = vieja. Se firma con la
        //         nueva y se ACEPTAN las dos.
        // Fase 2: pasados 15 minutos (la vida de un token), se quita la anterior.
        var anterior = CargarClave(config["Jwt:ClaveAnterior"], "Jwt:ClaveAnterior", ambiente, obligatoria: false);

        var aceptadas = anterior is null
            ? new SecurityKey[] { actual }
            : new SecurityKey[] { actual, anterior };

        Console.WriteLine($"[JWT] Ambiente {ambiente.EnvironmentName} · firma con kid={actual.KeyId}" +
                          (anterior is null ? "" : $" · acepta también kid={anterior.KeyId}"));

        builder.Services.AddSingleton(new EmisorTokens(actual));
        builder.Services.AddSingleton<DirectorioUsuarios>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = aceptadas,      // varias, para rotar

                    ValidateIssuer = true,
                    ValidIssuer = EmisorTokens.Emisor,

                    ValidateAudience = true,
                    ValidAudience = EmisorTokens.Audiencia,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        // ── Autorización: quién puede hacer qué ──────────────────────────────
        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy(Politicas.Operador,       p => p.RequireRole("operador"));
            o.AddPolicy(Politicas.BancoDestino,   p => p.RequireRole("banco"));
            o.AddPolicy(Politicas.LecturaCuentas, p => p.RequireRole("operador", "consulta"));
        });
    }

    private static SymmetricSecurityKey? CargarClave(string? valor, string nombre,
                                                     IHostEnvironment ambiente, bool obligatoria)
    {
        // FALLAR AL ARRANCAR, no en la primera petición. Una API que arranca
        // "bien" sin clave y revienta al emitir el primer token es un incidente
        // a las 3 de la mañana.
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (!obligatoria) return null;
            throw new InvalidOperationException(
                $"Falta {nombre}. En Development viene de appsettings.Development.json. " +
                $"En cualquier otro ambiente debe llegar por variable de entorno " +
                $"({nombre.Replace(":", "__")}) o desde un gestor de secretos.");
        }

        var bytes = Encoding.UTF8.GetBytes(valor);

        if (bytes.Length < 32)
            throw new InvalidOperationException(
                $"{nombre} tiene {bytes.Length * 8} bits. HS256 exige 256 o más.");

        if (!ambiente.IsDevelopment() && valor == ClaveDeDesarrollo)
            throw new InvalidOperationException(
                $"{nombre} es la clave de desarrollo, publicada en el repositorio. " +
                $"Fuera de Development no se acepta.");

        return new SymmetricSecurityKey(bytes) { KeyId = Kid(bytes) };
    }

    /// <summary>
    /// kid = primeros 8 hex del SHA-256 de la clave. Identifica la clave sin
    /// revelarla, y no hace falta inventar ni configurar un nombre aparte.
    /// </summary>
    private static string Kid(byte[] clave) =>
        Convert.ToHexString(SHA256.HashData(clave))[..8].ToLowerInvariant();
}
