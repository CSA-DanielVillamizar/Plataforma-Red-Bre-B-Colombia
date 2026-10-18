using Microsoft.Extensions.DependencyInjection;
using Breb.App.Servicios;
using Breb.App.Vistas;

namespace Breb.App;

public partial class AppShell : Shell
{
    private static bool _rutasListas;

    public AppShell(LoginPage login, IServiceProvider servicios)
    {
        InitializeComponent();
        ContenidoLogin.Content = login;
        RegistrarRutas(servicios);
    }

    /// <summary>
    /// Registra la ruta sin crear la página. Saldo y Transferir nacen al
    /// navegar, cuando App.InitializeComponent ya cargó Headline.
    /// </summary>
    private static void RegistrarRutas(IServiceProvider servicios)
    {
        if (_rutasListas) return;
        Routing.RegisterRoute(Rutas.Saldo, new PaginaDesdeDi<SaldoPage>(servicios));
        Routing.RegisterRoute(Rutas.Transferencia, new PaginaDesdeDi<TransferenciaPage>(servicios));
        _rutasListas = true;
    }

    private sealed class PaginaDesdeDi<TPagina> : RouteFactory where TPagina : Page
    {
        private readonly IServiceProvider _servicios;

        public PaginaDesdeDi(IServiceProvider servicios) => _servicios = servicios;

        public override Element GetOrCreate() => _servicios.GetRequiredService<TPagina>();

        public override Element GetOrCreate(IServiceProvider services) =>
            services.GetRequiredService<TPagina>();
    }
}
