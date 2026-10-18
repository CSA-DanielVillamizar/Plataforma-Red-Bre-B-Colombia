using System.Globalization;
using System.Net;
using Breb.App.Tests.Apoyo;
using Breb.App.ViewModels;

namespace Breb.App.Tests;

public class SaldoViewModelTests
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

    [Fact]
    public async Task Consultar_muestra_el_saldo_y_el_rol()
    {
        var (vm, handler) = Crear();
        handler.Encolar(HttpStatusCode.OK, TokenConsulta);
        handler.Encolar(HttpStatusCode.OK, SaldoInicial);

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal("ok", vm.Tono);
        Assert.Contains("consulta", vm.Estado);
        Assert.EndsWith("UVB", vm.SaldoTexto);
        Assert.DoesNotContain("—", vm.SaldoTexto);
        Assert.False(vm.Cargando);
        Assert.Equal("Bearer", handler.Solicitudes[1].Esquema);
    }

    [Fact]
    public async Task Transferir_con_operador_confirma_y_refresca_el_saldo()
    {
        var (vm, handler) = Crear();
        vm.Usuario = "ana.operadora";
        vm.Clave = "Operadora-2026";
        vm.MontoTexto = "10";
        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.OK, RetencionOk);
        handler.Encolar(HttpStatusCode.OK, SaldoTrasRetener);

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal("ok", vm.Tono);
        Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", vm.ResultadoTransferencia);
        Assert.Contains("plata quedó apartada", vm.Estado);
        Assert.Equal(Uvb(4990m), vm.SaldoTexto);
        Assert.Equal(Uvb(10m), vm.SaldoRetenidoTexto);
        Assert.False(vm.Cargando);
        Assert.Equal(3, handler.Solicitudes.Count);
        Assert.Contains("/retener?montoUVB=10", handler.Solicitudes[1].Ruta);
        Assert.False(handler.Solicitudes[1].TraeClaveDeIdempotencia);
    }

    [Fact]
    public async Task Transferir_reutiliza_el_token_de_la_consulta()
    {
        var (vm, handler) = Crear();
        vm.Usuario = "ana.operadora";
        vm.Clave = "Operadora-2026";
        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.OK, SaldoInicial);
        handler.Encolar(HttpStatusCode.OK, RetencionOk);
        handler.Encolar(HttpStatusCode.OK, SaldoTrasRetener);

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);
        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal(4, handler.Solicitudes.Count);
        Assert.Equal("/token", handler.Solicitudes[0].Ruta);
        Assert.DoesNotContain(handler.Solicitudes.Skip(1), s => s.Ruta == "/token");
        Assert.Equal("abc", handler.Solicitudes[2].Token);
    }

    [Fact]
    public async Task Consulta_que_transfiere_ve_el_403()
    {
        var (vm, handler) = Crear();
        handler.Encolar(HttpStatusCode.OK, TokenConsulta);
        handler.Encolar(HttpStatusCode.Forbidden, "");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal("error", vm.Tono);
        Assert.Contains("ana.operadora", vm.Estado);
        Assert.Contains("permiso", vm.Estado);
        Assert.Equal(string.Empty, vm.ResultadoTransferencia);
        Assert.Equal("—", vm.SaldoTexto);
    }

    [Fact]
    public async Task Saldo_insuficiente_se_muestra_como_validacion()
    {
        var (vm, handler) = Crear();
        vm.Usuario = "ana.operadora";
        vm.Clave = "Operadora-2026";
        vm.MontoTexto = "999999";
        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.BadRequest, "\"Saldo insuficiente para retener.\"");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal("error", vm.Tono);
        Assert.Contains("Saldo insuficiente para retener.", vm.Estado);
        Assert.StartsWith("No se pudo transferir:", vm.Estado);
    }

    [Fact]
    public async Task Clave_mala_no_habla_de_token_vencido()
    {
        var (vm, handler) = Crear();
        handler.Encolar(HttpStatusCode.Unauthorized, "");

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal("Usuario o clave incorrectos.", vm.Estado);
        Assert.Equal("—", vm.SaldoTexto);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Token_vencido_al_transferir_pide_volver_a_entrar()
    {
        var (vm, handler) = Crear();
        vm.Usuario = "ana.operadora";
        vm.Clave = "Operadora-2026";
        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.OK, SaldoInicial);
        handler.Encolar(HttpStatusCode.Unauthorized, "");

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);
        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("sesión venció", vm.Estado);
        Assert.Equal("error", vm.Tono);
        Assert.Equal(Uvb(5000m), vm.SaldoTexto);

        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.OK, RetencionOk);
        handler.Encolar(HttpStatusCode.OK, SaldoTrasRetener);
        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Equal("/token", handler.Solicitudes[^3].Ruta);
        Assert.Equal("ok", vm.Tono);
    }

    [Fact]
    public async Task Sin_red_es_un_mensaje_visible()
    {
        var (vm, handler) = Crear();
        handler.Falla = new HttpRequestException("Connection refused");

        await vm.ConsultarSaldoCommand.ExecuteAsync(null);

        Assert.Equal("error", vm.Tono);
        Assert.Contains("No hay conexión", vm.Estado);
        Assert.Contains("10.0.2.2", vm.Estado);
        Assert.Equal("—", vm.SaldoTexto);
        Assert.False(vm.Cargando);
    }

    [Fact]
    public async Task Timeout_no_se_presenta_como_un_500()
    {
        var (vm, handler) = Crear();
        handler.Falla = new TaskCanceledException();

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("no respondió a tiempo", vm.Estado);
        Assert.DoesNotContain("500", vm.Estado);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Fallo_del_servidor_se_distingue_de_la_validacion()
    {
        var (vm, handler) = Crear();
        vm.Usuario = "ana.operadora";
        handler.Encolar(HttpStatusCode.OK, TokenOk);
        handler.Encolar(HttpStatusCode.InternalServerError, "boom");

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Contains("HTTP 500", vm.Estado);
        Assert.Contains("servidor", vm.Estado);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Monto_invalido_no_llama_a_la_api()
    {
        var (vm, handler) = Crear();
        vm.MontoTexto = "0";

        await vm.TransferirCommand.ExecuteAsync(null);

        Assert.Empty(handler.Solicitudes);
        Assert.Contains("mayor que cero", vm.Estado);
        Assert.Equal("error", vm.Tono);
    }

    [Fact]
    public async Task Mientras_carga_los_dos_comandos_estan_apagados()
    {
        var (vm, handler) = Crear();
        var puerta = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.EncolarEspera(puerta);

        var enCurso = vm.ConsultarSaldoCommand.ExecuteAsync(null);
        await Esperar(() => vm.Cargando);

        Assert.False(vm.ConsultarSaldoCommand.CanExecute(null));
        Assert.False(vm.TransferirCommand.CanExecute(null));

        puerta.SetResult(HandlerFalso.Respuesta(HttpStatusCode.Unauthorized, "", "application/json"));
        await enCurso;

        Assert.False(vm.Cargando);
        Assert.True(vm.ConsultarSaldoCommand.CanExecute(null));
        Assert.True(vm.TransferirCommand.CanExecute(null));
    }

    private static string Uvb(decimal valor) =>
        valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO")) + " UVB";

    private static (SaldoViewModel Vm, HandlerFalso Handler) Crear()
    {
        var (api, handler) = ClienteFalso.Crear();
        return (new SaldoViewModel(api), handler);
    }

    private static async Task Esperar(Func<bool> condicion)
    {
        for (var i = 0; i < 50 && !condicion(); i++)
            await Task.Delay(20);
        Assert.True(condicion());
    }
}
