using System.Net;
using Breb.App.Servicios;
using Breb.App.Tests.Apoyo;

namespace Breb.App.Tests;

public class ClienteBrebApiTests
{
    private static readonly Guid Cuenta = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Token_envia_usuario_y_clave_en_el_cuerpo()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.OK,
            """{"token":"abc","expira":"2026-10-21T12:00:00Z","tipo":"Bearer","rol":"operador","duracionMinutos":15}""");

        var token = await api.ObtenerTokenAsync("ana.operadora", "Operadora-2026");

        Assert.Equal("abc", token.Token);
        Assert.Equal("operador", token.Rol);
        var solicitud = Assert.Single(handler.Solicitudes);
        Assert.Equal(HttpMethod.Post, solicitud.Metodo);
        Assert.Equal("/token", solicitud.Ruta);
        Assert.Contains("ana.operadora", solicitud.Cuerpo);
        Assert.Contains("Operadora-2026", solicitud.Cuerpo);
    }

    [Fact]
    public async Task Login_rechazado_es_401_de_login()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.Unauthorized, "");

        var ex = await Assert.ThrowsAsync<ExcepcionDeApi>(() => api.ObtenerTokenAsync("ana", "mala"));

        Assert.Equal(401, ex.Codigo);
        Assert.Equal("login", ex.Operacion);
    }

    [Fact]
    public async Task Saldo_manda_el_bearer()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.OK,
            """{"cuentaId":"11111111-1111-1111-1111-111111111111","saldoDisponible":5000,"saldoRetenido":0}""");

        var saldo = await api.ConsultarSaldoAsync(Cuenta, "tok-1");

        Assert.Equal(5000m, saldo.SaldoDisponible);
        var solicitud = Assert.Single(handler.Solicitudes);
        Assert.Equal(HttpMethod.Get, solicitud.Metodo);
        Assert.Equal($"/cuentas/{Cuenta}/saldo", solicitud.Ruta);
        Assert.Equal("Bearer", solicitud.Esquema);
        Assert.Equal("tok-1", solicitud.Token);
    }

    [Fact]
    public async Task Retener_manda_el_monto_en_la_query_y_no_pide_idempotencia()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.OK,
            """{"transferenciaId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","montoUVB":10.5}""");

        var retencion = await api.RetenerAsync(Cuenta, 10.5m, "tok-op");

        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), retencion.TransferenciaId);
        Assert.Equal(10.5m, retencion.MontoUVB);
        var solicitud = Assert.Single(handler.Solicitudes);
        Assert.Equal(HttpMethod.Post, solicitud.Metodo);
        Assert.Equal($"/cuentas/{Cuenta}/retener?montoUVB=10.5", solicitud.Ruta);
        Assert.Equal("Bearer", solicitud.Esquema);
        Assert.Equal("tok-op", solicitud.Token);
        Assert.False(solicitud.TraeClaveDeIdempotencia);
        Assert.Null(solicitud.Cuerpo);
    }

    [Fact]
    public async Task Retener_400_conserva_el_mensaje_del_dominio()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.BadRequest, "\"Saldo insuficiente para retener.\"");

        var ex = await Assert.ThrowsAsync<ExcepcionDeApi>(() => api.RetenerAsync(Cuenta, 999999m, "tok"));

        Assert.Equal(400, ex.Codigo);
        Assert.Equal("transferencia", ex.Operacion);
        Assert.Equal("Saldo insuficiente para retener.", ex.Detalle);
    }

    [Fact]
    public async Task Retener_403_no_se_confunde_con_un_401()
    {
        var (api, handler) = ClienteFalso.Crear();
        handler.Encolar(HttpStatusCode.Forbidden, "");

        var ex = await Assert.ThrowsAsync<ExcepcionDeApi>(() => api.RetenerAsync(Cuenta, 10m, "tok-consulta"));

        Assert.Equal(403, ex.Codigo);
        Assert.Equal("transferencia", ex.Operacion);
    }
}
