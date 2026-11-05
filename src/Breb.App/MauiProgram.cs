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

		// Clase 19: la direccion de la API ya no se decide aqui.
		//
		// Estaba fija en 10.0.2.2 para todo Android, y eso es un alias que
		// SOLO existe dentro del emulador: en un telefono fisico no resuelve
		// y ninguna llamada llega nunca. Como el sintoma es un timeout de
		// 10 s y no un error de direccion, parecia lentitud del servidor.
		//
		// Toda la decision —y el aviso de cuando falta configurar la IP de
		// la WiFi— vive ahora en ConfiguracionApi, con su explicacion de por
		// que en el escritorio se usa 127.0.0.1 y no localhost, y de lo que
		// hace un AppContainer de Windows con las conexiones a loopback.
		builder.Services.AddHttpClient<ClienteBrebApi>(http =>
		{
			http.BaseAddress = new Uri(ConfiguracionApi.Direccion);
			http.Timeout = TimeSpan.FromSeconds(10);
		});

		// La sesion del usuario: UN token para toda la app.
		//
		// Singleton porque es UNA sesion; dos instancias serian dos usuarios
		// distintos dentro de la misma app. Antes cada pantalla pedia su
		// propio token en cada operacion, y con usuarios distintos.
		builder.Services.AddSingleton<SesionUsuario>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();

		builder.Services.AddTransient<SaldoViewModel>();
		builder.Services.AddTransient<MainPage>();

		// Clase 18: la segunda pantalla. Sin registrar aqui el ViewModel y la
		// pagina, el Shell no puede construirla: la pestana sale en blanco y
		// no aparece ningun error a la vista.
		builder.Services.AddTransient<TransferenciaViewModel>();
		builder.Services.AddTransient<TransferenciaPage>();

		// La cola local. Singleton porque es UNA cola: dos instancias
		// escribiendo el mismo archivo se pisarian, y el candado que las
		// serializa vive dentro de la instancia.
		//
		// AppDataDirectory es la carpeta privada de la app que el sistema
		// RESPALDA y no borra al limpiar cache. Si esto apuntara a
		// CacheDirectory, Android podria borrar las transferencias
		// pendientes del usuario cuando necesitara espacio.
		builder.Services.AddSingleton(new ColaOperaciones(FileSystem.AppDataDirectory));

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
