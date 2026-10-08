using Android.App;
using Android.Runtime;

namespace Breb.App;

// ── Tráfico HTTP en claro, solo en Debug (Semana 11) ────────────────────────
// Android bloquea el HTTP sin cifrar desde API 28. Nuestra API de laboratorio
// vive en http://10.0.2.2:5080, así que sin esto el HttpClient de la app falla
// aunque el permiso INTERNET esté concedido.
//
// OJO: probar la URL en el navegador del emulador NO demuestra que la app
// pueda alcanzarla. El navegador tiene su propia política de red; la de la
// aplicación es independiente, y es la que manda para nuestro HttpClient.
//
// Va solo en Debug a propósito: una entrega que acepte HTTP en claro en
// Release estaría exponiendo tokens y saldos en texto plano.
#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
