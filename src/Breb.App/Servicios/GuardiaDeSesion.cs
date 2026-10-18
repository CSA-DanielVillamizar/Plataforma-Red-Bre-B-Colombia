namespace Breb.App.Servicios;

/// <summary>
/// Saldo y Transferir no se muestran sin un token vigente. Si no lo hay,
/// se vuelve a Entrar y se vacía la pila.
/// </summary>
public static class GuardiaDeSesion
{
    public static async Task<SesionGuardada?> ParaPantallaAsync(
        IAlmacenSesion almacen, INavegacion nav, DateTimeOffset ahora)
    {
        SesionGuardada? sesion;
        try
        {
            sesion = await almacen.LeerAsync();
        }
        catch (ExcepcionAlmacenSeguro)
        {
            await nav.IrAsync(Rutas.Login);
            throw;
        }

        if (sesion is not null && VigenciaDelToken.SigueVigente(sesion.Token, ahora))
            return sesion;

        if (sesion is not null)
        {
            try { await almacen.BorrarAsync(); }
            catch (ExcepcionAlmacenSeguro) { /* igual se sale de la pantalla */ }
        }

        await nav.IrAsync(Rutas.Login);
        return null;
    }
}
