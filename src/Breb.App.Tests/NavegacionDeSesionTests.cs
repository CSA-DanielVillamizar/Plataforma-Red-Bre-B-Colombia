using System.Text;
using Breb.App.Servicios;
using Breb.App.Tests.Apoyo;

namespace Breb.App.Tests;

public class VigenciaDelTokenTests
{
    [Fact]
    public void Un_token_con_exp_futuro_sigue_vigente()
    {
        var token = JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.True(VigenciaDelToken.SigueVigente(token, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Un_token_vencido_o_ilegible_no_sirve()
    {
        var ahora = DateTimeOffset.UtcNow;
        Assert.False(VigenciaDelToken.SigueVigente(null, ahora));
        Assert.False(VigenciaDelToken.SigueVigente("abc", ahora));
        Assert.False(VigenciaDelToken.SigueVigente(JwtDePrueba.ConExpira(ahora.AddMinutes(-1)), ahora));
        Assert.False(VigenciaDelToken.SigueVigente(JwtDePrueba.SinExp(), ahora));
    }
}

public class NavegacionDeSesionTests
{
    [Fact]
    public void La_ruta_de_login_vacia_la_pila_y_saldo_se_empuja()
    {
        Assert.StartsWith("//", Rutas.Login);
        Assert.Equal("saldo", Rutas.Saldo);
        Assert.Equal("transferencia", Rutas.Transferencia);
        Assert.Equal("..", Rutas.Atras);
    }

    [Fact]
    public async Task Al_abrir_con_token_vigente_pide_saldo()
    {
        var (vm, _, almacen, nav, _) = Fabrica.Login();
        await almacen.GuardarAsync(JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10)), "operador", "ana.operadora");

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Equal(Rutas.Saldo, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Al_abrir_sin_token_se_queda_en_entrar()
    {
        var (vm, _, _, nav, _) = Fabrica.Login();

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Empty(nav.RutasVisitadas);
    }

    [Fact]
    public async Task Al_abrir_con_token_vencido_lo_borra_y_no_navega()
    {
        var (vm, _, almacen, nav, _) = Fabrica.Login();
        await almacen.GuardarAsync(JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(-2)), "consulta", "luis.consulta");

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Empty(nav.RutasVisitadas);
        Assert.Null(await almacen.LeerAsync());
    }

    [Fact]
    public async Task La_segunda_aparicion_de_entrar_no_vuelve_a_saltar()
    {
        var (vm, _, almacen, nav, _) = Fabrica.Login();
        await almacen.GuardarAsync(JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10)), "operador", "ana.operadora");

        await vm.PrepararCommand.ExecuteAsync(null);
        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Single(nav.RutasVisitadas);
    }

    [Fact]
    public async Task Si_el_almacen_falla_al_abrir_no_navega()
    {
        var (vm, boveda, _, nav, _) = Fabrica.Login();
        boveda.FallarAlLeer = new ExcepcionAlmacenSeguro("No se pudo leer la sesión guardada en el teléfono.");

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Empty(nav.RutasVisitadas);
        Assert.Contains("leer", vm.Estado);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Saldo_sin_token_regresa_a_entrar()
    {
        var (vm, _, nav, _) = Fabrica.Saldo();

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Equal(Rutas.Login, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Saldo_con_token_vigente_no_redirige()
    {
        var sesion = new SesionGuardada(
            JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10)), "consulta", "luis.consulta");
        var (vm, _, nav, _) = Fabrica.Saldo(sesion);

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Empty(nav.RutasVisitadas);
        Assert.Contains("luis.consulta", vm.SesionTexto);
    }

    [Fact]
    public async Task Transferir_sin_token_regresa_a_entrar()
    {
        var (vm, _, nav, _) = Fabrica.Transferencia();

        await vm.PrepararCommand.ExecuteAsync(null);

        Assert.Equal(Rutas.Login, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Volver_a_saldo_es_un_paso_atras()
    {
        var sesion = new SesionGuardada(
            JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10)), "operador", "ana.operadora");
        var (vm, _, nav, _) = Fabrica.Transferencia(sesion);

        await vm.IrASaldoCommand.ExecuteAsync(null);

        Assert.Equal(Rutas.Atras, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Cerrar_sesion_en_transferir_borra_y_pide_entrar()
    {
        var sesion = new SesionGuardada(
            JwtDePrueba.ConExpira(DateTimeOffset.UtcNow.AddMinutes(10)), "operador", "ana.operadora");
        var (vm, almacen, nav, _) = Fabrica.Transferencia(sesion);

        await vm.CerrarSesionCommand.ExecuteAsync(null);

        Assert.Null(await almacen.LeerAsync());
        Assert.Equal(Rutas.Login, Assert.Single(nav.RutasVisitadas));
    }
}

internal static class JwtDePrueba
{
    public static string ConExpira(DateTimeOffset expira) =>
        Armar($$"""{"exp":{{expira.ToUnixTimeSeconds()}}}""");

    public static string SinExp() => Armar("""{"sub":"ana"}""");

    private static string Armar(string payload)
    {
        static string B64(string json)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        return $"{B64("""{"alg":"none"}""")}.{B64(payload)}.firma";
    }
}
