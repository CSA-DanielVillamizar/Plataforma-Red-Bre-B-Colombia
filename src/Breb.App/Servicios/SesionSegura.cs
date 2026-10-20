using Breb.App.Modelos;

namespace Breb.App.Servicios;

/// <summary>
/// Guarda el JWT en SecureStorage: en Android lo cifra con una clave del
/// Android Keystore, que no sale del dispositivo. Ni la vista ni los
/// ViewModels tocan el token: solo ClienteBrebApi lo lee para el Bearer.
/// </summary>
public sealed class SesionSegura
{
    private const string ClaveToken = "breb.token";
    private const string ClaveExpira = "breb.expira";
    private const string ClaveRol = "breb.rol";
    private const string ClaveUsuario = "breb.usuario";

    public async Task GuardarAsync(string usuario, RespuestaToken token)
    {
        await SecureStorage.Default.SetAsync(ClaveToken, token.Token);
        await SecureStorage.Default.SetAsync(ClaveExpira, token.Expira.ToUnixTimeSeconds().ToString());
        await SecureStorage.Default.SetAsync(ClaveRol, token.Rol);
        await SecureStorage.Default.SetAsync(ClaveUsuario, usuario);
    }

    /// <summary>El token vigente, o null si no hay sesión o ya expiró.</summary>
    public async Task<string?> ObtenerTokenAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(ClaveToken);
            var expira = await SecureStorage.Default.GetAsync(ClaveExpira);
            if (token is null || !long.TryParse(expira, out var segundos))
                return null;

            if (DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow)
            {
                Cerrar();
                return null;
            }
            return token;
        }
        catch (Exception)
        {
            // Si Android restauró un respaldo, la clave del Keystore ya no
            // descifra lo guardado. Se descarta y se pide login de nuevo.
            Cerrar();
            return null;
        }
    }

    public async Task<string> DescribirAsync()
    {
        var usuario = await SecureStorage.Default.GetAsync(ClaveUsuario);
        var rol = await SecureStorage.Default.GetAsync(ClaveRol);
        return $"{usuario} · rol {rol}";
    }

    public void Cerrar() => SecureStorage.Default.RemoveAll();
}
