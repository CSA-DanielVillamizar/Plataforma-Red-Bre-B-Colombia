using System.Security.Cryptography;

namespace Breb.Cuentas.Seguridad;

/// <summary>
/// Directorio de usuarios del laboratorio (Semana 8, segunda sesión).
///
/// Cierra el hallazgo A10 de la actividad de la Clase 8: /token emitía EL ROL
/// QUE EL CLIENTE LE PEDÍA. Un usuario de consulta pedía rol=operador y salía
/// con un token de operador firmado por nosotros. Ninguna política de
/// autorización arregla eso: la puerta estaba en la emisión.
///
/// REGLA: el rol lo decide el servidor a partir de quién es el usuario.
/// El cliente solo dice quién es, y lo demuestra.
///
/// En producción esto es un servicio de identidad con su propia base
/// (Entra ID, Keycloak, Auth0…). Aquí es un diccionario en memoria para que la
/// clase quepa en una sesión, pero guarda las contraseñas COMO SE DEBE.
/// </summary>
public sealed class DirectorioUsuarios
{
    // PBKDF2-SHA256. Las iteraciones son el punto: hacen que calcular UN hash
    // cueste decenas de milisegundos. Para un usuario legítimo es imperceptible;
    // para quien robó la tabla y prueba millones de contraseñas, es la diferencia
    // entre horas y siglos.
    private const int Iteraciones = 100_000;

    private sealed record Usuario(string Rol, byte[] Sal, byte[] Hash);

    // Contraseñas de laboratorio (documentadas en el instructivo, NO son secretas):
    //   ana.operadora / Operadora-2026      → operador
    //   luis.consulta / Consulta-2026       → consulta
    //   core-bancario / CoreBancario-2026   → banco    (identidad de SERVICIO)
    //
    // Lo que se guarda no es la contraseña: es una sal aleatoria y el resultado
    // de PBKDF2. Quien lea este archivo no puede sacar la contraseña de aquí.
    private static readonly Dictionary<string, Usuario> Usuarios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ana.operadora"] = new("operador",
            Convert.FromBase64String("vAEMPm2rxVOAJdYwAF0uJw=="),
            Convert.FromBase64String("43mDRWihZL9EtGPkOdUW4/+N3lvGYFBcez1Ku1YMpvY=")),

        ["luis.consulta"] = new("consulta",
            Convert.FromBase64String("IzwXFh78qep61Ll6ULzTBg=="),
            Convert.FromBase64String("Li+ouZ7A4XcqSCzmSPnPlTy7O5ffmfinCuL3hXn7I48=")),

        ["core-bancario"] = new("banco",
            Convert.FromBase64String("E46cXWjaCu0S2y21GLHZMg=="),
            Convert.FromBase64String("WRikkSyD13o4nsumqN4E4DNfpR+GoFKki+6+atxcQuo=")),
    };

    private static readonly byte[] SalFicticia = new byte[16];

    /// <summary>Devuelve el rol si usuario y contraseña son correctos; si no, null.</summary>
    public string? Verificar(string? usuario, string? clave)
    {
        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave))
            return null;

        var existe = Usuarios.TryGetValue(usuario, out var u);

        // Si el usuario NO existe, igual se calcula un hash. Sin esto, "usuario
        // inexistente" responde en microsegundos y "contraseña equivocada" en
        // decenas de milisegundos, y midiendo el tiempo se averigua qué usuarios
        // existen. Mismo trabajo en los dos casos, misma respuesta.
        var calculado = Rfc2898DeriveBytes.Pbkdf2(clave, existe ? u!.Sal : SalFicticia,
                                                  Iteraciones, HashAlgorithmName.SHA256, 32);

        if (!existe) return null;

        // Comparación en tiempo constante: no se detiene en el primer byte distinto.
        return CryptographicOperations.FixedTimeEquals(calculado, u!.Hash) ? u.Rol : null;
    }
}
