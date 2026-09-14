using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Breb.Cuentas.Seguridad;

/// <summary>
/// Autenticacion con JWT para la API de la Red Bre-B (Semana 8).
///
/// LO PRIMERO QUE HAY QUE ENTENDER, Y CASI TODO EL MUNDO SE EQUIVOCA:
/// un JWT NO ESTA CIFRADO. Esta FIRMADO. Son cosas distintas.
///
///   Cifrado  -> nadie puede LEER el contenido sin la clave
///   Firmado  -> cualquiera puede LEER el contenido, pero nadie puede
///               MODIFICARLO sin que se note
///
/// El cuerpo de un JWT es Base64Url. Se decodifica con una linea de codigo,
/// sin ninguna clave. Cualquiera que intercepte el token —o cualquiera que
/// abra las herramientas del navegador— lee todo lo que ustedes metan ahi.
///
/// CONSECUENCIA PRACTICA: nunca poner en un JWT nada que no pueda ser publico.
/// Ni cedulas, ni saldos, ni numeros de cuenta, ni correos.
/// </summary>
public static class Autenticacion
{
    /// <summary>
    /// La clave con la que se firma. En este laboratorio esta en el codigo
    /// para que la clase sea reproducible; EN PRODUCCION ESTO ES UN DEFECTO
    /// GRAVE — va en un gestor de secretos, nunca en el repositorio.
    ///
    /// HS256 exige minimo 256 bits (32 caracteres). Con menos, la libreria
    /// se niega a firmar.
    /// </summary>
    public const string ClaveFirma = "clave-de-laboratorio-no-usar-en-produccion-32+";

    public const string Emisor    = "breb-auth";
    public const string Audiencia = "breb-api";

    /// <summary>Cuanto vive el token. Corto a proposito: ver la nota abajo.</summary>
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(15);

    public static void AgregarAutenticacionJwt(this IServiceCollection servicios)
    {
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveFirma));

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                // Cada una de estas validaciones responde a un ataque concreto.
                // Desactivar cualquiera abre una puerta.
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    // ¿La firma es valida? -> impide que alguien MODIFIQUE el token
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = clave,

                    // ¿Quien lo emitio? -> impide aceptar tokens de otro sistema
                    ValidateIssuer = true,
                    ValidIssuer = Emisor,

                    // ¿Para quien es? -> impide reusar en esta API un token
                    //                    emitido para OTRO servicio del mismo emisor
                    ValidateAudience = true,
                    ValidAudience = Audiencia,

                    // ¿Ya vencio? -> limita la ventana de un token robado
                    ValidateLifetime = true,

                    // Por defecto .NET regala 5 minutos de gracia al vencimiento.
                    // Con tokens de 15 minutos, eso es un 33 % de vida extra
                    // regalada. Se pone en cero para que "vencido" signifique
                    // vencido.
                    ClockSkew = TimeSpan.Zero
                };
            });

        servicios.AddAuthorization();
    }

    /// <summary>
    /// Emite un token. En produccion esto vive en un servicio de identidad
    /// aparte, no dentro de la API que protege — aqui esta junto para que la
    /// clase quepa en una sesion.
    /// </summary>
    public static (string token, DateTime expira) EmitirToken(string usuario, string rol)
    {
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClaveFirma));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.Add(Duracion);

        var afirmaciones = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario),
            new Claim(ClaimTypes.Role, rol),

            // jti: identificador unico del token. Es lo que permite revocarlo
            // uno por uno si hiciera falta — ver la nota sobre revocacion.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var jwt = new JwtSecurityToken(
            issuer: Emisor,
            audience: Audiencia,
            claims: afirmaciones,
            expires: expira,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }
}

// ─────────────────────────────────────────────────────────────────────────
//  LA LIMITACION QUE HAY QUE DECIR EN VOZ ALTA:
//
//  Un JWT NO SE PUEDE REVOCAR. Una vez emitido, es valido hasta que venza,
//  aunque el usuario cierre sesion, aunque lo despidan, aunque se den cuenta
//  de que se lo robaron.
//
//  Eso es EL PRECIO de no consultar la base en cada peticion — que es
//  justamente la razon por la que se usa JWT en sistemas distribuidos: cada
//  servicio valida el token por su cuenta, sin preguntarle a nadie.
//
//  Por eso la duracion es corta. Quince minutos no es paranoia: es el tamaño
//  de la ventana en la que un token robado sigue sirviendo.
//
//  Quien necesite revocacion inmediata tiene que agregar una lista de
//  revocados consultada en cada peticion — y con eso pierde exactamente la
//  ventaja por la que eligio JWT.
// ─────────────────────────────────────────────────────────────────────────
