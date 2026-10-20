using Microsoft.Extensions.Logging;
using Breb.App.Pantallas;
using Breb.App.Servicios;
using Breb.App.ViewModels;

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

		// Dentro del EMULADOR, localhost es el emulador, no esta maquina.
		// 10.0.2.2 es la direccion que el emulador reserva para el host.
		// En un telefono real se cambia por la IP de la maquina en la red WiFi.
		builder.Services.AddHttpClient<ClienteBrebApi>(http =>
		{
			http.BaseAddress = new Uri("http://10.0.2.2:5080");
			http.Timeout = TimeSpan.FromSeconds(10);
		});

		// Una sola sesion para toda la app: las tres pantallas comparten el token.
		builder.Services.AddSingleton<AlmacenToken>();

		builder.Services.AddTransient<InicioSesionViewModel>();
		builder.Services.AddTransient<SaldoViewModel>();
		builder.Services.AddTransient<TransferenciaViewModel>();

		builder.Services.AddTransient<InicioSesionPage>();
		builder.Services.AddTransient<SaldoPage>();
		builder.Services.AddTransient<TransferenciaPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
