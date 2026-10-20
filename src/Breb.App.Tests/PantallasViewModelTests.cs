using System.Net;
using Breb.App.Servicios;
using Breb.App.Tests.Apoyo;
using Breb.App.ViewModels;

namespace Breb.App.Tests;

public class PantallasViewModelTests
{
    private const string TokenOk =
        """{"token":"abc","expira":"2026-10-21T12:00:00Z","tipo":"Bearer","rol":"operador","duracionMinutos":15}""";

    private const string TokenConsulta =
        """{"token":"consulta","expira":"2026-10-21T12:00:00Z","tipo":"Bearer","rol":"consulta","duracionMinutos":15}""";

    private const string SaldoInicial =
        """{"cuentaId":"11111111-1111-1111-1111-111111111111","saldoDisponible":5000,"saldoRetenido":0}""";

    private const string SaldoTrasRetener =
        """{"cuentaId":"11111111-1111-1111-1111-111111111111","saldoDisponible":4990,"saldoRetenido":10}""";

    private const string RetencionOk =
        """{"transferenciaId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","montoUVB":10}""";

    private static readonly SesionGuardada Operadora = new("abc", "operador", "ana.operadora");
    private static readonly SesionGuardada Consulta = new("consulta", "consulta", "luis.consulta");

    [Fact]
    public async Task Entrar_guarda_el_token_y_abre_saldo()
    {
        var (vm, boveda, _, nav, http) = Fabrica.Login();
        vm.Usuario = "ana.operadora";
        vm.Clave = "Operadora-2026";
        http.Encolar(HttpStatusCode.OK, TokenOk);

        await vm.EntrarCommand.ExecuteAsync(null);

        Assert.Equal("ok", vm.Tono);
        Assert.Equal("abc", boveda.Datos[AlmacenSesion.ClaveToken]);
        Assert.Equal("operador", boveda.Datos[AlmacenSesion.ClaveRol]);
        Assert.Equal("ana.operadora", boveda.Datos[AlmacenSesion.ClaveUsuario]);
        Assert.Equal(Rutas.Saldo, Assert.Single(nav.RutasVisitadas));
        Assert.False(vm.Cargando);
    }

    [Fact]
    public async Task Clave_mala_no_guarda_nada_ni_navega()
    {
        var (vm, boveda, _, nav, http) = Fabrica.Login();
        http.Encolar(HttpStatusCode.Unauthorized, "");

        await vm.EntrarCommand.ExecuteAsync(null);

        Assert.Equal("Usuario o clave incorrectos.", vm.Estado);
        Assert.Empty(boveda.Datos);
        Assert.Empty(nav.RutasVisitadas);
    }

    [Fact]
    public async Task Fallo_del_almacen_se_ve_y_no_navega()
    {
        var (vm, boveda, _, nav, http) = Fabrica.Login();
        boveda.FallarAlGuardarClave = AlmacenSesion.ClaveToken;
        http.Encolar(HttpStatusCode.OK, TokenOk);

        await vm.EntrarCommand.ExecuteAsync(null);

        Assert.Equal("error", vm.Tono);
        Assert.Contains("almacén seguro", vm.Estado);
        Assert.Empty(nav.RutasVisitadas);
    }

    [Fact]
    public async Task Saldo_usa_el_token_guardado_y_no_pide_otro()
    {
        var (vm, _, _, http) = Fabrica.Saldo(Consulta);
        http.Encolar(HttpStatusCode.OK, SaldoInicial);

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal(ViewModelBase.FormatoUvb(5000m), vm.SaldoTexto);
        Assert.Equal("ok", vm.Tono);
        Assert.Contains("consulta", vm.Estado);
        var solicitud = Assert.Single(http.Solicitudes);
        Assert.Equal("Bearer", solicitud.Esquema);
        Assert.Equal("consulta", solicitud.Token);
        Assert.DoesNotContain(http.Solicitudes, s => s.Ruta == "/token");
    }

