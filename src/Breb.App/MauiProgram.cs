using Microsoft.Extensions.Logging;
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

		// Clase 14 (trampa 3): dentro del EMULADOR, localhost es el emulador,
		// no esta maquina. 10.0.2.2 es la direccion que el emulador reserva
		// para hablar con el host. En un telefono real esto cambia por la IP
		// de la maquina en la red WiFi (eso llega mas adelante en el curso).
		builder.Services.AddHttpClient<ClienteBrebApi>(http =>
		{
			http.BaseAddress = new Uri("http://10.0.2.2:5080");
			http.Timeout = TimeSpan.FromSeconds(10);
		});

		builder.Services.AddTransient<SaldoViewModel>();
		builder.Services.AddTransient<MainPage>();

		// Clase 18: la segunda pantalla. Sin registrar aqui el ViewModel y la
		// pagina, el Shell no puede construirla: la pestana sale en blanco y
		// no aparece ningun error a la vista.
		builder.Services.AddTransient<TransferenciaViewModel>();
		builder.Services.AddTransient<TransferenciaPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
