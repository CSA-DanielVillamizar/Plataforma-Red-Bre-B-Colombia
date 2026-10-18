using Microsoft.Extensions.DependencyInjection;

namespace Breb.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Headline y el resto de estilos viven en App.xaml. Si el Shell entra
        // por el constructor, el contenedor crea las páginas antes de esta
        // línea y el arranque muere con "StaticResource not found".
        var shell = IPlatformApplication.Current!.Services.GetRequiredService<AppShell>();
        return new Window(shell);
    }
}
