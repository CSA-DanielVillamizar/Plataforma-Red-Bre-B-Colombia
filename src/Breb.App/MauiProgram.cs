using Microsoft.Extensions.Logging;
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

		// Clase 14 (trampa 3): dentro del EMULADOR, localhost es el emulador,
		// no esta maquina. 10.0.2.2 es la direccion que el emulador reserva
		// para hablar con el host. En un telefono real esto cambia por la IP
		// de la maquina en la red WiFi (eso llega mas adelante en el curso).
		builder.Services.AddHttpClient<ClienteBrebApi>(http =>
		{
			http.BaseAddress = new Uri("http://10.0.2.2:5080");
			http.Timeout = TimeSpan.FromSeconds(10);
		});

		builder.Services.AddSingleton<IBovedaSegura, BovedaSecureStorage>();
		builder.Services.AddSingleton<IAlmacenSesion, AlmacenSesion>();
		builder.Services.AddSingleton<INavegacion, NavegacionShell>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<SaldoViewModel>();
		builder.Services.AddTransient<TransferenciaViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<SaldoPage>();
		builder.Services.AddTransient<TransferenciaPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
