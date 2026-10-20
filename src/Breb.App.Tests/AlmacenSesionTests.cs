using Breb.App.Servicios;
using Breb.App.Tests.Apoyo;

namespace Breb.App.Tests;

public class AlmacenSesionTests
{
    [Fact]
    public async Task Guardar_escribe_token_rol_y_usuario_y_no_la_clave()
    {
        var boveda = new BovedaEnMemoria();
        var almacen = new AlmacenSesion(boveda);

        await almacen.GuardarAsync("tok", "operador", "ana.operadora");

        var sesion = await almacen.LeerAsync();
        Assert.Equal("tok", sesion!.Token);
        Assert.Equal("operador", sesion.Rol);
        Assert.Equal("ana.operadora", sesion.Usuario);
        Assert.Equal(3, boveda.Datos.Count);
        Assert.DoesNotContain(boveda.Datos.Values, v => v.Contains("Operadora"));
    }

    [Fact]
    public async Task Si_falla_a_medias_no_deja_token()
    {
        var boveda = new BovedaEnMemoria { FallarAlGuardarClave = AlmacenSesion.ClaveRol };
        var almacen = new AlmacenSesion(boveda);

        await Assert.ThrowsAsync<ExcepcionAlmacenSeguro>(() =>
            almacen.GuardarAsync("tok", "operador", "ana.operadora"));

        Assert.Null(await almacen.LeerAsync());
        Assert.DoesNotContain(AlmacenSesion.ClaveToken, boveda.Datos.Keys);
    }

    [Fact]
    public async Task Borrar_quita_las_tres_claves()
    {
        var boveda = new BovedaEnMemoria();
        var almacen = new AlmacenSesion(boveda);
        await almacen.GuardarAsync("tok", "consulta", "luis.consulta");

        await almacen.BorrarAsync();

        Assert.Empty(boveda.Datos);
        Assert.Null(await almacen.LeerAsync());
    }
}
