using Breb.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Breb.App;

public partial class AppShell : Shell
{
	public AppShell(IServiceProvider services)
	{
		InitializeComponent();

		Items.Add(new ShellContent
		{
			Title = "Login",
			Route = "login",
			ContentTemplate = new DataTemplate(() => services.GetRequiredService<LoginPage>())
		});

		Routing.RegisterRoute("saldo", typeof(SaldoPage));
		Routing.RegisterRoute("transferencia", typeof(TransferenciaPage));
	}
}