    [Fact]
    public async Task Saldo_sin_sesion_no_llama_a_la_api()
    {
        var (vm, _, _, http) = Fabrica.Saldo();

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Empty(http.Solicitudes);
        Assert.Contains("Entrar", vm.Estado);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Saldo_401_borra_el_token()
    {
        var (vm, almacen, _, http) = Fabrica.Saldo(Operadora);
        http.Encolar(HttpStatusCode.Unauthorized, "");

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Contains("sesión venció", vm.Estado);
        Assert.Null(await almacen.LeerAsync());
        Assert.Equal("—", vm.SaldoTexto);
    }

    [Fact]
    public async Task Cerrar_sesion_borra_y_vuelve_a_entrar()
    {
        var (vm, almacen, nav, _) = Fabrica.Saldo(Operadora);

        await vm.CerrarSesionCommand.ExecuteAsync(null);

        Assert.Null(await almacen.LeerAsync());
        Assert.Equal(Rutas.Login, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Ir_a_transferir_cambia_de_pantalla()
    {
        var (vm, _, nav, _) = Fabrica.Saldo(Operadora);

        await vm.IrATransferenciaCommand.ExecuteAsync(null);

        Assert.Equal(Rutas.Transferencia, Assert.Single(nav.RutasVisitadas));
    }

    [Fact]
    public async Task Transferir_confirma_refresca_y_avisa_los_15_segundos()
    {
        var (vm, _, _, http) = Fabrica.Transferencia(Operadora);
        http.Encolar(HttpStatusCode.OK, RetencionOk);
        http.Encolar(HttpStatusCode.OK, SaldoTrasRetener);

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", vm.ResultadoTransferencia);
        Assert.Contains("15 segundos", vm.ResultadoTransferencia);
        Assert.Contains("plata quedó apartada", vm.Estado);
        Assert.Equal(ViewModelBase.FormatoUvb(4990m), vm.SaldoTexto);
        Assert.Equal(ViewModelBase.FormatoUvb(10m), vm.SaldoRetenidoTexto);
        Assert.Equal(2, http.Solicitudes.Count);
        Assert.Contains("/retener?montoUVB=10", http.Solicitudes[0].Ruta);
        Assert.Equal("abc", http.Solicitudes[0].Token);
        Assert.False(http.Solicitudes[0].TraeClaveDeIdempotencia);
        Assert.DoesNotContain(http.Solicitudes, s => s.Ruta == "/token");
    }

    [Fact]
    public async Task Consulta_que_transfiere_ve_el_403_y_conserva_el_token()
    {
        var (vm, almacen, _, http) = Fabrica.Transferencia(Consulta);
        http.Encolar(HttpStatusCode.Forbidden, "");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(
            "No tienes permisos para transferir, hace falta el rol operador",
            vm.Estado);
        Assert.Equal(string.Empty, vm.ResultadoTransferencia);
        Assert.NotNull(await almacen.LeerAsync());
    }

    [Fact]
    public async Task Saldo_insuficiente_se_muestra_como_validacion()
    {
        var (vm, _, _, http) = Fabrica.Transferencia(Operadora);
        vm.MontoTexto = "999999";
        http.Encolar(HttpStatusCode.BadRequest, "\"Saldo insuficiente para retener.\"");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("Saldo insuficiente para retener.", vm.Estado);
        Assert.StartsWith("No se pudo transferir:", vm.Estado);
    }

    [Fact]
    public async Task Token_vencido_al_transferir_borra_la_sesion()
    {
        var (vm, almacen, _, http) = Fabrica.Transferencia(Operadora);
        http.Encolar(HttpStatusCode.Unauthorized, "");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("sesión venció", vm.Estado);
        Assert.Null(await almacen.LeerAsync());
    }

    [Fact]
    public async Task Sin_red_en_el_saldo_es_un_mensaje_visible()
    {
        var (vm, _, _, http) = Fabrica.Saldo(Consulta);
        http.Falla = new HttpRequestException("Connection refused");

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Contains("No hay conexión", vm.Estado);
        Assert.Contains("10.0.2.2", vm.Estado);
        Assert.False(vm.Cargando);
    }

    [Fact]
    public async Task Timeout_al_entrar_no_se_presenta_como_un_500()
    {
        var (vm, _, _, _, http) = Fabrica.Login();
        http.Falla = new TaskCanceledException();

        await vm.EntrarCommand.ExecuteAsync(null);

        Assert.Contains("no respondió a tiempo", vm.Estado);
        Assert.DoesNotContain("500", vm.Estado);
    }

    [Fact]
    public async Task Fallo_del_servidor_al_transferir()
    {
        var (vm, _, _, http) = Fabrica.Transferencia(Operadora);
        http.Encolar(HttpStatusCode.InternalServerError, "boom");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("HTTP 500", vm.Estado);
        Assert.Contains("servidor", vm.Estado);
    }

    [Fact]
    public async Task Monto_invalido_no_llama_a_la_api()
    {
        var (vm, _, _, http) = Fabrica.Transferencia(Operadora);
        vm.MontoTexto = "0";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Empty(http.Solicitudes);
        Assert.Contains("mayor que cero", vm.Estado);
    }

    [Fact]
    public async Task Mientras_entra_el_boton_esta_apagado()
    {
        var (vm, _, _, _, http) = Fabrica.Login();
        var puerta = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        http.EncolarEspera(puerta);

        var enCurso = vm.EntrarCommand.ExecuteAsync(null);
        await Esperar(() => vm.Cargando);

        Assert.False(vm.EntrarCommand.CanExecute(null));

        puerta.SetResult(HandlerFalso.Respuesta(HttpStatusCode.Unauthorized, "", "application/json"));
        await enCurso;

        Assert.False(vm.Cargando);
        Assert.True(vm.EntrarCommand.CanExecute(null));
    }

    private static async Task Esperar(Func<bool> condicion)
    {
        for (var i = 0; i < 50 && !condicion(); i++)
            await Task.Delay(20);
        Assert.True(condicion());
    }
}
