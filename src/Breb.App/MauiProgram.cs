using Breb.App.Services;
using Breb.App.ViewModels;
using Breb.App.Views;
using Microsoft.Extensions.Logging;

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

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();

		builder.Services.AddHttpClient<IAuthService, AuthService>(cliente =>
		{
			cliente.BaseAddress = new Uri(ApiConfig.BaseUrl);
			cliente.Timeout = TimeSpan.FromSeconds(10);
		});

		builder.Services.AddHttpClient<IBrebApiService, BrebApiService>(cliente =>
		{
			cliente.BaseAddress = new Uri(ApiConfig.BaseUrl);
			cliente.Timeout = TimeSpan.FromSeconds(10);
		});

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<SaldoViewModel>();
		builder.Services.AddTransient<TransferenciaViewModel>();

		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<SaldoPage>();
		builder.Services.AddTransient<TransferenciaPage>();

		builder.Services.AddSingleton<AppShell>();

		return builder.Build();
	}
}
