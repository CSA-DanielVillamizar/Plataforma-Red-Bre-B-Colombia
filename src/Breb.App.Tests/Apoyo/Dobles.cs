using Breb.App.Servicios;
using Breb.App.ViewModels;

namespace Breb.App.Tests.Apoyo;

internal sealed class BovedaEnMemoria : IBovedaSegura
{
    public Dictionary<string, string> Datos { get; } = new();
    public string? FallarAlGuardarClave { get; set; }
    public Exception? FallarAlLeer { get; set; }

    public Task GuardarAsync(string clave, string valor)
    {
        if (clave == FallarAlGuardarClave)
            throw new ExcepcionAlmacenSeguro(
                "No se pudo guardar la sesión en el almacén seguro del teléfono (fallo de prueba).");
        Datos[clave] = valor;
        return Task.CompletedTask;
    }

    public Task<string?> LeerAsync(string clave)
    {
        if (FallarAlLeer is not null) throw FallarAlLeer;
        return Task.FromResult(Datos.TryGetValue(clave, out var valor) ? valor : null);
    }

    public Task BorrarAsync(string clave)
    {
        Datos.Remove(clave);
        return Task.CompletedTask;
    }
}

internal sealed class NavegacionFalsa : INavegacion
{
    public List<string> RutasVisitadas { get; } = new();

    public Task IrAsync(string ruta)
    {
        RutasVisitadas.Add(ruta);
        return Task.CompletedTask;
    }
}

internal static class Fabrica
{
    public static (LoginViewModel Vm, BovedaEnMemoria Boveda, AlmacenSesion Almacen, NavegacionFalsa Nav, HandlerFalso Http)
        Login()
    {
        var (api, http) = ClienteFalso.Crear();
        var boveda = new BovedaEnMemoria();
        var almacen = new AlmacenSesion(boveda);
        var nav = new NavegacionFalsa();
        return (new LoginViewModel(api, almacen, nav), boveda, almacen, nav, http);
    }

    public static (SaldoViewModel Vm, AlmacenSesion Almacen, NavegacionFalsa Nav, HandlerFalso Http)
        Saldo(SesionGuardada? sesion = null)
    {
        var (api, http) = ClienteFalso.Crear();
        var boveda = new BovedaEnMemoria();
        var almacen = new AlmacenSesion(boveda);
        if (sesion is not null)
            almacen.GuardarAsync(sesion.Token, sesion.Rol, sesion.Usuario).GetAwaiter().GetResult();
        var nav = new NavegacionFalsa();
        return (new SaldoViewModel(api, almacen, nav), almacen, nav, http);
    }

    public static (TransferenciaViewModel Vm, AlmacenSesion Almacen, NavegacionFalsa Nav, HandlerFalso Http)
        Transferencia(SesionGuardada? sesion = null)
    {
        var (api, http) = ClienteFalso.Crear();
        var boveda = new BovedaEnMemoria();
        var almacen = new AlmacenSesion(boveda);
        if (sesion is not null)
            almacen.GuardarAsync(sesion.Token, sesion.Rol, sesion.Usuario).GetAwaiter().GetResult();
        var nav = new NavegacionFalsa();
        return (new TransferenciaViewModel(api, almacen, nav), almacen, nav, http);
    }
}
