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

		// Clase 14 (trampa 3): dentro del EMULADOR de Android, localhost es el
		// emulador, no esta maquina. 10.0.2.2 es la direccion que el emulador
		// reserva para hablar con el host.
		//
		// Clase 18: pero esa direccion SOLO existe dentro del emulador. Si la
		// app corre en Windows, 10.0.2.2 no resuelve y ninguna llamada llega:
		// la pantalla se queda en "Sin conexion con la API" y uno pierde media
		// clase buscando el error en el codigo que esta bien.
		//
		// En el escritorio se usa 127.0.0.1 y NO "localhost", a proposito: en
		// Windows localhost resuelve PRIMERO a ::1 por IPv6, y la API escucha
		// en 0.0.0.0, que es solo IPv4. Nombrar la IP evita depender de ese
		// orden de resolucion.
		//
		// Y una advertencia que costo media hora el 28 de octubre: si la app
		// se lanza desde dentro de un AppContainer de Windows —por ejemplo
		// como proceso hijo de una app empaquetada de la Store— el sistema
		// BLOQUEA sus conexiones a loopback Y a la red privada. No las
		// rechaza: las deja esperando, asi que el sintoma es un timeout de
		// 10 s y no un "sin conexion". Parece lentitud del servidor y no lo
		// es. La senal que lo delata es netstat: el socket queda en SYN_SENT.
		// La solucion es lanzar la app desde una consola normal.
		//
		// En un telefono REAL no sirve ninguna de las dos direcciones: hay
		// que poner la IP de esta maquina en la red WiFi. Eso es el 4 de
		// noviembre.
		var direccionApi = DeviceInfo.Platform == DevicePlatform.Android
			? "http://10.0.2.2:5080"       // emulador de Android
			: "http://127.0.0.1:5080";     // Windows y macOS

		builder.Services.AddHttpClient<ClienteBrebApi>(http =>
		{
			http.BaseAddress = new Uri(direccionApi);
			http.Timeout = TimeSpan.FromSeconds(10);
		});

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
