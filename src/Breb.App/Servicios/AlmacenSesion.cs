namespace Breb.App.Servicios;

/// <summary>
/// Guarda el token, el rol y el usuario en la bóveda. Tres claves, nada más.
/// Si a medias falla, borra lo que alcanzó a escribir: una sesión a medias
/// haría que Saldo crea que hay token y Transferencia no encuentre el rol.
/// </summary>
public sealed class AlmacenSesion(IBovedaSegura boveda) : IAlmacenSesion
{
    public const string ClaveToken = "breb.token";
    public const string ClaveRol = "breb.rol";
    public const string ClaveUsuario = "breb.usuario";

    public async Task GuardarAsync(string token, string rol, string usuario)
    {
        try
        {
            await boveda.GuardarAsync(ClaveToken, token);
            await boveda.GuardarAsync(ClaveRol, rol);
            await boveda.GuardarAsync(ClaveUsuario, usuario);
        }
        catch (Exception ex)
        {
            try { await BorrarAsync(); }
            catch { /* se informa el fallo original */ }

            if (ex is ExcepcionAlmacenSeguro) throw;
            throw new ExcepcionAlmacenSeguro(
                "No se pudo guardar la sesión en el almacén seguro del teléfono.", ex);
        }
    }

    public async Task<SesionGuardada?> LeerAsync()
    {
        var token = await boveda.LeerAsync(ClaveToken);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var rol = await boveda.LeerAsync(ClaveRol) ?? string.Empty;
        var usuario = await boveda.LeerAsync(ClaveUsuario) ?? string.Empty;
        return new SesionGuardada(token, rol, usuario);
    }

    public async Task BorrarAsync()
    {
        await boveda.BorrarAsync(ClaveToken);
        await boveda.BorrarAsync(ClaveRol);
        await boveda.BorrarAsync(ClaveUsuario);
    }
}
