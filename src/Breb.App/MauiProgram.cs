using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm;
using Breb.App.Servicios;
using Breb.App.ViewModels;
using Breb.App.Vistas;

namespace Breb.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Servicios de aplicación (una sola instancia compartida) ─────
        builder.Services.AddSingleton<ConsolaLog>();
        builder.Services.AddSingleton<SesionSegura>();

        // ── HttpClient tipado para hablar con Breb.Cuentas ──────────────
        // La URL depende de la plataforma:
        //  · Simulador iOS: localhost apunta al Mac anfitrión.
        //  · Emulador Android: 10.0.2.2 es la máquina anfitriona (Clase 14).
        builder.Services.AddHttpClient<ClienteBrebApi>(http =>
        {
#if IOS
            http.BaseAddress = new Uri("http://localhost:5080");
#else
            http.BaseAddress = new Uri("http://10.0.2.2:5080");
#endif
            http.Timeout = TimeSpan.FromSeconds(10);
        });

        // ── ViewModels ──────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<SaldoViewModel>();
        builder.Services.AddTransient<TransferenciaViewModel>();
        builder.Services.AddSingleton<ConsolaViewModel>();

        // ── Páginas ─────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<SaldoPage>();
        builder.Services.AddTransient<TransferenciaPage>();
        builder.Services.AddSingleton<ConsolaPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
