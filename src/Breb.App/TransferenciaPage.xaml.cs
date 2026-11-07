using Breb.App.ViewModels;

namespace Breb.App;

public partial class TransferenciaPage : ContentPage
{
    private readonly TransferenciaViewModel _viewModel;

    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // ── Sobre la regla del code-behind ──────────────────────────────────────
    // La regla de la Clase 15 es "sin LÓGICA en el code-behind", no "sin una
    // sola línea". Aquí hay cuatro líneas y ninguna decide nada: reenvían un
    // evento del ciclo de vida de la página al ViewModel.
    //
    // La diferencia importa. Si en lugar de esto hubiera un
    //
    //     if (hayPendientes) EtiquetaEstado.Text = "…";
    //
    // ahí sí se rompió el MVVM: la vista estaría decidiendo. Lo de abajo es
    // cableado — es el único punto donde la vista puede avisar que apareció,
    // porque OnAppearing es un método de la página, no un evento al que el
    // ViewModel pueda suscribirse.
    //
    // Para quitarlo del todo haría falta el EventToCommandBehavior del
    // CommunityToolkit.Maui, que es una dependencia más para ahorrar cuatro
    // líneas sin lógica. No vale la pena, y conviene saber por qué.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AparecerCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.DesaparecerCommand.Execute(null);
    }
}
