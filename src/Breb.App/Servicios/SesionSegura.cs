namespace Breb.App.Servicios;

/// <summary>
/// Guarda el JWT de forma SEGURA, no en una variable global ni en un campo
/// del ViewModel. En iOS esto va al Keychain, en Android al KeyStore cifrado;
/// MAUI lo abstrae en <see cref="SecureStorage"/>. Requisito explícito del
/// Evaluable 3: "el token se guarda de forma segura".
/// </summary>
public sealed class SesionSegura
{
    private const string ClaveToken = "breb_jwt";
    private const string ClaveRol   = "breb_rol";

    // Caché en memoria para no ir al Keychain en cada request (el valor
    // sensible sigue viviendo cifrado en el almacén seguro).
    private string? _tokenEnMemoria;

    public string? Rol { get; private set; }

    public async Task GuardarAsync(string token, string rol)
    {
        _tokenEnMemoria = token;
        Rol = rol;
        await SecureStorage.Default.SetAsync(ClaveToken, token);
        await SecureStorage.Default.SetAsync(ClaveRol, rol);
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (_tokenEnMemoria is not null) return _tokenEnMemoria;
        _tokenEnMemoria = await SecureStorage.Default.GetAsync(ClaveToken);
        Rol ??= await SecureStorage.Default.GetAsync(ClaveRol);
        return _tokenEnMemoria;
    }

    public bool HaySesion => _tokenEnMemoria is not null;

    public void CerrarSesion()
    {
        _tokenEnMemoria = null;
        Rol = null;
        SecureStorage.Default.Remove(ClaveToken);
        SecureStorage.Default.Remove(ClaveRol);
    }
}
